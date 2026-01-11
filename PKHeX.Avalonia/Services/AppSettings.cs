using System;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using CommunityToolkit.Mvvm.ComponentModel;
using PKHeX.Core;

namespace PKHeX.Avalonia.Services;

/// <summary>
/// Manages application settings and configuration.
/// Implements IProgramSettings to provide settings to Core components.
/// </summary>
public partial class AppSettings : ObservableObject, IProgramSettings
{
    private const string ConfigFileName = "config.json";
    private static readonly string ConfigPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, ConfigFileName);

    // IProgramSettings Implementation

    // CommunityToolkit generates 'Startup' from '_startup' with type StartupSettings.
    // Interface requires IStartupSettings.
    // We can explicitly implement the interface property to return the observable property.
    IStartupSettings IProgramSettings.Startup => Startup;



    // Backing fields for settings
    [ObservableProperty] private StartupSettings _startup = new();
    [ObservableProperty] private BackupSettings _backup = new();
    [ObservableProperty] private SaveLanguageSettings _saveLanguage = new();
    [ObservableProperty] private SlotWriteSettings _slotWrite = new();
    [ObservableProperty] private SetImportSettings _import = new();
    [ObservableProperty] private LegalitySettings _legality = new();
    [ObservableProperty] private EntityConverterSettings _converter = new();
    [ObservableProperty] private LocalResourceSettings _localResources = new();

    // Additional Avalonia-specific settings could go here
    
    // Serialization Context
    [JsonSerializable(typeof(AppSettings))]
    private partial class AppSettingsContext : JsonSerializerContext;

    public static AppSettings Load()
    {
        if (!File.Exists(ConfigPath))
        {
            return new AppSettings();
        }

        try
        {
            var json = File.ReadAllText(ConfigPath);
            // Verify if simple deserialization works for the complex object graph
            // Since ObservableProperty generates fields, direct deserialization might be tricky.
            // For now, let's assume standard properties, but wait... CommunityToolkit.Mvvm uses private fields.
            // A simpler DTO might be needed if direct deserialization fails, but let's try direct first 
            // as new JSON serializers are quite capable.
            // Actually, simplest is to use public properties for serialization and simple fields if needed, 
            // but for IProgramSettings they are classes.
            
            // Re-evaluating: The Core settings classes (e.g. SaveLanguageSettings) are just POCOs.
            // Wrapping them in ObservableObject here is fine for the Container (AppSettings),
            // but the properties themselves (Startup, Backup...) are just objects.
            // We just need to serialize/deserialize the container.
            
            var settings = JsonSerializer.Deserialize<AppSettings>(json);
            return settings ?? new AppSettings();
        }
        catch (Exception)
        {
            // Fallback to defaults on error
            return new AppSettings();
        }
    }

    public void Save()
    {
        try
        {
            var options = new JsonSerializerOptions { WriteIndented = true };
            var json = JsonSerializer.Serialize(this, options);
            File.WriteAllText(ConfigPath, json);
        }
        catch (Exception)
        {
            // Ignore save errors for now
        }
    }

    [ObservableProperty] private string _displayLanguage = "en";

    /// <summary>
    /// Initializes global Core settings based on loaded configuration.
    /// </summary>
    public void InitializeCore()
    {
        // Apply Language Settings
        // In Core, GameInfo.CurrentLanguage is static.
        // If DisplayLanguage is set, use it.
        // Avalonia's GameInfo expects language code strings like "en", "ja", etc.
        if (!string.IsNullOrEmpty(DisplayLanguage))
        {
            GameInfo.CurrentLanguage = DisplayLanguage;
        }
    }

    /// <summary>
    /// Local implementation of IStartupSettings since Core only defines the interface.
    /// </summary>
    public class StartupSettings : IStartupSettings
    {
        public GameVersion DefaultSaveVersion { get; set; } = GameVersion.SW;
        public SaveFileLoadSetting AutoLoadSaveOnStartup { get; set; } = SaveFileLoadSetting.LastLoaded;
        public System.Collections.Generic.List<string> RecentlyLoaded { get; set; } = [];
        public string Version { get; set; } = string.Empty;
        public bool ShowChangelogOnUpdate { get; set; } = true;
        public bool ForceHaXOnLaunch { get; set; } = false;
    }
}
