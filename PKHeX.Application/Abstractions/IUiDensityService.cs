namespace PKHeX.Application.Abstractions;

/// <summary>
/// Controls the app-wide spacing and control-size vocabulary used by the Avalonia frontend.
/// </summary>
/// <remarks>
/// The enum is deliberately framework-free so the persisted preference and presentation settings
/// model do not depend on Avalonia. The host maps the selected value to its resource tokens.
/// </remarks>
public enum AppDensity
{
    /// <summary>Space-efficient desktop layout that keeps more of an editor visible.</summary>
    Compact,

    /// <summary>Roomier layout for touch input and users who prefer more breathing room.</summary>
    Comfortable,
}

/// <summary>Applies and persists the app-wide UI density preference.</summary>
public interface IUiDensityService
{
    /// <summary>The currently selected density preference.</summary>
    AppDensity CurrentDensity { get; }

    /// <summary>Applies and persists <paramref name="density"/> immediately.</summary>
    void ApplyDensity(AppDensity density);
}
