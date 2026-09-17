using Avalonia.Styling;
using PKHeX.Application.Abstractions;
using PKHeX.Application.Services;

namespace PKHeX.Avalonia.Services;

/// <summary>
/// Avalonia-side implementation of <see cref="IThemeService"/>. Drives the app-wide
/// <see cref="global::Avalonia.Application.RequestedThemeVariant"/>, which every open window/dialog
/// inherits automatically and re-styles live, so switching needs no restart. The visible product
/// choices are Dark and Light. Legacy persisted values are normalized to Dark so an older settings
/// file cannot reintroduce an unsupported third appearance.
/// </summary>
public sealed class ThemeService : IThemeService
{
    private readonly AppSettings _settings;
    private readonly ISettingsStore _settingsStore;

    public ThemeService(AppSettings settings, ISettingsStore settingsStore)
    {
        _settings = settings;
        _settingsStore = settingsStore;
    }

    public AppTheme CurrentTheme => _settings.Theme.Selected;

    /// <summary>Applies the persisted theme preference. Call once at startup, before the main window is created.</summary>
    public void Initialize()
    {
        var normalized = Normalize(_settings.Theme.Selected);
        if (_settings.Theme.Selected != normalized)
        {
            _settings.Theme.Selected = normalized;
            _settingsStore.Save(_settings);
        }

        ApplyThemeVariant(normalized);
    }

    public void ApplyTheme(AppTheme theme)
    {
        theme = Normalize(theme);
        _settings.Theme.Selected = theme;
        _settingsStore.Save(_settings);
        ApplyThemeVariant(theme);
    }

    private static void ApplyThemeVariant(AppTheme theme)
    {
        var app = global::Avalonia.Application.Current;
        if (app is null)
            return;

        app.RequestedThemeVariant = theme == AppTheme.Light ? ThemeVariant.Light : ThemeVariant.Dark;
    }

    private static AppTheme Normalize(AppTheme theme) =>
        theme is AppTheme.Dark or AppTheme.Light ? theme : AppTheme.Dark;
}
