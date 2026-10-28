using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Media.Imaging;
using Moq;
using PKHeX.Application.Abstractions;
using PKHeX.Application.Services;
using PKHeX.Avalonia.Views;
using PKHeX.Core;
using PKHeX.Presentation.Localization;
using PKHeX.Presentation.ViewModels;

namespace PKHeX.Avalonia.Render.Tests;

public class Pokedex9aRenderTests
{
    [AvaloniaTheory]
    [InlineData("en", 960, 640)] [InlineData("de", 620, 420)]
    [InlineData("ja", 620, 420)] [InlineData("pt-BR", 620, 420)]
    public async Task RealSkiaFramesShowFormsFlagsAndLanguages(string language, int width, int height)
    {
        var previous = LocalizedStrings.Instance.CurrentLanguage;
        var dataLanguage = GameInfo.CurrentLanguage; var strings = GameInfo.Strings; var filtered = GameInfo.FilteredSources;
        var culture = CultureInfo.CurrentCulture; var uiCulture = CultureInfo.CurrentUICulture;
        var defaultCulture = CultureInfo.DefaultThreadCurrentCulture; var defaultUiCulture = CultureInfo.DefaultThreadCurrentUICulture;
        Window? window = null;
        try
        {
            new LanguageService().SetLanguage(language); LocalizedStrings.Instance.SetLanguage(language);
            var directory = new DirectoryInfo(AppContext.BaseDirectory);
            while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Tests/savefiles/gen9a_legendsza.main"))) directory = directory.Parent;
            Assert.NotNull(directory);
            var save = Assert.IsType<SAV9ZA>(SaveUtil.GetSaveFile(File.ReadAllBytes(Path.Combine(directory.FullName, "Tests/savefiles/gen9a_legendsza.main"))));
            save.SetValue(SaveBlockAccessor9ZA.KSaveRevision, 1ul);
            var before = save.Zukan.Data.ToArray();
            using var vm = new Pokedex9aEditorViewModel(save, new Mock<IDialogService>().Object);
            vm.SelectedSpecies = vm.FilteredSpecies.Single(item => item.Value == (int)Species.Tatsugiri);
            vm.Forms[0].Seen = true; vm.Forms[0].Caught = true; vm.Forms[0].Shiny = true;
            vm.Flags[^1].Value = true; vm.Languages[1].Value = true;
            vm.DisplayShiny = true; vm.IsNew = true;
            var view = new Pokedex9aEditor { DataContext = vm };
            window = new Window { Content = view, Width = width, Height = height }; window.Show();
            await PKHeX.Testing.HeadlessRenderSettling.Settle(window);
            var scroll = view.FindControl<ScrollViewer>("Dex9aDetails")!;
            for (int state = 0; state < 3; state++)
            {
                if (state == 1) scroll.Offset = new Vector(0, Math.Max(0, (scroll.Extent.Height - scroll.Viewport.Height) / 2));
                if (state == 2) scroll.ScrollToEnd();
                await PKHeX.Testing.HeadlessRenderSettling.Settle(window);
                using var frame = new RenderTargetBitmap(new PixelSize(width, height)); frame.Render(window);
                using var bytes = new MemoryStream(); frame.Save(bytes, PngBitmapEncoderOptions.Default);
                using var pixels = SkiaSharp.SKBitmap.Decode(bytes.ToArray()); Assert.NotNull(pixels);
                Assert.Contains(pixels.Pixels, pixel => pixel != pixels.GetPixel(0, 0));
                if (Environment.GetEnvironmentVariable("PKHEX_HEADLESS_CAPTURE") == "1" && Environment.GetEnvironmentVariable("PKHEX_HEADLESS_CAPTURE_DIR") is { } path)
                { Directory.CreateDirectory(path); frame.Save(Path.Combine(path, $"dexza-{language}-state{state}.png"), PngBitmapEncoderOptions.Default); }
            }
            Assert.Equal(before, save.Zukan.Data.ToArray());
        }
        finally
        {
            window?.Close(); LocalizedStrings.Instance.SetLanguage(previous); GameInfo.CurrentLanguage = dataLanguage;
            GameInfo.Strings = strings; GameInfo.FilteredSources = filtered;
            CultureInfo.CurrentCulture = culture; CultureInfo.CurrentUICulture = uiCulture;
            CultureInfo.DefaultThreadCurrentCulture = defaultCulture; CultureInfo.DefaultThreadCurrentUICulture = defaultUiCulture;
        }
    }
}
