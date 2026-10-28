using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Media.Imaging;
using Avalonia.Styling;
using Avalonia.VisualTree;
using Moq;
using PKHeX.Application.Abstractions;
using PKHeX.Avalonia.Services;
using PKHeX.Avalonia.Views;
using PKHeX.Presentation.Localization;
using PKHeX.Presentation.ViewModels;
using PKHeX.Testing;

namespace PKHeX.Avalonia.Render.Tests;

public sealed class RsBoxWallpaperRenderTests
{
    public static IEnumerable<object[]> Languages => new[] { "en", "de", "es", "fr", "it", "ja", "ko", "pt-BR", "zh-Hans", "zh-Hant" }
        .SelectMany(language => new[] { new object[] { language, false }, new object[] { language, true } });

    [AvaloniaTheory]
    [MemberData(nameof(Languages))]
    public async Task CompleteSyntheticImageAndExportActionFitAllTenLocalesInBothThemes(string language, bool dark)
    {
        var previousLanguage = LocalizedStrings.Instance.CurrentLanguage;
        var app = global::Avalonia.Application.Current!;
        var previousTheme = app.RequestedThemeVariant;
        var save = RsBoxWallpaperFixture.Create(enabled: false);
        var original = RsBoxWallpaperFixture.BoxBuffer(save);
        Window? window = null;
        try
        {
            LocalizedStrings.Instance.SetLanguage(language);
            app.RequestedThemeVariant = dark ? ThemeVariant.Dark : ThemeVariant.Light;
            using var vm = new RsBoxWallpaperViewModel(save, new PngImageCodec(), Mock.Of<IDialogService>());
            var view = new RsBoxWallpaperView { DataContext = vm };
            window = new Window { Content = view, MaxWidth = 1000, MaxHeight = 750 };
            WindowService.SetMeasuredInitialBounds(window);
            Assert.InRange(window.Width, 420, 640);
            Assert.InRange(window.Height, 330, 500);
            window.Show();
            await HeadlessRenderSettling.Settle(window);
            var image = view.FindControl<Image>("WallpaperPreview")!;
            Assert.NotNull(image.Source);
            Assert.Equal(568, image.Source!.Size.Width); Assert.Equal(216, image.Source.Size.Height);
            AssertVisible(window, image);
            foreach (var button in view.GetVisualDescendants().OfType<Button>().Where(button => button.IsEffectivelyVisible))
            {
                AssertVisible(window, button);
                Assert.True(button.Bounds.Width + button.Margin.Left + button.Margin.Right >= button.DesiredSize.Width - 1);
            }
            Assert.True(view.FindControl<Button>("ExportWallpaper")!.IsEffectivelyEnabled);
            Assert.Equal(LocalizedStrings.Instance["RsBoxWallpaper_Disabled"], vm.Status);
            Assert.Equal(original, RsBoxWallpaperFixture.BoxBuffer(save));
            using var frame = new RenderTargetBitmap(PixelSize.FromSize(window.ClientSize, 1));
            frame.Render(window);
            if (Environment.GetEnvironmentVariable("PKHEX_HEADLESS_CAPTURE_DIR") is { } path)
            {
                Directory.CreateDirectory(path);
                frame.Save(Path.Combine(path, $"rsbox-synthetic-{language}-{(dark ? "dark" : "light")}.png"), PngBitmapEncoderOptions.Default);
            }
        }
        finally
        {
            window?.Close(); app.RequestedThemeVariant = previousTheme; LocalizedStrings.Instance.SetLanguage(previousLanguage);
        }
    }

    [AvaloniaTheory]
    [InlineData(false)] [InlineData(true)]
    public async Task MissingImageShowsUnavailableAndDisablesExport(bool dark)
    {
        var app = global::Avalonia.Application.Current!; var previous = app.RequestedThemeVariant;
        using var vm = new RsBoxWallpaperViewModel(RsBoxWallpaperFixture.Create(false, false), new PngImageCodec(), Mock.Of<IDialogService>());
        var view = new RsBoxWallpaperView { DataContext = vm };
        var window = new Window { Content = view, Width = 620, Height = 430 };
        try
        {
            app.RequestedThemeVariant = dark ? ThemeVariant.Dark : ThemeVariant.Light;
            window.Show(); await HeadlessRenderSettling.Settle(window);
            Assert.False(view.FindControl<Button>("ExportWallpaper")!.IsEffectivelyEnabled);
            Assert.Null(view.FindControl<Image>("WallpaperPreview")!.Source);
            Assert.Equal(LocalizedStrings.Instance["RsBoxWallpaper_Unavailable"], vm.Status);
            using var frame = new RenderTargetBitmap(PixelSize.FromSize(window.ClientSize, 1)); frame.Render(window);
            if (Environment.GetEnvironmentVariable("PKHEX_HEADLESS_CAPTURE_DIR") is { } path)
            { Directory.CreateDirectory(path); frame.Save(Path.Combine(path, $"rsbox-unavailable-{dark}.png"), PngBitmapEncoderOptions.Default); }
        }
        finally { window.Close(); app.RequestedThemeVariant = previous; }
    }

    private static void AssertVisible(Window window, Control control)
    {
        var top = control.TranslatePoint(default, window)!.Value;
        var bottom = control.TranslatePoint(new Point(control.Bounds.Width, control.Bounds.Height), window)!.Value;
        Assert.True(top.X >= 0 && top.Y >= 0 && bottom.X <= window.ClientSize.Width + 1 && bottom.Y <= window.ClientSize.Height + 1,
            $"{control.GetType().Name} from {top} to {bottom} outside {window.ClientSize}");
    }
}
