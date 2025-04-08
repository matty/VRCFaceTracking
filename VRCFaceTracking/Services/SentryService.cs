using Microsoft.Extensions.Logging;
using Sentry;
using VRCFaceTracking.Core.Contracts.Services;

namespace VRCFaceTracking.Services;

public class SentryService
{
    private readonly ILocalSettingsService _localSettingsService;
    private readonly ILogger<SentryService> _logger;
    private bool _isSentryInitialized = false;
    private const string SentryEnabledKey = "SentryEnabled";
    private const string SentryDsn = "https://444b0799dd2b670efa85d866c8c12134@o4506152235237376.ingest.us.sentry.io/4506152246575104";

    public SentryService(ILocalSettingsService localSettingsService, ILogger<SentryService> logger)
    {
        _localSettingsService = localSettingsService;
        _logger = logger;
    }

    public async Task InitializeAsync()
    {
        // Default to enabled if setting doesn't exist
        var isSentryEnabled = await _localSettingsService.ReadSettingAsync<bool>(SentryEnabledKey, true);

        if (isSentryEnabled)
        {
            InitializeSentry();
        }
        else
        {
            _logger.LogInformation("Sentry disabled by user preference");
        }
    }

    public async Task<bool> GetSentryEnabledAsync()
    {
        return await _localSettingsService.ReadSettingAsync<bool>(SentryEnabledKey, true);
    }

    public async Task SetSentryEnabledAsync(bool enabled)
    {
        await _localSettingsService.SaveSettingAsync(SentryEnabledKey, enabled);

        if (enabled && !_isSentryInitialized)
        {
            InitializeSentry();
        }
        else if (!enabled && _isSentryInitialized)
        {
            // Close and dispose of current Sentry SDK
            await SentrySdk.FlushAsync(TimeSpan.FromSeconds(2));
            SentrySdk.Close();
            _isSentryInitialized = false;
            _logger.LogInformation("Sentry has been disabled");
        }
    }

    private void InitializeSentry()
    {
        if (_isSentryInitialized)
            return;

        SentrySdk.Init(o =>
        {
            o.Dsn = SentryDsn;
            o.TracesSampleRate = 1.0;
            o.IsGlobalModeEnabled = true;
            o.EnableTracing = true;
        });

        _isSentryInitialized = true;
        _logger.LogInformation("Sentry has been initialized");
    }
}
