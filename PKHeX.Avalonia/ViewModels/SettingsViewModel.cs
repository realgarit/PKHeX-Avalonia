using System;
using System.Collections.Generic;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PKHeX.Avalonia.Services;
using PKHeX.Core;

namespace PKHeX.Avalonia.ViewModels;

public partial class SettingsViewModel : ViewModelBase
{
    private readonly AppSettings _settings;

    public Action? CloseRequested { get; set; }

    public SettingsViewModel(AppSettings settings)
    {
        _settings = settings;
        Load();
    }

    [ObservableProperty] private string _displayLanguage = "en";
    public IReadOnlyList<string> LanguageOptions { get; } = ["en", "ja", "fr", "it", "de", "es", "ko", "zh"];

    // Startup
    [ObservableProperty] private GameVersion _defaultSaveVersion;
    public IReadOnlyList<GameVersion> GameVersions { get; } = Enum.GetValues<GameVersion>();
    
    [ObservableProperty] private SaveFileLoadSetting _autoLoadMode;
    public IReadOnlyList<SaveFileLoadSetting> LoadModes { get; } = Enum.GetValues<SaveFileLoadSetting>();

    [ObservableProperty] private bool _forceHaX;
    [ObservableProperty] private bool _showChangelog;

    // Backup
    [ObservableProperty] private bool _bakEnabled;
    [ObservableProperty] private bool _bakPrompt;

    // SlotWrite
    [ObservableProperty] private bool _setUpdateDex;
    [ObservableProperty] private bool _setUpdatePKM;
    [ObservableProperty] private bool _setUpdateRecords;
    [ObservableProperty] private bool _modifyUnset;

    // Privacy
    [ObservableProperty] private bool _hideSAVDetails;
    [ObservableProperty] private bool _hideSecretDetails;

    // Legality
    [ObservableProperty] private bool _wordFilterCheck; // Attempting to map WordFilter.Check

    private void Load()
    {
        // General
        DisplayLanguage = _settings.DisplayLanguage;

        // Startup
        DefaultSaveVersion = _settings.Startup.DefaultSaveVersion;
        AutoLoadMode = _settings.Startup.AutoLoadSaveOnStartup;
        ForceHaX = _settings.Startup.ForceHaXOnLaunch;
        ShowChangelog = _settings.Startup.ShowChangelogOnUpdate;

        // Backup
        BakEnabled = _settings.Backup.BAKEnabled;
        BakPrompt = _settings.Backup.BAKPrompt;

        // Slot Write
        SetUpdateDex = _settings.SlotWrite.SetUpdateDex;
        SetUpdatePKM = _settings.SlotWrite.SetUpdatePKM;
        SetUpdateRecords = _settings.SlotWrite.SetUpdateRecords;
        ModifyUnset = _settings.SlotWrite.ModifyUnset;

        // Privacy
        // HideSAVDetails = _settings.Privacy.HideSAVDetails;
        // HideSecretDetails = _settings.Privacy.HideSecretDetails;

        // Legality
        // WordFilterCheck = _settings.Legality.WordFilter.Check;
    }

    [RelayCommand]
    private void Save()
    {
        // General
        _settings.DisplayLanguage = DisplayLanguage;

        // Startup
        _settings.Startup.DefaultSaveVersion = DefaultSaveVersion;
        _settings.Startup.AutoLoadSaveOnStartup = AutoLoadMode;
        _settings.Startup.ForceHaXOnLaunch = ForceHaX;
        _settings.Startup.ShowChangelogOnUpdate = ShowChangelog;

        // Backup
        _settings.Backup.BAKEnabled = BakEnabled;
        _settings.Backup.BAKPrompt = BakPrompt;

        // Slot Write
        _settings.SlotWrite.SetUpdateDex = SetUpdateDex;
        _settings.SlotWrite.SetUpdatePKM = SetUpdatePKM;
        _settings.SlotWrite.SetUpdateRecords = SetUpdateRecords;
        _settings.SlotWrite.ModifyUnset = ModifyUnset;

        // Privacy
        // _settings.Privacy.HideSAVDetails = HideSAVDetails;
        // _settings.Privacy.HideSecretDetails = HideSecretDetails;

        // Legality
        // _settings.Legality.WordFilter.Check = WordFilterCheck;

        _settings.Save();
        _settings.InitializeCore();
        
        CloseRequested?.Invoke();
    }
}
