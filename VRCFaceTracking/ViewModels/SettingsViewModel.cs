using System.Windows.Input;

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

using Microsoft.UI.Xaml;

using VRCFaceTracking.Contracts.Services;
using VRCFaceTracking.Models;
using VRCFaceTracking.Services;

namespace VRCFaceTracking.ViewModels;

public partial class SettingsViewModel : ObservableRecipient
{
    private readonly IThemeSelectorService _themeSelectorService;
    private readonly SentryService _sentryService;
    [ObservableProperty] private ElementTheme _elementTheme;
    [ObservableProperty] private List<GithubContributor> _contributors;
    [ObservableProperty] private bool _isSentryEnabled;

    public ICommand SwitchThemeCommand
    {
        get;
    }

    public ICommand ToggleSentryCommand
    {
        get;
    }

    private GithubService GithubService
    {
        get;
        set;
    }

    private async void LoadContributors()
    {
        Contributors = await GithubService.GetContributors("benaclejames/VRCFaceTracking");
    }

    private async void LoadSentrySettings()
    {
        IsSentryEnabled = await _sentryService.GetSentryEnabledAsync();
    }

    public SettingsViewModel(IThemeSelectorService themeSelectorService, GithubService githubService, SentryService sentryService)
    {
        _themeSelectorService = themeSelectorService;
        _sentryService = sentryService;
        GithubService = githubService;

        _elementTheme = _themeSelectorService.Theme;

        SwitchThemeCommand = new RelayCommand<ElementTheme>(
            async (param) =>
            {
                if (ElementTheme != param)
                {
                    ElementTheme = param;
                    await _themeSelectorService.SetThemeAsync(param);
                }
            });

        ToggleSentryCommand = new RelayCommand<bool>(
            async (enabled) =>
            {
                await _sentryService.SetSentryEnabledAsync(IsSentryEnabled);
            });

        LoadContributors();
        LoadSentrySettings();
    }
}
