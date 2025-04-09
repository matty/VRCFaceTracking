using Microsoft.Extensions.Logging;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
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

    private IEnumerable<InstallableTrackingModule>? _availableUpdates;
    private bool _isUpdating = false;
    private ContentDialog? _updateDialog;

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
        var updates = e.AvailableUpdates;
        if (updates == null || !updates.Any())
        {
            _logger.LogInformation("No updates available");
            return;
        }

        _logger.LogInformation("Received notification of {count} available module updates", updates.Count());
        _availableUpdates = updates;

        // Show update dialog on the UI thread
        _dispatcherService.Run(() => ShowUpdateDialog());
    }

    private void ShowUpdateDialog()
    {
        try
        {
            // Ensure we have the MainWindow
            var mainWindow = App.MainWindow;

            // Create and show the update dialog
            _updateDialog = new ContentDialog
            {
                Title = "Updates Available",
                Content = $"{_availableUpdates?.Count()} module updates are available. Do you want to install them now?",
                PrimaryButtonText = "Install",
                CloseButtonText = "Later",
                DefaultButton = ContentDialogButton.Primary,
                XamlRoot = mainWindow.Content.XamlRoot
            };

            // Handle the primary button click (Install)
            _updateDialog.PrimaryButtonClick += async (sender, args) =>
            {
                if (_isUpdating || _availableUpdates == null)
                    return;

                try
                {
                    _isUpdating = true;
                    _logger.LogInformation("User clicked to install {count} updates", _availableUpdates.Count());

                    // Change dialog to show installation progress
                    if (_updateDialog != null)
                    {
                        _updateDialog.Content = "Installing updates...";
                        _updateDialog.PrimaryButtonText = "";
                        _updateDialog.CloseButtonText = "";
                        _updateDialog.IsPrimaryButtonEnabled = false;
                        _updateDialog.IsSecondaryButtonEnabled = false;
                    }

                    // Install updates
                    if (_moduleUpdateService is ModuleUpdateService updateService)
                    {
                        await updateService.InstallUpdatesAsync(_availableUpdates);
                    }

                    // Update dialog to show completion
                    if (_updateDialog != null)
                    {
                        _updateDialog.Content = "Updates installed successfully.";
                        _updateDialog.CloseButtonText = "OK";
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error installing updates");

                    // Update dialog to show error
                    if (_updateDialog != null)
                    {
                        _updateDialog.Content = $"Error installing updates: {ex.Message}";
                        _updateDialog.CloseButtonText = "Close";
                    }
                }
                finally
                {
                    _isUpdating = false;
                    _availableUpdates = null;
                }
            };

            // Show the dialog asynchronously
            _ = _updateDialog.ShowAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error showing update dialog: {message}", ex.Message);
        }
    }
}
