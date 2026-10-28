using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Styling;
using Avalonia.VisualTree;
using Moq;
using PKHeX.Application.Abstractions;
using PKHeX.Application.Services;
using PKHeX.Avalonia.Services;
using PKHeX.Avalonia.Views;
using PKHeX.Core;
using PKHeX.Presentation.ViewModels;
using SkiaSharp;

namespace PKHeX.Avalonia.Render.Tests;

public class ExperienceAndTrainerLayoutTests
{
    [AvaloniaTheory]
    [InlineData(false)] [InlineData(true)]
    public async Task ExperienceFillHasVisibleHeightAndTracksPointerDragInBothThemes(bool dark)
    {
        var previous = global::Avalonia.Application.Current!.RequestedThemeVariant;
        var save = BlankSaveFile.Get(GameVersion.SL);
        var pk = new PK9 { Species = 906, CurrentLevel = 50, Language = 2, PID = 12345, Nickname = "Sprigatito" }; pk.RefreshAbility(0);
        var previousSources = GameInfo.FilteredSources;
        GameInfo.FilteredSources = new FilteredGameDataSource(save, GameInfo.Sources, false);
        var renderer = new AvaloniaSpriteRenderer(new AppSettings()); renderer.Initialize(save);
        var vm = new PokemonEditorViewModel(pk, save, renderer, Mock.Of<IDialogService>(), Mock.Of<IWindowService>());
        var view = new PokemonEditor { DataContext = vm };
        var window = new Window { Content = view, Width = 360, Height = 860 };
        try
        {
            global::Avalonia.Application.Current.RequestedThemeVariant = dark ? ThemeVariant.Dark : ThemeVariant.Light;
            view.FindControl<ToggleButton>("MainDetailsToggle")!.IsChecked = true;
            window.Show(); await PKHeX.Testing.HeadlessRenderSettling.Settle(window);
            var bar = view.FindControl<Border>("ExpBar")!;
            bar.GetVisualAncestors().OfType<ScrollViewer>().First().ScrollToEnd();
            await PKHeX.Testing.HeadlessRenderSettling.Settle(window);
            var progress = view.FindControl<ProgressBar>("ExpProgress")!;
            Assert.True(progress.Bounds.Height >= 10, $"EXP fill has height {progress.Bounds.Height}");
            var point = bar.TranslatePoint(new Point(bar.Bounds.Width * 0.25, bar.Bounds.Height / 2), window)!.Value;
            window.MouseDown(point, MouseButton.Left);
            await PKHeX.Testing.HeadlessRenderSettling.Settle(window);
            Assert.InRange(vm.ExpPercent, 0.24, 0.26);
            Assert.Equal(50, vm.Level);
            CheckFill(window, bar, progress, 0.1, 0.8, $"exp-quarter-{dark}");
            point = bar.TranslatePoint(new Point(bar.Bounds.Width * 0.75, bar.Bounds.Height / 2), window)!.Value;
            window.MouseMove(point, RawInputModifiers.LeftMouseButton);
            window.MouseUp(point, MouseButton.Left);
            await PKHeX.Testing.HeadlessRenderSettling.Settle(window);
            Assert.InRange(vm.ExpPercent, 0.74, 0.76);
            Assert.Equal(50, vm.Level);
            CheckFill(window, bar, progress, 0.6, 0.9, $"exp-drag-{dark}");
            vm.Level = 100;
            await PKHeX.Testing.HeadlessRenderSettling.Settle(window);
            Assert.Equal(1, progress.Value);
        }
        finally { window.Close(); GameInfo.FilteredSources = previousSources; global::Avalonia.Application.Current.RequestedThemeVariant = previous; }
    }

    [AvaloniaTheory]
    [InlineData(false, 620)] [InlineData(true, 900)]
    public async Task ZaTimestampEdgesAndOptionalMapFieldSpacingAreConsistent(bool dark, int width)
    {
        var previous = global::Avalonia.Application.Current!.RequestedThemeVariant;
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Tests/savefiles/gen9a_legendsza.main"))) directory = directory.Parent;
        var save = Assert.IsType<SAV9ZA>(SaveUtil.GetSaveFile(File.ReadAllBytes(Path.Combine(directory!.FullName, "Tests/savefiles/gen9a_legendsza.main"))));
        var view = new TrainerZaWorkspace { DataContext = new TrainerEditorViewModel(save) };
        var window = new Window { Content = view, Width = width, Height = 600 };
        try
        {
            global::Avalonia.Application.Current.RequestedThemeVariant = dark ? ThemeVariant.Dark : ThemeVariant.Light;
            window.Show(); await PKHeX.Testing.HeadlessRenderSettling.Settle(window);
            var date = view.FindControl<DatePicker>("TrainerZaSavedDate")!;
            var fieldBrush = Assert.IsAssignableFrom<ISolidColorBrush>(window.FindResource(window.ActualThemeVariant, "ThemeControlBackgroundBrush"));
            Assert.Equal(fieldBrush.Color, Assert.IsAssignableFrom<ISolidColorBrush>(date.Background).Color);
            date.GetVisualAncestors().OfType<ScrollViewer>().First().ScrollToEnd();
            await PKHeX.Testing.HeadlessRenderSettling.Settle(window);
            Assert.Equal(view.FindControl<TextBlock>("TrainerZaSavedLabel")!.TranslatePoint(default, view)!.Value.X, date.TranslatePoint(default, view)!.Value.X, 2);
            Assert.InRange(date.Bounds.Width, 296, 346);
            foreach (var control in new Control[] { view.FindControl<TimePicker>("TrainerZaSavedTime")!, view.FindControl<NumericUpDown>("TrainerZaSavedSecond")! })
            {
                Assert.Equal(date.Bounds.Width, control.Bounds.Width, 2);
                Assert.Equal(date.TranslatePoint(default, view)!.Value.X, control.TranslatePoint(default, view)!.Value.X, 2);
            }
            SaveFrame(window, $"trainer-timestamp-{dark}");
            view.FindControl<TabControl>("TrainerZaSections")!.SelectedIndex = 1;
            await PKHeX.Testing.HeadlessRenderSettling.Settle(window);
            foreach (var name in new[] { "TrainerZaRoyalePoints", "TrainerZaRoyalePointsInfinite" })
            {
                var input = view.FindControl<NumericUpDown>(name)!;
                var group = Assert.IsType<StackPanel>(input.Parent);
                var label = Assert.IsType<TextBlock>(group.Children[0]);
                Assert.Equal(8, input.Bounds.Y - label.Bounds.Bottom, 2);
            }
            view.FindControl<NumericUpDown>("TrainerZaRoyalePointsInfinite")!.GetVisualAncestors().OfType<ScrollViewer>().First().ScrollToEnd();
            await PKHeX.Testing.HeadlessRenderSettling.Settle(window);
            SaveFrame(window, $"trainer-map-spacing-{dark}");
        }
        finally { window.Close(); global::Avalonia.Application.Current.RequestedThemeVariant = previous; }
    }

    private static void CheckFill(Window window, Border bar, ProgressBar progress, double filled, double empty, string name)
    {
        using var bitmap = Frame(window);
        var accent = Assert.IsType<SolidColorBrush>(progress.Foreground).Color;
        var expected = new SKColor(accent.R, accent.G, accent.B, accent.A);
        SKColor Pixel(double fraction)
        {
            var point = bar.TranslatePoint(new Point(bar.Bounds.Width * fraction, bar.Bounds.Height / 2), window)!.Value;
            return bitmap.GetPixel((int)point.X, (int)point.Y);
        }
        Assert.Equal(expected, Pixel(filled));
        Assert.NotEqual(expected, Pixel(empty));
        SaveFrame(window, name);
    }
    private static SKBitmap Frame(Window window)
    {
        using var frame = new RenderTargetBitmap(new PixelSize((int)window.ClientSize.Width, (int)window.ClientSize.Height)); frame.Render(window);
        using var stream = new MemoryStream(); frame.Save(stream, PngBitmapEncoderOptions.Default); return SKBitmap.Decode(stream.ToArray());
    }
    private static void SaveFrame(Window window, string name)
    {
        if (Environment.GetEnvironmentVariable("PKHEX_HEADLESS_CAPTURE") != "1" || Environment.GetEnvironmentVariable("PKHEX_HEADLESS_CAPTURE_DIR") is not { } path) return;
        Directory.CreateDirectory(path); using var bitmap = Frame(window); using var image = SKImage.FromBitmap(bitmap); using var bytes = image.Encode(SKEncodedImageFormat.Png, 100);
        File.WriteAllBytes(Path.Combine(path, name + ".png"), bytes.ToArray());
    }
}
