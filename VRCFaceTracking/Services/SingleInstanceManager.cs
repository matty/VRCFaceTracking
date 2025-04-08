using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.Principal;
using System.Threading;
using Microsoft.Extensions.Logging;
using VRCFaceTracking.Contracts.Services;
using VRCFaceTracking.Helpers;

namespace VRCFaceTracking.Services;

/// <summary>
/// Manages the application's single-instance behavior by preventing multiple instances from running.
/// Provides robust process detection and termination capabilities.
/// </summary>
public class SingleInstanceManager : ISingleInstanceManager
{
    private readonly ILogger<SingleInstanceManager> _logger;
    private readonly Mutex _mutex;
    private readonly string _mutexName;
    private bool _ownsMutex;
    private bool _disposed;

    // Win32 API imports for more reliable process operations
    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool TerminateProcess(IntPtr hProcess, uint uExitCode);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool QueryFullProcessImageName(IntPtr hProcess, uint dwFlags, System.Text.StringBuilder lpExeName, ref uint lpdwSize);

    public SingleInstanceManager(ILogger<SingleInstanceManager> logger)
    {
        _logger = logger;

        // Create a unique mutex name based on the executable path
        // In MSIX we need a different approach since paths can be virtualized
        if (RuntimeHelper.IsMSIX)
        {
            // For MSIX, use a prefix with the app name
            _mutexName = "VRCFaceTracking_SingleInstanceMutex";
        }
        else
        {
            // For non-MSIX, include the path for extra uniqueness
            string executablePath = Process.GetCurrentProcess().MainModule?.FileName
                ?? Assembly.GetEntryAssembly()?.Location
                ?? typeof(SingleInstanceManager).Assembly.Location;

            // Sanitize the path to create a valid mutex name
            _mutexName = "VRCFaceTracking_" + executablePath
                .Replace('\\', '_')
                .Replace('/', '_')
                .Replace(':', '_')
                .Replace(' ', '_');

            // Limit the length to avoid exceeding the maximum mutex name length
            if (_mutexName.Length > 200)
                _mutexName = _mutexName.Substring(0, 200);
        }

        _logger.LogDebug("Creating mutex with name: {MutexName}", _mutexName);

        // Create or open the mutex, determining if we're the first instance
        bool createdNew;
        _mutex = new Mutex(true, _mutexName, out createdNew);
        _ownsMutex = createdNew;

        _logger.LogDebug("Mutex created new: {CreatedNew}", createdNew);
    }

    /// <summary>
    /// Checks if this is the first instance of the application.
    /// </summary>
    /// <returns>True if this is the first instance, False otherwise</returns>
    public bool IsFirstInstance()
    {
        return _ownsMutex;
    }

    /// <summary>
    /// Ensures this is the only instance of the application running by terminating other instances.
    /// </summary>
    /// <returns>True if this becomes the only instance, False if unable to terminate other instances</returns>
    public bool EnsureSingleInstance()
    {
        if (IsFirstInstance())
        {
            _logger.LogInformation("This is the first instance of the application");
            return true;
        }

        _logger.LogWarning("Another instance of the application is already running. Attempting to terminate it.");

        // Find and terminate other instances of this application
        if (TerminateOtherInstances())
        {
            // Try to acquire the mutex after terminating other instances
            var acquired = false;
            try
            {
                acquired = _mutex.WaitOne(5000, false);
                if (acquired)
                {
                    _ownsMutex = true;
                    _logger.LogInformation("Successfully acquired mutex after terminating other instances");
                    return true;
                }
            }
            catch (AbandonedMutexException)
            {
                // This exception means we acquired an abandoned mutex, which is fine
                _ownsMutex = true;
                _logger.LogInformation("Acquired abandoned mutex after terminating other instances");
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while trying to acquire mutex after terminating other instances");
            }
        }

        _logger.LogWarning("Failed to become the only instance. Another instance might still be running.");
        return false;
    }

    /// <summary>
    /// Finds and terminates other instances of this application.
    /// </summary>
    /// <returns>True if all other instances were terminated, False otherwise</returns>
    private bool TerminateOtherInstances()
    {
        var allTerminated = true;
        var currentProcessId = Process.GetCurrentProcess().Id;
        var currentProcessPath = GetProcessPath(Process.GetCurrentProcess());

        _logger.LogDebug("Current process ID: {ProcessId}, Path: {ProcessPath}", currentProcessId, currentProcessPath);

        if (string.IsNullOrEmpty(currentProcessPath))
        {
            _logger.LogWarning("Could not determine current process path");
            return false;
        }

        // Get all processes that match our executable name
        List<Process> targetProcesses = new();
        var exeName = Path.GetFileNameWithoutExtension(currentProcessPath);

        // Also check common variations
        foreach (var processName in new[] { exeName, "VRCFaceTracking" })
        {
            try
            {
                var processes = Process.GetProcessesByName(processName);
                targetProcesses.AddRange(processes);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while getting processes by name: {ProcessName}", processName);
            }
        }

        foreach (var process in targetProcesses)
        {
            try
            {
                // Skip our own process
                if (process.Id == currentProcessId)
                {
                    continue;
                }

                var processPath = GetProcessPath(process);
                _logger.LogDebug("Examining process ID: {ProcessId}, Path: {ProcessPath}", process.Id, processPath);

                // Check if it's the same application
                if (!string.IsNullOrEmpty(processPath) &&
                    Path.GetFileName(processPath).Equals(Path.GetFileName(currentProcessPath), StringComparison.OrdinalIgnoreCase))
                {
                    _logger.LogInformation("Found another instance with ID: {ProcessId}, Path: {ProcessPath}",
                        process.Id, processPath);

                    if (TerminateProcessEffectively(process))
                    {
                        _logger.LogInformation("Successfully terminated process {ProcessId}", process.Id);
                    }
                    else
                    {
                        _logger.LogWarning("Failed to terminate process {ProcessId}", process.Id);
                        allTerminated = false;
                    }
                }
            }
            catch (Exception ex) when (ex is not (Win32Exception or UnauthorizedAccessException))
            {
                // Don't log access errors as they're expected when dealing with other processes
                _logger.LogError(ex, "Error while examining process {ProcessId}", process.Id);
                allTerminated = false;
            }
            finally
            {
                try { process.Dispose(); } catch { }
            }
        }

        return allTerminated;
    }

    /// <summary>
    /// Gets the full path of a process.
    /// </summary>
    /// <param name="process">The process to get the path for</param>
    /// <returns>The full path of the process, or null if it couldn't be determined</returns>
    private string GetProcessPath(Process process)
    {
        try
        {
            // First try the standard .NET way
            if (process.MainModule != null)
            {
                return process.MainModule.FileName;
            }
        }
        catch (Win32Exception)
        {
            // Expected when we don't have access rights to the process
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Standard method to get process path failed for process {ProcessId}", process.Id);
        }

        try
        {
            // Alternative method using Windows API directly
            var buffer = new System.Text.StringBuilder(1024);
            var size = (uint)buffer.Capacity;

            if (QueryFullProcessImageName(process.Handle, 0, buffer, ref size))
            {
                return buffer.ToString(0, (int)size);
            }
        }
        catch { }

        return null;
    }

    /// <summary>
    /// Attempts to terminate a process using multiple methods.
    /// </summary>
    /// <param name="process">The process to terminate</param>
    /// <returns>True if the process was terminated, False otherwise</returns>
    private bool TerminateProcessEffectively(Process process)
    {
        // First try the .NET way
        if (TryTerminateWithNetMethod(process))
        {
            return true;
        }

        // If that fails, try the Win32 API
        if (TryTerminateWithWin32Api(process))
        {
            return true;
        }

        return !IsProcessRunning(process.Id);
    }

    private bool TryTerminateWithNetMethod(Process process)
    {
        try
        {
            process.Kill();
            process.WaitForExit(1000);
            return !IsProcessRunning(process.Id);
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Failed to terminate process {ProcessId} with .NET method", process.Id);
            return false;
        }
    }

    private bool TryTerminateWithWin32Api(Process process)
    {
        try
        {
            if (TerminateProcess(process.Handle, 1))
            {
                process.WaitForExit(1000);
                return !IsProcessRunning(process.Id);
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Failed to terminate process {ProcessId} with Win32 API", process.Id);
        }
        return false;
    }

    private bool IsProcessRunning(int processId)
    {
        try
        {
            var process = Process.GetProcessById(processId);
            var hasExited = process.HasExited;
            process.Dispose();
            return !hasExited;
        }
        catch (ArgumentException)
        {
            // Process not found, it has exited
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking if process {ProcessId} is running", processId);
            return false; // Assume not running on error
        }
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (_disposed)
            return;

        if (disposing && _ownsMutex)
        {
            try
            {
                _mutex.ReleaseMutex();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error releasing mutex");
            }
            _mutex.Dispose();
        }

        _disposed = true;
    }

    ~SingleInstanceManager()
    {
        Dispose(false);
    }
}
