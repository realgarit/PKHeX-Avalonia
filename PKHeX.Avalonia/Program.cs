using Avalonia;

namespace PKHeX.Avalonia;

internal sealed class Program
{
    [STAThread]
    public static void Main(string[] args) => BuildAvaloniaApp()
        .StartWithClassicDesktopLifetime(args);

    public static AppBuilder BuildAvaloniaApp()
    {
        // Desktop environments match a window to its .desktop entry through this ID: the Flatpak app
        // ID inside the sandbox, otherwise the StartupWMClass of the installed entries.
        var windowClass = Environment.GetEnvironmentVariable("FLATPAK_ID") ?? "PKHeX.Avalonia";
        return AppBuilder.Configure<App>()
            .UsePlatformDetect()
            // Native Wayland when a compositor accepts the connection; otherwise the X11 backend chosen
            // above (X11 desktops, XWayland, Xvfb). Has no effect on Windows or macOS.
            .UseWaylandWithFallback()
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
