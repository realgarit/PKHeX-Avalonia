using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Media.Imaging;
using Avalonia.Styling;
using Moq;
using PKHeX.Application.Abstractions;
using PKHeX.Avalonia.Services;
using PKHeX.Avalonia.Views;
using PKHeX.Presentation.ViewModels;
using PKHeX.Core;

namespace PKHeX.Avalonia.Render.Tests;

public class UtilityWindowRenderTests
{
    [AvaloniaTheory]
    [InlineData("update", false)] [InlineData("update", true)]
    [InlineData("legality", false)] [InlineData("legality", true)]
    [InlineData("about", false)] [InlineData("about", true)]
    [InlineData("audit", false)] [InlineData("audit", true)]
    [InlineData("database", false)] [InlineData("database", true)]
    public async Task UtilitiesHaveCompactFirstFramesAndContentUsesTheHost(string kind, bool dark)
    {
        var previous = global::Avalonia.Application.Current!.RequestedThemeVariant;
        Window? window = null;
        try
        {
            global::Avalonia.Application.Current.RequestedThemeVariant = dark ? ThemeVariant.Dark : ThemeVariant.Light;
            object vm = kind switch
            {
                "legality" => new LegalityViewModel("Legal!"),
                "about" => new AboutViewModel(),
                "audit" => new LegalityAuditViewModel(new SAV6XY(), Mock.Of<IDialogService>()),
                "database" => new PKMDatabaseViewModel(new SAV6XY(), Mock.Of<ISpriteRenderer>(), Mock.Of<IDialogService>()),
                _ => new UpdateDownloadViewModel(new ReleaseInfo("v1.79.0", "Release", "notes", "https://example.com", false, []),
                    new ReleaseAsset("asset.zip", "https://example.com/asset.zip"), Mock.Of<IUpdateInstaller>(), Mock.Of<IAppLifetime>()),
            };
            var view = ViewLocator.Build(vm);
            window = new Window { Content = view, MaxWidth = 1200, MaxHeight = 700 };
            if (kind is "about" or "audit" or "database") WindowService.SetMeasuredInitialBounds(window);
            else WindowService.ConfigureCompactUtilityBounds(window, kind == "legality", 1200, 700);
            Assert.True(window.Width <= (kind is "audit" or "database" ? 1200 : 620) && window.Height <= (kind is "audit" or "database" ? 700 : 440));
            window.Show(); await PKHeX.Testing.HeadlessRenderSettling.Settle(window);
            using var frame = new RenderTargetBitmap(new PixelSize((int)window.ClientSize.Width, (int)window.ClientSize.Height)); frame.Render(window);
            using var bytes = new MemoryStream(); frame.Save(bytes, PngBitmapEncoderOptions.Default); using var pixels = SkiaSharp.SKBitmap.Decode(bytes.ToArray());
            Assert.NotNull(pixels); Assert.Contains(pixels.Pixels, pixel => pixel != pixels.GetPixel(0, 0));
            if (Environment.GetEnvironmentVariable("PKHEX_HEADLESS_CAPTURE") == "1" && Environment.GetEnvironmentVariable("PKHEX_HEADLESS_CAPTURE_DIR") is { } path)
            { Directory.CreateDirectory(path); frame.Save(Path.Combine(path, $"utility-{kind}-{(dark ? "dark" : "light")}.png"), PngBitmapEncoderOptions.Default); }
        }
        finally { window?.Close(); global::Avalonia.Application.Current.RequestedThemeVariant = previous; }
    }
}
