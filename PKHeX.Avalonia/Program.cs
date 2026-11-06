using Avalonia;
using PKHeX.Application.Services;
using PKHeX.Avalonia.Services;
using PKHeX.Infrastructure.Configuration;

namespace PKHeX.Avalonia;

internal sealed class Program
{
    [STAThread]
    public static void Main(string[] args)
    {
        // Linux reads its display settings before the UI starts; the file is loaded at most once and
        // never on Windows or macOS.
        var isLinux = OperatingSystem.IsLinux();
        AppSettings.LinuxSettings? linux = null;
        AppSettings.LinuxSettings Linux() => linux ??= new SettingsStore(new AppPaths()).Load().Linux;

        var useWayland = NativeWaylandPolicy.ShouldUseWayland(
            isLinux,
            () => Linux().UseNativeWayland,
            Environment.GetEnvironmentVariable(NativeWaylandPolicy.EnvironmentVariable));
        var scale = LinuxInterfaceScalePolicy.Resolve(
            isLinux,
            () => Linux().InterfaceScalePercent,
            Environment.GetEnvironmentVariable(LinuxInterfaceScalePolicy.EnvironmentVariable));
        if (scale is not null)
            Environment.SetEnvironmentVariable(LinuxInterfaceScalePolicy.EnvironmentVariable, scale);

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
