using System;

namespace VRCFaceTracking.Contracts.Services;

/// <summary>
/// Interface for managing the application's single-instance behavior.
/// </summary>
public interface ISingleInstanceManager : IDisposable
{
    /// <summary>
    /// Checks if this is the first/only instance of the application.
    /// </summary>
    /// <returns>True if this is the first/only instance, False otherwise</returns>
    bool IsFirstInstance();

    /// <summary>
    /// Ensures this is the only instance of the application running by terminating other instances.
    /// </summary>
    /// <returns>True if this becomes the only instance, False if unable to terminate other instances</returns>
    bool EnsureSingleInstance();
}
