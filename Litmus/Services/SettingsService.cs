using System;
using System.Diagnostics;
using System.IO;
using System.Text.Json;

namespace Litmus.Services;

public static class SettingsService
{
    private static readonly string SettingsPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "Litmus", "settings.json");

    private static AppSettings? _settings;

    public static AppSettings Settings
    {
        get
        {
            if (_settings == null)
            {
                Load();
            }
            return _settings!;
        }
    }

    public static void Load()
    {
        Debug.WriteLine($"[SettingsService] Loading settings from: {SettingsPath}");

        try
        {
            if (File.Exists(SettingsPath))
            {
                var json = File.ReadAllText(SettingsPath);
                _settings = JsonSerializer.Deserialize<AppSettings>(json) ?? new AppSettings();
                Debug.WriteLine("[SettingsService] Settings loaded successfully.");
            }
            else
            {
                _settings = new AppSettings();
                Debug.WriteLine("[SettingsService] No settings file found, using defaults.");
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[SettingsService] Error loading settings: {ex.Message}");
            _settings = new AppSettings();
        }
    }

    public static void Save()
    {
        Debug.WriteLine($"[SettingsService] Saving settings to: {SettingsPath}");

        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(SettingsPath)!);

            var options = new JsonSerializerOptions { WriteIndented = true };
            var json = JsonSerializer.Serialize(_settings, options);
            File.WriteAllText(SettingsPath, json);

            Debug.WriteLine("[SettingsService] Settings saved successfully.");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[SettingsService] Error saving settings: {ex.Message}");
        }
    }
}

public class AppSettings
{
    public bool LicenseAccepted { get; set; }
    public DateTime? LicenseAcceptedDate { get; set; }
}
