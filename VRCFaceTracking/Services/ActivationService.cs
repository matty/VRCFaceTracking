﻿using System.Reflection;
using Microsoft.Extensions.Logging;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

using VRCFaceTracking.Activation;
using VRCFaceTracking.Contracts.Services;
using VRCFaceTracking.Core.Contracts.Services;
using VRCFaceTracking.Core.Models;
using VRCFaceTracking.Core.OSC;
using VRCFaceTracking.Core.Services;
using VRCFaceTracking.Services.Updates;
using VRCFaceTracking.Views;

namespace VRCFaceTracking.Services;

public class ActivationService : IActivationService
{
    private readonly ActivationHandler<LaunchActivatedEventArgs> _defaultHandler;
    private readonly IEnumerable<IActivationHandler> _activationHandlers;
    private readonly IThemeSelectorService _themeSelectorService;
    private readonly OscQueryService _parameterOutputService;
    private readonly IMainService _mainService;
    private readonly IModuleDataService _moduleDataService;
    private readonly ModuleInstaller _moduleInstaller;
    private readonly ILibManager _libManager;
    private readonly IModuleUpdateService _moduleUpdateService;
    private readonly ILogger<ActivationService> _logger;
    private UIElement? _shell;

    public ActivationService(
        ActivationHandler<LaunchActivatedEventArgs> defaultHandler,
        IEnumerable<IActivationHandler> activationHandlers,
        IThemeSelectorService themeSelectorService,
        OscQueryService parameterOutputService,
        IMainService mainService,
        IModuleDataService moduleDataService,
        ModuleInstaller moduleInstaller,
        ILibManager libManager,
        IModuleUpdateService moduleUpdateService,
        ILogger<ActivationService> logger)
    {
        _defaultHandler = defaultHandler;
        _activationHandlers = activationHandlers;
        _themeSelectorService = themeSelectorService;
        _parameterOutputService = parameterOutputService;
        _mainService = mainService;
        _moduleDataService = moduleDataService;
        _moduleInstaller = moduleInstaller;
        _libManager = libManager;
        _moduleUpdateService = moduleUpdateService;
        _logger = logger;
    }

    public async Task ActivateAsync(object activationArgs)
    {
        // Execute tasks before activation.
        await InitializeAsync();

        // Set the MainWindow Content.
        if (App.MainWindow.Content == null)
        {
            _shell = App.GetService<ShellPage>();
            App.MainWindow.Content = _shell ?? new Frame();
        }

        // Handle activation via ActivationHandlers.
        await HandleActivationAsync(activationArgs);

        // Activate the MainWindow.
        App.MainWindow.Activate();

        // Execute tasks after activation.
        await StartupAsync();
    }

    private async Task HandleActivationAsync(object activationArgs)
    {
        var activationHandler = _activationHandlers.FirstOrDefault(h => h.CanHandle(activationArgs));

        if (activationHandler != null)
        {
            await activationHandler.HandleAsync(activationArgs);
        }

        if (_defaultHandler.CanHandle(activationArgs))
        {
            await _defaultHandler.HandleAsync(activationArgs);
        }
    }

    private async Task InitializeAsync()
    {
        await _themeSelectorService.InitializeAsync().ConfigureAwait(false);

        await Task.CompletedTask;
    }

    private async Task StartupAsync()
    {
        await _themeSelectorService.SetRequestedThemeAsync();

        _logger.LogInformation("VRCFT Version {version} initializing...", Assembly.GetExecutingAssembly().GetName().Version);

        _logger.LogInformation("Initializing OSC...");
        await _parameterOutputService.InitializeAsync().ConfigureAwait(false);

        _logger.LogInformation("Initializing main service...");
        await _mainService.InitializeAsync().ConfigureAwait(false);

        // Before we initialize, we need to delete pending restart modules and check for updates for all our installed modules
        _logger.LogDebug("Checking for deletion requests for installed modules...");
        var needsDeleting = _moduleDataService.GetInstalledModules().Concat(_moduleDataService.GetLegacyModules())
            .Where(m => m.InstallationState == InstallState.AwaitingRestart);
        foreach (var deleteModule in needsDeleting)
        {
            _moduleInstaller.UninstallModule(deleteModule);
        }

        // Start module update check in the background, don't wait for it
        _logger.LogInformation("Starting background check for module updates...");
        _moduleUpdateService.CheckForUpdatesAsync();

        _logger.LogInformation("Initializing modules...");
        App.MainWindow.DispatcherQueue.TryEnqueue(() => _libManager.Initialize());

        await Task.CompletedTask;
    }
}
