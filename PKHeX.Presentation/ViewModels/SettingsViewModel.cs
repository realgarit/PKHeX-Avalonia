using System;
using System.Collections.Generic;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PKHeX.Application.Abstractions;
using PKHeX.Application.Services;
using PKHeX.Core;
using PKHeX.Presentation.Localization;

namespace PKHeX.Presentation.ViewModels;

public partial class SettingsViewModel : ViewModelBase, ICloseableDialog
{
    private readonly AppSettings _settings;
    private readonly ISettingsStore _settingsStore;
    private readonly IThemeService _themeService;
    private readonly IUiDensityService _uiDensityService;
    private readonly LanguageService _languageService;
    private readonly UpdateCheckCoordinator _updateCoordinator;
    private readonly ILinuxDesktopIntegrationService? _linuxDesktopIntegration;
    private readonly Func<Task>? _openKeyboardShortcuts;
    private bool _isLoading;

    public Action? CloseRequested { get; set; }

    /// <summary>Exposed so the settings screen can host a UI-language selector that switches live.</summary>
    public LanguageService LanguageService => _languageService;

    public SettingsViewModel(
        AppSettings settings,
        ISettingsStore settingsStore,
        IThemeService themeService,
        IUiDensityService uiDensityService,
        LanguageService languageService,
        UpdateCheckCoordinator updateCoordinator,
        ILinuxDesktopIntegrationService? linuxDesktopIntegration = null,
        Func<Task>? openKeyboardShortcuts = null,
        bool? isLinux = null)
    {
        _settings = settings;
        _settingsStore = settingsStore;
        _themeService = themeService;
        _uiDensityService = uiDensityService;
        _languageService = languageService;
        _updateCoordinator = updateCoordinator;
        _linuxDesktopIntegration = linuxDesktopIntegration;
        _openKeyboardShortcuts = openKeyboardShortcuts;
        ShowLinuxDisplay = isLinux ?? OperatingSystem.IsLinux();
        Load();
        RefreshLinuxIntegrationState();
    }

    /// <summary>Status line for the manual "Check for Updates" button; empty until a check runs.</summary>
    [ObservableProperty] private string _updateCheckStatus = string.Empty;

    /// <summary>Disables the button and shows the "checking…" state while a manual check is in flight.</summary>
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(CheckForUpdatesCommand))]
    private bool _isCheckingForUpdates;

    private bool CanCheckForUpdates => !IsCheckingForUpdates;

    [RelayCommand(CanExecute = nameof(CanCheckForUpdates))]
    private async Task CheckForUpdatesAsync()
    {
        IsCheckingForUpdates = true;
        UpdateCheckStatus = LocalizedStrings.Instance["Update_Checking"];
        try
        {
            var result = await _updateCoordinator.CheckNowAsync();
            UpdateCheckStatus = result.Message;
        }
        finally
        {
            IsCheckingForUpdates = false;
        }
    }

    // ---------------------------------------------------------------------------------------------
    // Keyboard shortcuts (the section is hidden when no editor was provided)
    // ---------------------------------------------------------------------------------------------

    /// <summary>Whether the keyboard shortcut section is shown: the host supplied the editor.</summary>
    public bool CanOpenKeyboardShortcuts => _openKeyboardShortcuts is not null;

    /// <summary>
    /// Opens the shortcut editor over this dialog. Shortcuts are saved by the editor itself, so the
    /// staged settings on this screen are neither saved nor discarded by it.
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanOpenKeyboardShortcuts))]
    private Task OpenKeyboardShortcutsAsync() => _openKeyboardShortcuts?.Invoke() ?? Task.CompletedTask;

    // ---------------------------------------------------------------------------------------------
    // Linux display (Linux only)
    // ---------------------------------------------------------------------------------------------

    /// <summary>Whether the Linux display section is shown: only Linux has a choice of backend.</summary>
    public bool ShowLinuxDisplay { get; }

    /// <summary>Opt-in native Wayland; the host reads the saved value on the next start.</summary>
    [ObservableProperty] private bool _useNativeWayland;

    // ---------------------------------------------------------------------------------------------
    // Linux desktop integration (AppImage only — the section is hidden everywhere else)
    // ---------------------------------------------------------------------------------------------

    /// <summary>Whether the Linux desktop-integration section should be shown at all.</summary>
    public bool ShowLinuxIntegration => _linuxDesktopIntegration?.IsSupported == true;

    /// <summary>Disables the buttons while a registration/removal is in flight.</summary>
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(AddToApplicationMenuCommand))]
    [NotifyCanExecuteChangedFor(nameof(RemoveFromApplicationMenuCommand))]
    private bool _isLinuxIntegrationBusy;

    /// <summary>Whether the app is currently present in the user's application menu.</summary>
    [ObservableProperty] private bool _linuxIntegrationRegistered;

    /// <summary>Status line under the integration buttons (installed path, progress, or error).</summary>
    [ObservableProperty] private string _linuxIntegrationStatus = string.Empty;

    private bool CanRunLinuxIntegration => !IsLinuxIntegrationBusy;

    [RelayCommand(CanExecute = nameof(CanRunLinuxIntegration))]
    private async Task AddToApplicationMenuAsync()
    {
        if (_linuxDesktopIntegration is not { } integration)
            return;

        IsLinuxIntegrationBusy = true;
        LinuxIntegrationStatus = LocalizedStrings.Instance["Settings_LinuxIntegration_Working"];
        try
        {
            var result = await integration.RegisterAsync();
            LinuxIntegrationStatus = result.Outcome == DesktopIntegrationOutcome.Success
                ? LocalizedStrings.Instance.Format("Settings_LinuxIntegration_InstalledAt", ShortenHomePath(result.InstalledPath ?? string.Empty))
                : LocalizedStrings.Instance[result.MessageKey ?? "LinuxIntegration_Error_Generic"];
        }
        finally
        {
            IsLinuxIntegrationBusy = false;
            RefreshLinuxIntegrationState();
        }
    }

    [RelayCommand(CanExecute = nameof(CanRunLinuxIntegration))]
    private async Task RemoveFromApplicationMenuAsync()
    {
        if (_linuxDesktopIntegration is not { } integration)
            return;

        IsLinuxIntegrationBusy = true;
        LinuxIntegrationStatus = LocalizedStrings.Instance["Settings_LinuxIntegration_Working"];
        try
        {
            var result = await integration.UnregisterAsync();
            LinuxIntegrationStatus = result.Outcome == DesktopIntegrationOutcome.Success
                ? LocalizedStrings.Instance["Settings_LinuxIntegration_Removed"]
                : LocalizedStrings.Instance[result.MessageKey ?? "LinuxIntegration_Error_Generic"];
        }
        finally
        {
            IsLinuxIntegrationBusy = false;
            RefreshLinuxIntegrationState();
        }
    }

    private void RefreshLinuxIntegrationState()
    {
        if (_linuxDesktopIntegration is not { } integration)
            return;

        LinuxIntegrationRegistered = integration.IsRegistered;
        if (string.IsNullOrEmpty(LinuxIntegrationStatus))
        {
            LinuxIntegrationStatus = integration.InstalledAppImagePath is { } installed
                ? LocalizedStrings.Instance.Format("Settings_LinuxIntegration_InstalledAt", ShortenHomePath(installed))
                : LocalizedStrings.Instance["Settings_LinuxIntegration_NotInstalled"];
        }
    }

    /// <summary>
    /// Renders an absolute path under the user's home directory as <c>~/…</c> for display;
    /// paths outside the home directory are shown verbatim.
    /// </summary>
    internal static string ShortenHomePath(string path)
    {
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        if (string.IsNullOrEmpty(home))
            return path;
        home = Path.TrimEndingDirectorySeparator(home);
        if (path == home)
            return "~";
        if (!path.StartsWith(home + Path.DirectorySeparatorChar, StringComparison.Ordinal))
            return path;
        return "~/" + path[(home.Length + 1)..].Replace(Path.DirectorySeparatorChar, '/');
    }

    // Startup
    [ObservableProperty] private GameVersion _defaultSaveVersion;
    public IReadOnlyList<GameVersion> GameVersions { get; } = Enum.GetValues<GameVersion>();
    [ObservableProperty] private SaveFileLoadSetting _autoLoadMode;
    public IReadOnlyList<SaveFileLoadSetting> LoadModes { get; } = Enum.GetValues<SaveFileLoadSetting>();

    [ObservableProperty] private bool _forceHaX;
    [ObservableProperty] private bool _showChangelog;
    [ObservableProperty] private bool _checkForUpdatesOnStartup;

    // Backup
    [ObservableProperty] private bool _bakEnabled;
    [ObservableProperty] private bool _bakPrompt;

    // SlotWrite
    [ObservableProperty] private bool _setUpdateDex;
    [ObservableProperty] private bool _setUpdatePKM;
    [ObservableProperty] private bool _setUpdateRecords;
    [ObservableProperty] private bool _modifyUnset;
    [ObservableProperty] private bool _warnClosingModified;
    [ObservableProperty] private bool _allowBattleTowerTeamSwap;

    // Sprites
    [ObservableProperty] private SpritePreference _spritePreference;
    public IReadOnlyList<SpritePreference> SpritePreferences { get; } = Enum.GetValues<SpritePreference>();

    // Appearance
    [ObservableProperty] private AppTheme _selectedTheme;
    // Keep the picker intentionally small. HighContrast/System remain readable legacy enum values
    // for old settings files, but are not product-facing appearance choices anymore.
    public IReadOnlyList<AppTheme> Themes { get; } = [AppTheme.Dark, AppTheme.Light];

    [ObservableProperty] private AppDensity _selectedDensity;
    public IReadOnlyList<AppDensity> Densities { get; } = Enum.GetValues<AppDensity>();

    partial void OnSelectedThemeChanged(AppTheme value)
    {
        // Apply (and persist) immediately so the picker previews live, without needing Save.
        // Skip during Load(), which sets the initial value from the already-applied preference.
        if (!_isLoading)
            _themeService.ApplyTheme(value);
    }

    partial void OnSelectedDensityChanged(AppDensity value)
    {
        // Apply (and persist) immediately so the picker previews live, matching the theme
        // preference. Skip during Load(), which reads the already-applied startup value.
        if (!_isLoading)
            _uiDensityService.ApplyDensity(value);
    }

    private void Load()
    {
        _isLoading = true;

        DefaultSaveVersion = _settings.Startup.DefaultSaveVersion;
        AutoLoadMode = _settings.Startup.AutoLoadSaveOnStartup;
        ForceHaX = _settings.Startup.ForceHaXOnLaunch;
        ShowChangelog = _settings.Startup.ShowChangelogOnUpdate;
        CheckForUpdatesOnStartup = _settings.Startup.CheckForUpdatesOnStartup;

        BakEnabled = _settings.Backup.BAKEnabled;
        BakPrompt = _settings.Backup.BAKPrompt;

        SetUpdateDex = _settings.SlotWrite.SetUpdateDex;
        SetUpdatePKM = _settings.SlotWrite.SetUpdatePKM;
        SetUpdateRecords = _settings.SlotWrite.SetUpdateRecords;
        ModifyUnset = _settings.SlotWrite.ModifyUnset;
        WarnClosingModified = _settings.EditorBehavior.WarnClosingModified;
        AllowBattleTowerTeamSwap = _settings.Legality.Game.Gen3.AllowBattleTowerTeamSwap;
        UseNativeWayland = _settings.Linux.UseNativeWayland;

        SpritePreference = _settings.Sprite.SpritePreference;
        SelectedTheme = Themes.Contains(_themeService.CurrentTheme)
            ? _themeService.CurrentTheme
            : AppTheme.Dark;
        SelectedDensity = _uiDensityService.CurrentDensity;

        _isLoading = false;
    }

    [RelayCommand]
    private void Save()
    {
        _settings.Startup.DefaultSaveVersion = DefaultSaveVersion;
        _settings.Startup.AutoLoadSaveOnStartup = AutoLoadMode;
        _settings.Startup.ForceHaXOnLaunch = ForceHaX;
        _settings.Startup.ShowChangelogOnUpdate = ShowChangelog;
        _settings.Startup.CheckForUpdatesOnStartup = CheckForUpdatesOnStartup;

        _settings.Backup.BAKEnabled = BakEnabled;
        _settings.Backup.BAKPrompt = BakPrompt;

        _settings.SlotWrite.SetUpdateDex = SetUpdateDex;
        _settings.SlotWrite.SetUpdatePKM = SetUpdatePKM;
        _settings.SlotWrite.SetUpdateRecords = SetUpdateRecords;
        _settings.SlotWrite.ModifyUnset = ModifyUnset;
        _settings.EditorBehavior.WarnClosingModified = WarnClosingModified;
        _settings.Legality.Game.Gen3.AllowBattleTowerTeamSwap = AllowBattleTowerTeamSwap;
        _settings.Linux.UseNativeWayland = UseNativeWayland;

        _settings.Sprite.SpritePreference = SpritePreference;

        _settingsStore.Save(_settings);
        _settings.InitializeCore();

        CloseRequested?.Invoke();
    }
}
