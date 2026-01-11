using System;
using System.Collections.Generic;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PKHeX.Avalonia.Services;
using PKHeX.Core;

namespace PKHeX.Avalonia.ViewModels;

public partial class SettingsViewModel : ViewModelBase
{
    private readonly AppSettings _settings;

    public SettingsViewModel(AppSettings settings)
    {
        _settings = settings;

        // General
        _displayLanguage = _settings.DisplayLanguage;
        
        // Backup
        _backupEnabled = _settings.Backup.BAKEnabled;

        // Slot Write
        _updateDex = _settings.SlotWrite.SetUpdateDex;
        _updatePKM = _settings.SlotWrite.SetUpdatePKM;
    }

    [ObservableProperty]
    private string _displayLanguage;

    [ObservableProperty]
    private bool _backupEnabled;

    [ObservableProperty]
    private bool _updateDex;

    [ObservableProperty]
    private bool _updatePKM;

    public IReadOnlyList<string> LanguageOptions { get; } = ["en", "ja", "fr", "it", "de", "es", "ko", "zh"];

    [RelayCommand]
    private void Save()
    {
        _settings.DisplayLanguage = DisplayLanguage;
        _settings.Backup.BAKEnabled = BackupEnabled;
        _settings.SlotWrite.SetUpdateDex = UpdateDex;
        _settings.SlotWrite.SetUpdatePKM = UpdatePKM;

        _settings.Save();
        _settings.InitializeCore();
    }
}
