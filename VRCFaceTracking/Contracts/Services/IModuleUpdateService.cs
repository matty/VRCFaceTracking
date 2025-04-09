namespace VRCFaceTracking.Contracts.Services;

public interface IModuleUpdateService
{
    /// <summary>
    /// Checks for module updates in the background without blocking the UI
    /// </summary>
    /// <returns>A Task representing the background operation</returns>
    Task CheckForUpdatesAsync();

    /// <summary>
    /// Event raised when updates are available
    /// </summary>
    event EventHandler<ModuleUpdatesAvailableEventArgs>? UpdatesAvailable;
}

public class ModuleUpdatesAvailableEventArgs : EventArgs
{
    public IEnumerable<Core.Models.InstallableTrackingModule> AvailableUpdates
    {
        get;
    }

    public ModuleUpdatesAvailableEventArgs(IEnumerable<Core.Models.InstallableTrackingModule> availableUpdates)
    {
        AvailableUpdates = availableUpdates;
    }
}
