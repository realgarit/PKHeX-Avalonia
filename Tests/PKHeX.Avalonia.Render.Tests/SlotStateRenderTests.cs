using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Media.Imaging;
using Avalonia.Styling;
using Avalonia.VisualTree;
using PKHeX.Application.Models;
using PKHeX.Application.Services;
using PKHeX.Avalonia.Services;
using PKHeX.Avalonia.Views;
using PKHeX.Core;
using PKHeX.Presentation.ViewModels;
using SkiaSharp;

namespace PKHeX.Avalonia.Render.Tests;

public class SlotStateRenderTests
{
    [AvaloniaTheory]
    [InlineData(false)] [InlineData(true)]
    public async Task PopulatedAlphaBoxesAndConcurrentMarkersRemainVisibleInBothThemes(bool dark)
    {
        var previous = global::Avalonia.Application.Current!.RequestedThemeVariant;
        Window? window = null;
        try
        {
            global::Avalonia.Application.Current.RequestedThemeVariant = dark ? ThemeVariant.Dark : ThemeVariant.Light;
            var directory = new DirectoryInfo(AppContext.BaseDirectory);
            while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Tests/savefiles/gen9a_legendsza.main"))) directory = directory.Parent;
            Assert.NotNull(directory);
            foreach (var file in new[] { "gen8a_legendsarceus.main", "gen9a_legendsza.main" })
            {
                var save = SaveUtil.GetSaveFile(File.ReadAllBytes(Path.Combine(directory.FullName, "Tests/savefiles", file)))!;
                var renderer = new AvaloniaSpriteRenderer(new AppSettings()); renderer.Initialize(save);
                var vm = new BoxViewerViewModel(save, renderer);
                Assert.Contains(vm.Slots, s => s.SemanticSummary.Contains(PKHeX.Presentation.Localization.LocalizedStrings.Instance["SlotState_Alpha"]));
                window = new Window { Content = new BoxViewer { DataContext = vm }, Width = 490, Height = 400 };
                window.Show(); await PKHeX.Testing.HeadlessRenderSettling.Settle(window);
                SaveFrame(window, $"box-markers-{save.Context}-{(dark ? "dark" : "light")}");
                window.Close();
            }
            // All five storage badges coexist with Alpha, shiny, held item and egg state.
            var compositor = new AvaloniaSpriteRenderer(new AppSettings());
            var pokemon = new PA9 { Species = 25, IsAlpha = true, IsEgg = true, HeldItem = 81 }; pokemon.SetShiny();
            var state = new SpriteSlotState(true, false, StorageSlotSource.BattleTeam1 | StorageSlotSource.Locked | StorageSlotSource.Party2 | StorageSlotSource.Starter);
            using var bitmap = SKBitmap.Decode(compositor.GetSlotSprite(pokemon, state)!);
            if (CapturePath is { } path)
            {
                Directory.CreateDirectory(path);
                using var image = SKImage.FromBitmap(bitmap); using var data = image.Encode(SKEncodedImageFormat.Png, 100);
                File.WriteAllBytes(Path.Combine(path, "all-slot-markers.png"), data.ToArray());
            }
        }
        finally { window?.Close(); global::Avalonia.Application.Current.RequestedThemeVariant = previous; }
    }

    [AvaloniaTheory]
    [InlineData(false)] [InlineData(true)]
    public async Task AboutStatusGrowsTheActualRenderedWindowWithoutClippingActions(bool dark)
    {
        var previous = global::Avalonia.Application.Current!.RequestedThemeVariant;
        var vm = new AboutViewModel();
        var view = new AboutView { DataContext = vm };
        var window = new Window { Content = view, MaxWidth = 900, MaxHeight = 800 };
        try
        {
            global::Avalonia.Application.Current.RequestedThemeVariant = dark ? ThemeVariant.Dark : ThemeVariant.Light;
            WindowService.SetMeasuredInitialBounds(window); WindowService.ConfigureAboutAutoSize(window);
            window.Show(); await PKHeX.Testing.HeadlessRenderSettling.Settle(window);
            var initial = window.ClientSize.Height;
            vm.UpdateCheckStatus = PKHeX.Presentation.Localization.LocalizedStrings.Instance.Format("Update_UpToDate", vm.UIVersion);
            await PKHeX.Testing.HeadlessRenderSettling.Settle(window);
            Assert.True(window.ClientSize.Height > initial);
            foreach (var button in view.GetVisualDescendants().OfType<Button>())
                Assert.InRange(button.TranslatePoint(new Point(0, button.Bounds.Height), window)!.Value.Y, 1, window.ClientSize.Height);
            SaveFrame(window, $"about-update-resized-{(dark ? "dark" : "light")}");
        }
        finally { window.Close(); global::Avalonia.Application.Current.RequestedThemeVariant = previous; }
    }

    private static string? CapturePath => Environment.GetEnvironmentVariable("PKHEX_HEADLESS_CAPTURE") == "1" ? Environment.GetEnvironmentVariable("PKHEX_HEADLESS_CAPTURE_DIR") : null;
    private static void SaveFrame(Window window, string name)
    {
        using var frame = new RenderTargetBitmap(new PixelSize((int)window.ClientSize.Width, (int)window.ClientSize.Height)); frame.Render(window);
        using var bytes = new MemoryStream(); frame.Save(bytes, PngBitmapEncoderOptions.Default); using var pixels = SKBitmap.Decode(bytes.ToArray());
        Assert.NotNull(pixels); Assert.Contains(pixels.Pixels, p => p != pixels.GetPixel(0, 0));
        if (CapturePath is { } path) { Directory.CreateDirectory(path); frame.Save(Path.Combine(path, name + ".png"), PngBitmapEncoderOptions.Default); }
    }
}
