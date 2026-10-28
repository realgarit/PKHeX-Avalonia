namespace PKHeX.Avalonia.Services;

/// <summary>
/// Decides at startup whether to opt into Avalonia's native Wayland backend. X11 (XWayland on
/// Wayland desktops) is the default because the Wayland backend has no screen-reader support yet;
/// native Wayland is a Linux-only setting. An environment variable overrides the setting so a user
/// can recover from a misbehaving backend without opening the app.
/// </summary>
internal static class NativeWaylandPolicy
{
    /// <summary><c>1</c> forces native Wayland, <c>0</c> forces X11; any other value is ignored.</summary>
    public const string EnvironmentVariable = "PKHEX_AVALONIA_WAYLAND";

    /// <param name="isLinux">Whether the app runs on Linux; Windows and macOS never use Wayland.</param>
    /// <param name="settingEnabled">
    /// Reads the persisted setting. Only called on Linux without an override, so other platforms
    /// never touch the settings file this early.
    /// </param>
    /// <param name="environmentValue">The value of <see cref="EnvironmentVariable"/>, if set.</param>
    public static bool ShouldUseWayland(bool isLinux, Func<bool> settingEnabled, string? environmentValue)
    {
        if (!isLinux)
            return false;

        return environmentValue?.Trim() switch
        {
            "1" => true,
            "0" => false,
            _ => settingEnabled(),
        };
    }
}
