using Avalonia;
using PKHeX.Avalonia.Services;
using PKHeX.Infrastructure.Configuration;

namespace PKHeX.Avalonia;

internal sealed class Program
{
    [STAThread]
    public static void Main(string[] args)
    {
        var useWayland = NativeWaylandPolicy.ShouldUseWayland(
            OperatingSystem.IsLinux(),
            static () => new SettingsStore(new AppPaths()).Load().Linux.UseNativeWayland,
            Environment.GetEnvironmentVariable(NativeWaylandPolicy.EnvironmentVariable));
        BuildAvaloniaApp(useWayland).StartWithClassicDesktopLifetime(args);
    }

    // Entry point for the XAML previewer and design tools: the platform default backend.
    public static AppBuilder BuildAvaloniaApp() => BuildAvaloniaApp(useWayland: false);

    private static AppBuilder BuildAvaloniaApp(bool useWayland)
    {
        // Desktop environments match a window to its .desktop entry through this ID: the Flatpak app
        // ID inside the sandbox, otherwise the StartupWMClass of the installed entries.
        var windowClass = Environment.GetEnvironmentVariable("FLATPAK_ID") ?? "PKHeX.Avalonia";
        var builder = AppBuilder.Configure<App>()
            .UsePlatformDetect();
        // Opt-in native Wayland (see NativeWaylandPolicy). It falls back to the X11 backend chosen
        // above when no compositor accepts the connection.
        if (useWayland)
            builder = builder.UseWaylandWithFallback();
        return builder
            .With(new X11PlatformOptions
            {
                WmClass = windowClass,
                UseDBusFilePicker = true,
            })
            .With(new WaylandPlatformOptions
            {
                AppId = windowClass,
            })
            .WithInterFont()
            .LogToTrace();
    }
}
