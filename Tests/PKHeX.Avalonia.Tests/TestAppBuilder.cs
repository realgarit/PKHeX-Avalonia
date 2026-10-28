using Avalonia;
using Avalonia.Headless;
using Avalonia.Threading;
using PKHeX.Avalonia;

[assembly: AvaloniaTestApplication(typeof(TestAppBuilder))]

public class TestAppBuilder
{
    public static AppBuilder BuildAvaloniaApp()
    {
        // Text is measured with Inter, the font the app bundles and renders with. Avalonia 12 replaced
        // the old fixed-advance headless glyph stub with a placeholder font whose every glyph is 1 em
        // wide, which no longer resembles real text.
        var builder = AppBuilder.Configure<App>().WithInterFont();
        return Environment.GetEnvironmentVariable("PKHEX_HEADLESS_CAPTURE") == "1"
            ? builder.UseSkia()
                .UseHeadless(new AvaloniaHeadlessPlatformOptions { UseHeadlessDrawing = false })
            : builder.UseHeadless(new AvaloniaHeadlessPlatformOptions());
    }
}
