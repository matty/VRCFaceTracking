using Microsoft.Extensions.Logging;
using Microsoft.UI.Xaml;
using VRCFaceTracking.Contracts.Services;
using VRCFaceTracking.Core.Contracts.Services;
using VRCFaceTracking.Core.Models;
using VRCFaceTracking.Core.Services;

namespace VRCFaceTracking.Services.Updates;

public class ModuleUpdateService : IModuleUpdateService
{
    private readonly IModuleDataService _moduleDataService;
    private readonly ModuleInstaller _moduleInstaller;
    private readonly IDispatcherService _dispatcherService;
    private readonly ILogger<ModuleUpdateService> _logger;

    public event EventHandler<ModuleUpdatesAvailableEventArgs>? UpdatesAvailable;

    public ModuleUpdateService(
        IModuleDataService moduleDataService,
        ModuleInstaller moduleInstaller,
        IDispatcherService dispatcherService,
        ILogger<ModuleUpdateService> logger)
    {
        _moduleDataService = moduleDataService;
        _moduleInstaller = moduleInstaller;
        _dispatcherService = dispatcherService;
        _logger = logger;
    }

    public async Task CheckForUpdatesAsync()
    {
        try
        {
            _logger.LogDebug("Starting asynchronous check for module updates");

            // Get local and remote modules
            var localModules = _moduleDataService.GetInstalledModules().Where(m => m.ModuleId != Guid.Empty).ToList();
            var remoteModules = await _moduleDataService.GetRemoteModules();

            // Find modules that need updates
            var outdatedModules = remoteModules.Where(rm => localModules.Any(lm =>
            {
                if (rm.ModuleId != lm.ModuleId)
                    return false;

                var remoteVersion = new Version(rm.Version);
                var localVersion = new Version(lm.Version);

                return remoteVersion.CompareTo(localVersion) > 0;
            })).ToList();

            if (outdatedModules.Any())
            {
                _logger.LogInformation("Found {count} modules with available updates", outdatedModules.Count);

                // Run on UI thread since this might trigger UI updates
                _dispatcherService.Run(() =>
                {
                    OnUpdatesAvailable(new ModuleUpdatesAvailableEventArgs(outdatedModules));
                });
            }
            else
            {
                _logger.LogInformation("All modules are up to date");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking for module updates");
        }
    }

    public async Task InstallUpdatesAsync(IEnumerable<InstallableTrackingModule> updatesToInstall)
    {
        try
        {
            foreach (var module in updatesToInstall)
            {
                _logger.LogInformation("Updating module: {name} to version {version}", module.ModuleName, module.Version);
                await _moduleInstaller.InstallRemoteModule(module);
            }

            _logger.LogInformation("All selected updates have been installed");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error installing module updates");
        }
    }

    // Method to simulate updates available (used for debugging)
    public void SimulateUpdatesAvailable(IEnumerable<InstallableTrackingModule> modules)
    {
        _logger.LogInformation("Simulating {count} module updates available", modules.Count());

        // Make sure we're on the UI thread when raising this event
        _dispatcherService.Run(() =>
        {
            OnUpdatesAvailable(new ModuleUpdatesAvailableEventArgs(modules));
        });
    }

    // Helper method to raise the UpdatesAvailable event
    protected virtual void OnUpdatesAvailable(ModuleUpdatesAvailableEventArgs e)
    {
        UpdatesAvailable?.Invoke(this, e);
    }
}
