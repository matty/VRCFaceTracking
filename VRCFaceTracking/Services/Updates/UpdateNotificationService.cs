using Microsoft.Extensions.Logging;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using VRCFaceTracking.Controls;
using VRCFaceTracking.Contracts.Services;
using VRCFaceTracking.Core.Contracts.Services;
using VRCFaceTracking.Core.Models;
using VRCFaceTracking.Services.Updates;

namespace VRCFaceTracking.Services;

public class UpdateNotificationService
{
    private readonly IModuleUpdateService _moduleUpdateService;
    private readonly ILogger<UpdateNotificationService> _logger;
    private readonly IDispatcherService _dispatcherService;

    public UpdateNotificationService(
        IModuleUpdateService moduleUpdateService,
        IDispatcherService dispatcherService,
        ILogger<UpdateNotificationService> logger)
    {
        _moduleUpdateService = moduleUpdateService;
        _dispatcherService = dispatcherService;
        _logger = logger;

        // Subscribe to update events
        _moduleUpdateService.UpdatesAvailable += OnModuleUpdatesAvailable;
        _logger.LogInformation("UpdateNotificationService initialized and listening for updates");
    }

    private void OnModuleUpdatesAvailable(object? sender, ModuleUpdatesAvailableEventArgs e)
    {
        _logger.LogInformation("Received notification of {count} available module updates", e.AvailableUpdates.Count());

        // Use ContentDialog instead of TeachingTip as it's more reliable
        _dispatcherService.Run(() => ShowUpdateContentDialog(e.AvailableUpdates));
    }

    private async void ShowUpdateContentDialog(IEnumerable<InstallableTrackingModule> updates)
    {
        try
        {
            _logger.LogInformation("Showing ContentDialog for updates");

            // Ensure we have the XamlRoot
            var xamlRoot = App.MainWindow.Content?.XamlRoot;
            if (xamlRoot == null)
            {
                _logger.LogWarning("Cannot show dialog - XamlRoot is null");
                return;
            }

            var moduleNames = string.Join(", ", updates.Select(u => u.ModuleName));
            var dialog = new ContentDialog
            {
                Title = "Module Updates Available",
                Content = $"Updates are available for the following modules: {moduleNames}. Would you like to install them now?",
                PrimaryButtonText = "Update",
                SecondaryButtonText = "Later",
                XamlRoot = xamlRoot
            };

            var result = await dialog.ShowAsync();
            if (result == ContentDialogResult.Primary)
            {
                _logger.LogInformation("User chose to install updates");
                if (_moduleUpdateService is ModuleUpdateService updateService)
                {
                    await updateService.InstallUpdatesAsync(updates);
                }
            }
            else
            {
                _logger.LogInformation("User chose to skip updates for now");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error showing update content dialog");
        }
    }

    private async void OnUpdateActionSelected(object? sender, ModuleUpdateActionEventArgs e)
    {
        if (e.InstallUpdates)
        {
            _logger.LogInformation("User chose to install {count} module updates", e.Updates.Count());

            if (_moduleUpdateService is ModuleUpdateService updateService)
            {
                await updateService.InstallUpdatesAsync(e.Updates);
            }
        }
        else
        {
            _logger.LogInformation("User chose to skip module updates for now");
        }
    }
}
