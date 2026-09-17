namespace PKHeX.Application.Abstractions;

/// <summary>
/// UI theme/appearance preference. Persisted via <c>AppSettings</c>. The product-facing picker
/// exposes only <see cref="Dark"/> and <see cref="Light"/>; legacy values are retained for settings
/// compatibility and normalized by the host service.
/// </summary>
public enum AppTheme
{
    /// <summary>Neutral dark Fluent palette.</summary>
    Dark,
    /// <summary>Light surfaces with dark text, tuned for WCAG AA contrast.</summary>
    Light,
    /// <summary>Legacy maximum-contrast palette retained for settings compatibility.</summary>
    HighContrast,
    /// <summary>Legacy system-tracking preference retained for settings compatibility.</summary>
    System,
}

/// <summary>
/// Applies the active UI theme (colors/brushes exposed as resources) at runtime, so switching
/// requires no restart. Framework-free: the Avalonia-specific implementation lives in the host
/// project (see <c>PKHeX.Avalonia.Services.ThemeService</c>) and drives Avalonia's
/// <c>ThemeVariant</c>/<c>ThemeDictionaries</c> APIs. The host applies the two product-facing
/// Light/Dark variants and normalizes legacy values.
/// </summary>
public interface IThemeService
{
    /// <summary>The currently-applied theme preference.</summary>
    AppTheme CurrentTheme { get; }

    /// <summary>Applies and persists the given theme preference immediately.</summary>
    void ApplyTheme(AppTheme theme);
}
