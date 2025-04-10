using Microsoft.Extensions.Options;
using VRCFaceTracking.Core.Contracts.Services;
using VRCFaceTracking.Core.Helpers;
using VRCFaceTracking.Helpers;
using VRCFaceTracking.Models;
using Windows.Storage;

namespace VRCFaceTracking.Services;

public class LocalSettingsService : ILocalSettingsService
{
    private const string _defaultApplicationDataFolder = "VRCFaceTracking/ApplicationData";
    private const string _defaultLocalSettingsFile = "LocalSettings.json";

    private readonly IFileService _fileService;
    private readonly LocalSettingsOptions _options;

    private readonly string _localApplicationData = Core.Utils.PersistentDataDirectory;
    private readonly string _applicationDataFolder;
    private readonly string _localSettingsFile;

    private IDictionary<string, object> _settings;

    private static readonly SemaphoreSlim _settingsLock = new SemaphoreSlim(1, 1);
    private bool _isInitialized;

    public LocalSettingsService(IFileService fileService, IOptions<LocalSettingsOptions> options)
    {
        _fileService = fileService;
        _options = options.Value;

        _applicationDataFolder = Path.Combine(_localApplicationData, _options.ApplicationDataFolder ?? _defaultApplicationDataFolder);
        _localSettingsFile = _options.LocalSettingsFile ?? _defaultLocalSettingsFile;

        _settings = new Dictionary<string, object>();
    }

    private async Task InitializeAsync()
    {
        if (_isInitialized)
            return;

        await _settingsLock.WaitAsync();
        try
        {
            if (!_isInitialized) // Double-check after acquiring the lock
            {
                // FileService.Read has its own locking for file access
                _settings = await Task.Run(() =>
                               _fileService.Read<IDictionary<string, object>>(_applicationDataFolder, _localSettingsFile))
                           ?? new Dictionary<string, object>();

                _isInitialized = true;
            }
        }
        catch (Exception)
        {
            // In case of errors, use an empty dictionary
            _settings = new Dictionary<string, object>();
            _isInitialized = true;
        }
        finally
        {
            _settingsLock.Release();
        }
    }

    public async Task<T?> ReadSettingAsync<T>(string key, T? defaultValue = default, bool forceLocal = false)
    {
        if (RuntimeHelper.IsMSIX && !forceLocal)
        {
            if (ApplicationData.Current.LocalSettings.Values.TryGetValue(key, out var obj))
            {
                try
                {
                    return await Json.ToObjectAsync<T>((string)obj);
                }
                catch
                {
                    return defaultValue;
                }
            }
        }
        else
        {
            await InitializeAsync();

            // We still need to lock access to the in-memory dictionary
            await _settingsLock.WaitAsync();
            try
            {
                if (_settings != null && _settings.TryGetValue(key, out var obj))
                {
                    try
                    {
                        return await Json.ToObjectAsync<T>((string)obj);
                    }
                    catch
                    {
                        return defaultValue;
                    }
                }
            }
            finally
            {
                _settingsLock.Release();
            }
        }

        return defaultValue;
    }

    public async Task SaveSettingAsync<T>(string key, T value, bool forceLocal = false)
    {
        if (RuntimeHelper.IsMSIX && !forceLocal)
        {
            ApplicationData.Current.LocalSettings.Values[key] = await Json.StringifyAsync(value);
        }
        else
        {
            await InitializeAsync();

            string stringifiedValue = await Json.StringifyAsync(value);

            await _settingsLock.WaitAsync();
            try
            {
                // We need to lock here because we're modifying the in-memory dictionary
                // and then saving it all at once
                _settings[key] = stringifiedValue;

                // FileService.Save has its own locking for file access
                await _fileService.Save(_applicationDataFolder, _localSettingsFile, _settings);
            }
            finally
            {
                _settingsLock.Release();
            }
        }
    }

    public async Task Load(object instance)
    {
        var type = instance.GetType();
        var properties = type.GetProperties();

        foreach (var property in properties)
        {
            var attributes = property.GetCustomAttributes(typeof(SavedSettingAttribute), false);

            if (attributes.Length <= 0)
            {
                continue;
            }

            var savedSettingAttribute = (SavedSettingAttribute)attributes[0];
            var settingName = savedSettingAttribute.GetName();
            var defaultValue = savedSettingAttribute.Default();

            var setting = await ReadSettingAsync(settingName, defaultValue, savedSettingAttribute.ForceLocal());
            try
            {
                var convertedSetting = Convert.ChangeType(setting, property.PropertyType);
                property.SetValue(instance, convertedSetting);
            }
            catch
            {
                property.SetValue(instance, defaultValue);
            }
        }
    }

    public async Task Save(object instance)
    {
        var type = instance.GetType();
        var properties = type.GetProperties();

        var tasks = new List<Task>();

        foreach (var property in properties)
        {
            var attributes = property.GetCustomAttributes(typeof(SavedSettingAttribute), false);

            if (attributes.Length <= 0)
            {
                continue;
            }

            var savedSettingAttribute = (SavedSettingAttribute)attributes[0];
            var settingName = savedSettingAttribute.GetName();

            tasks.Add(SaveSettingAsync(settingName, property.GetValue(instance), savedSettingAttribute.ForceLocal()));
        }

        await Task.WhenAll(tasks);
    }
}
