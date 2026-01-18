using WifiGPSGate.Core.Models;

namespace WifiGPSGate.App.Services;

/// <summary>
/// Interface for loading and saving user settings.
/// </summary>
public interface ISettingsService
{
    /// <summary>
    /// Loads settings from persistent storage.
    /// Returns default settings if no saved settings exist.
    /// </summary>
    UserSettings Load();

    /// <summary>
    /// Saves settings to persistent storage.
    /// </summary>
    void Save(UserSettings settings);

    /// <summary>
    /// Gets the path to the settings file.
    /// </summary>
    string SettingsFilePath { get; }
}
