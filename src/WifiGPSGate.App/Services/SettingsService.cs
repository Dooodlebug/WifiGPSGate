using System.IO;
using System.Text.Json;
using Serilog;
using WifiGPSGate.Core.Models;

namespace WifiGPSGate.App.Services;

/// <summary>
/// JSON-based settings persistence implementation.
/// Stores settings in %LocalAppData%\WifiGPSGate\settings.json
/// </summary>
public sealed class SettingsService : ISettingsService
{
    private readonly ILogger _logger;
    private readonly string _settingsFilePath;
    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public string SettingsFilePath => _settingsFilePath;

    public SettingsService(ILogger? logger = null)
    {
        _logger = logger?.ForContext<SettingsService>() ?? Log.Logger.ForContext<SettingsService>();

        var appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var settingsDirectory = Path.Combine(appDataPath, "WifiGPSGate");
        _settingsFilePath = Path.Combine(settingsDirectory, "settings.json");
    }

    public UserSettings Load()
    {
        try
        {
            if (!File.Exists(_settingsFilePath))
            {
                _logger.Information("No settings file found, using defaults");
                return new UserSettings();
            }

            var json = File.ReadAllText(_settingsFilePath);
            var settings = JsonSerializer.Deserialize<UserSettings>(json, _jsonOptions);

            if (settings == null)
            {
                _logger.Warning("Failed to deserialize settings, using defaults");
                return new UserSettings();
            }

            _logger.Information("Settings loaded from {Path}", _settingsFilePath);
            return settings;
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error loading settings from {Path}", _settingsFilePath);
            return new UserSettings();
        }
    }

    public void Save(UserSettings settings)
    {
        try
        {
            var directory = Path.GetDirectoryName(_settingsFilePath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            var json = JsonSerializer.Serialize(settings, _jsonOptions);
            File.WriteAllText(_settingsFilePath, json);

            _logger.Information("Settings saved to {Path}", _settingsFilePath);
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error saving settings to {Path}", _settingsFilePath);
        }
    }
}
