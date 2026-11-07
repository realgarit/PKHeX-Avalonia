using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Styling;

namespace PKHeX.Avalonia.Render.Tests;

/// <summary>
/// Issue #245: every shared vector icon renders with real Skia at 100, 125, 150 and 200 % display
/// scaling, stays inside its 16 DIP box and follows the central state colours in every theme. The
/// optional capture is the visual inventory sheet of the whole set.
/// </summary>
public class IconRenderTests
{
    private static string[] IconKeys()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "PKHeX.Avalonia/Styles/Icons.axaml"))) directory = directory.Parent;
        Assert.NotNull(directory);
        var source = File.ReadAllText(Path.Combine(directory.FullName, "PKHeX.Avalonia/Styles/Icons.axaml"));
        return System.Text.RegularExpressions.Regex.Matches(source, @"x:Key=""(Icon\.[A-Za-z.]+)""").Select(m => m.Groups[1].Value).ToArray();
    }

    [AvaloniaTheory]
    [InlineData(1.0, "Light")] [InlineData(1.25, "Light")] [InlineData(1.5, "Dark")] [InlineData(2.0, "Dark")]
    [InlineData(2.0, "HighContrast")]
    public async Task EveryIconRendersCrisplyInsideItsBox(double scale, string theme)
    {
        var app = global::Avalonia.Application.Current!;
        var previous = app.RequestedThemeVariant;
        Window? window = null;
        try
        {
            app.RequestedThemeVariant = theme switch
            {
                "Dark" => ThemeVariant.Dark,
                "HighContrast" => PKHeX.Avalonia.Services.AppThemeVariants.HighContrast,
                _ => ThemeVariant.Light,
            };
            var keys = IconKeys();
            var size = 16 * scale;
            var panel = new WrapPanel { Orientation = Orientation.Horizontal, Width = (size + 8) * 8 };
            var icons = new List<PathIcon>();
            foreach (var key in keys)
            {
                var icon = new PathIcon { Classes = { "icon" }, Width = size, Height = size, Data = (Geometry)app.FindResource(key)! };
                icons.Add(icon);
                panel.Children.Add(new Border { Padding = new Thickness(4), Child = icon });
            }
            // A disabled action shows the disabled-state colour from the same central style.
            var disabled = new Button { Classes = { "icon-action" }, IsEnabled = false, Content = new PathIcon { Classes = { "icon" }, Data = (Geometry)app.FindResource("Icon.Close")! } };
            var enabled = new Button { Classes = { "icon-action" }, Content = new PathIcon { Classes = { "icon" }, Data = (Geometry)app.FindResource("Icon.Close")! } };
            panel.Children.Add(enabled); panel.Children.Add(disabled);

            window = new Window { Content = panel, SizeToContent = SizeToContent.WidthAndHeight };
            window.Show();
            await PKHeX.Testing.HeadlessRenderSettling.Settle(window);

            var enabledBrush = ((PathIcon)enabled.Content!).Foreground as ISolidColorBrush;
            var disabledBrush = ((PathIcon)disabled.Content!).Foreground as ISolidColorBrush;
            Assert.NotNull(enabledBrush); Assert.NotNull(disabledBrush);
            Assert.NotEqual(enabledBrush!.Color, disabledBrush!.Color);

            using var frame = new RenderTargetBitmap(new PixelSize((int)Math.Ceiling(window.ClientSize.Width), (int)Math.Ceiling(window.ClientSize.Height)));
            frame.Render(window);
            using var bytes = new MemoryStream();
            frame.Save(bytes, PngBitmapEncoderOptions.Default);
            using var pixels = SkiaSharp.SKBitmap.Decode(bytes.ToArray());
            var background = pixels.GetPixel(0, 0);

            for (int i = 0; i < icons.Count; i++)
            {
                var origin = icons[i].TranslatePoint(default, window)!.Value;
                // Ink inside the icon box: enough to read at this scale.
                int ink = 0;
                for (int y = (int)origin.Y; y < (int)(origin.Y + size); y++)
                for (int x = (int)origin.X; x < (int)(origin.X + size); x++)
                    if (Distance(pixels.GetPixel(x, y), background) > 60) ink++;
                Assert.True(ink >= size * size * 0.08, $"{keys[i]} at {scale:P0} {theme}: only {ink} ink pixels");

                // No ink spills into the 4 DIP padding around the box.
                for (int x = (int)origin.X - 3; x < (int)(origin.X + size + 3); x++)
                {
                    Assert.True(Distance(pixels.GetPixel(x, (int)origin.Y - 3), background) <= 60, $"{keys[i]} spills above its box");
                    Assert.True(Distance(pixels.GetPixel(x, (int)(origin.Y + size + 2)), background) <= 60, $"{keys[i]} spills below its box");
                }
            }

            if (Environment.GetEnvironmentVariable("PKHEX_HEADLESS_CAPTURE") == "1" && Environment.GetEnvironmentVariable("PKHEX_HEADLESS_CAPTURE_DIR") is { } path)
            {
                Directory.CreateDirectory(path);
                frame.Save(Path.Combine(path, $"icon-inventory-{theme}-{scale * 100:0}.png"), PngBitmapEncoderOptions.Default);
            }
        }
        finally
        {
            window?.Close();
            app.RequestedThemeVariant = previous;
        }
    }

    private static int Distance(SkiaSharp.SKColor a, SkiaSharp.SKColor b) =>
        Math.Abs(a.Red - b.Red) + Math.Abs(a.Green - b.Green) + Math.Abs(a.Blue - b.Blue);
}
