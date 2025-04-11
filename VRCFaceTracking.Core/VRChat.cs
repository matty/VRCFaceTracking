using System.Diagnostics;
using System.Runtime.Versioning;
using System.Security;
using Microsoft.Win32;

namespace VRCFaceTracking.Core;

public static class VRChat
{
    private const string VRChatProcessName = "VRChat";
    private const string VRChatRegistryPath = "Software\\VRChat\\VRChat";

    // Use GetVRCDataPath() method instead of accessing this directly
    private static readonly Lazy<string> _vrcDataPath = new(() =>
    {
        string? appDataLow = Environment.GetEnvironmentVariable("localappdata");
        return string.IsNullOrEmpty(appDataLow) ? string.Empty : Path.Combine($"{appDataLow}Low", "VRChat\\VRChat");
    });

    // Use GetVRCOSCDirectoryPath() method instead of accessing this directly
    private static readonly Lazy<string> _vrcOscDirectoryPath = new(() =>
    {
        var vrcDataPath = GetVRCDataPath();
        return string.IsNullOrEmpty(vrcDataPath) ? string.Empty : Path.Combine(vrcDataPath, "OSC");
    });

    /// <summary>
    /// Gets the VRChat data directory path.
    /// </summary>
    /// <returns>The VRChat data directory path, or empty string if it cannot be determined.</returns>
    public static string GetVRCDataPath() => _vrcDataPath.Value;

    /// <summary>
    /// Gets the VRChat OSC directory path.
    /// </summary>
    /// <returns>The VRChat OSC directory path, or empty string if it cannot be determined.</returns>
    public static string GetVRCOSCDirectoryPath() => _vrcOscDirectoryPath.Value;

    /// <summary>
    /// Gets the VRChat OSC directory path. 
    /// Property version for backward compatibility.
    /// </summary>
    public static string VRCOSCDirectory => GetVRCOSCDirectoryPath();


    /// <summary>
    /// Attempts to enable OSC in VRChat by setting relevant registry keys.
    /// </summary>
    /// <returns>True if OSC was forced to enable; False if OSC was already enabled or the operation failed.</returns>
    [SupportedOSPlatform("windows")]
    public static bool ForceEnableOsc()
    {
        try
        {
            using var regKey = Registry.CurrentUser.OpenSubKey(VRChatRegistryPath, true);
            if (regKey == null)
            {
                // Registry key doesn't exist, which means VRChat hasn't been run yet
                // or registry access is restricted
                return false;
            }

            var oscKeys = regKey.GetValueNames()
                .Where(x => x.StartsWith("VRC_INPUT_OSC", StringComparison.OrdinalIgnoreCase) ||
                            x.StartsWith("UI.Settings.Osc", StringComparison.OrdinalIgnoreCase))
                .ToList();

            if (!oscKeys.Any())
            {
                // No OSC keys found, can't force enable
                return false;
            }

            var wasOscForced = false;
            foreach (var key in oscKeys)
            {
                object? value = regKey.GetValue(key);
                if (value is int intValue && intValue == 0)
                {
                    // OSC is likely not enabled
                    regKey.SetValue(key, 1);
                    wasOscForced = true;
                }
            }

            return wasOscForced;
        }
        catch (SecurityException ex)
        {
            // Handle security exception (e.g., no permission to access registry)
            Debug.WriteLine($"Security exception when accessing registry: {ex.Message}");
            return false;
        }
        catch (UnauthorizedAccessException ex)
        {
            // Handle unauthorized access exception
            Debug.WriteLine($"Unauthorized access exception when modifying registry: {ex.Message}");
            return false;
        }
        catch (Exception ex)
        {
            // Handle other exceptions
            Debug.WriteLine($"Exception in ForceEnableOsc: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// Determines if VRChat is currently running.
    /// </summary>
    /// <returns>True if VRChat is running; otherwise, false.</returns>
    public static bool IsVrChatRunning()
    {
        try
        {
            var processes = Process.GetProcessesByName(VRChatProcessName);
            return processes.Length > 0;
        }
        catch (Exception ex)
        {
            // Handle exceptions that might occur during process enumeration
            Debug.WriteLine($"Exception checking if VRChat is running: {ex.Message}");
            return false;
        }
    }
}