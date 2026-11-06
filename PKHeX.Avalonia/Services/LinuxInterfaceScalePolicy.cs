using System.Globalization;

namespace PKHeX.Avalonia.Services;

/// <summary>
/// Decides at startup which interface scale the X11 backend uses on Linux. Avalonia's X11 backend
/// reads the <c>AVALONIA_GLOBAL_SCALE_FACTOR</c> environment variable before it creates a window, so
/// the host sets it from the saved setting. A value the user already exported wins, so a user can
/// still override the scale from a launcher or a terminal. The native Wayland backend ignores the
/// variable and uses the compositor's scale.
/// </summary>
internal static class LinuxInterfaceScalePolicy
{
    public const string EnvironmentVariable = "AVALONIA_GLOBAL_SCALE_FACTOR";

    /// <param name="isLinux">Whether the app runs on Linux; other platforms follow the OS scale.</param>
    /// <param name="settingPercent">Reads the saved percentage; only called when it can apply.</param>
    /// <param name="environmentValue">The current value of <see cref="EnvironmentVariable"/>, if set.</param>
    /// <returns>The value to export, or <see langword="null"/> to leave the environment unchanged.</returns>
    public static string? Resolve(bool isLinux, Func<int> settingPercent, string? environmentValue)
    {
        if (!isLinux || !string.IsNullOrWhiteSpace(environmentValue))
            return null;

        var percent = settingPercent();
        if (percent is < 50 or > 400)
            return null;

        return (percent / 100.0).ToString("0.##", CultureInfo.InvariantCulture);
    }
}
