using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Media.Imaging;
using Avalonia.Styling;
using Avalonia.VisualTree;
using PKHeX.Application.Services;
using PKHeX.Avalonia.Views;
using PKHeX.Core;
using PKHeX.Presentation.Localization;
using PKHeX.Presentation.Models;
using PKHeX.Presentation.ViewModels;

namespace PKHeX.Avalonia.Render.Tests;

public class BoxWallpaperRenderTests
{
    [AvaloniaTheory]
    [InlineData("e", "en", false)] [InlineData("e", "en", true)]
    [InlineData("rsbox", "ja", false)] [InlineData("rsbox", "ja", true)]
    [InlineData("bdsp", "de", false)] [InlineData("bdsp", "de", true)]
    [InlineData("sv", "pt-BR", false)] [InlineData("sv", "pt-BR", true)]
    public async Task SpecialWallpaperLabelsRenderAndRefreshWithoutCommitting(string game, string language, bool dark)
    {
        var previous = LocalizedStrings.Instance.CurrentLanguage;
        var dataLanguage = GameInfo.CurrentLanguage;
        var strings = GameInfo.Strings; var filtered = GameInfo.FilteredSources;
        var culture = CultureInfo.CurrentCulture; var uiCulture = CultureInfo.CurrentUICulture;
        var defaultCulture = CultureInfo.DefaultThreadCurrentCulture; var defaultUiCulture = CultureInfo.DefaultThreadCurrentUICulture;
        var application = global::Avalonia.Application.Current!;
        var theme = application.RequestedThemeVariant;
        Window? window = null;
        try
        {
            new LanguageService().SetLanguage(language);
            LocalizedStrings.Instance.SetLanguage(language);
            application.RequestedThemeVariant = dark ? ThemeVariant.Dark : ThemeVariant.Light;
            SaveFile save = game switch
            {
                "e" => new SAV3E(), "rsbox" => new SAV3RSBox(), "bdsp" => LoadFixture("gen8b_brilliantdiamond.bin"), _ => LoadFixture("gen9_violet.main"),
            };
            var wallpaper = (IBoxDetailWallpaper)save;
            var id = game switch { "e" => 16, "rsbox" => 20, "bdsp" => 31, _ => 19 };
            wallpaper.SetBoxWallpaper(0, id);
            save.State.Edited = false;
            var before = save.Data.ToArray();
            using var vm = new BoxLayoutEditorViewModel(save);
            var view = new BoxLayoutEditor { DataContext = vm };
            window = new Window { Content = view, Width = 900, Height = 600 };
            window.Show();
            await PKHeX.Testing.HeadlessRenderSettling.Settle(window);
            var selector = view.GetVisualDescendants().OfType<ComboBox>().Single(control => ReferenceEquals(control.DataContext, vm.Boxes[0]));
            var selected = Assert.IsType<BoxWallpaperChoice>(selector.SelectedItem);
            Assert.Equal(id, selected.Value);
            var text = Assert.Single(selector.GetVisualDescendants().OfType<TextBlock>(), control => control.Text == selected.Text && control.IsEffectivelyVisible);
            Assert.True(text.Bounds.Width >= text.DesiredSize.Width - 1, $"Wallpaper label clipped: {selected.Text}");
            foreach (var button in view.GetVisualDescendants().OfType<Button>().Where(control => control.Command == vm.SaveCommand || control.Command == vm.CancelCommand))
            {
                var position = button.TranslatePoint(default, window)!.Value;
                Assert.True(position.Y + button.Bounds.Height <= window.ClientSize.Height);
            }
            using var bitmap = new RenderTargetBitmap(PixelSize.FromSize(window.ClientSize, 1));
            bitmap.Render(window);
            using var buffer = new MemoryStream(); bitmap.Save(buffer, PngBitmapEncoderOptions.Default);
            using var pixels = SkiaSharp.SKBitmap.Decode(buffer.ToArray());
            Assert.Contains(pixels.Pixels, pixel => pixel != pixels.GetPixel(0, 0));
            if (Environment.GetEnvironmentVariable("PKHEX_HEADLESS_CAPTURE_DIR") is { } path)
            {
                Directory.CreateDirectory(path);
                bitmap.Save(Path.Combine(path, $"wallpaper-{game}-{language}-{(dark ? "dark" : "light")}.png"), PngBitmapEncoderOptions.Default);
            }
            vm.SaveCommand.Execute(null);
            Assert.Equal(before, save.Data.ToArray());
            Assert.Equal(id, wallpaper.GetBoxWallpaper(0));
            Assert.False(save.State.Edited);
        }
        finally
        {
            window?.Close(); application.RequestedThemeVariant = theme;
            LocalizedStrings.Instance.SetLanguage(previous);
            GameInfo.CurrentLanguage = dataLanguage; GameInfo.Strings = strings; GameInfo.FilteredSources = filtered;
            CultureInfo.CurrentCulture = culture; CultureInfo.CurrentUICulture = uiCulture;
            CultureInfo.DefaultThreadCurrentCulture = defaultCulture; CultureInfo.DefaultThreadCurrentUICulture = defaultUiCulture;
        }
    }

    private static SaveFile LoadFixture(string name)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        var file = Path.Combine("Tests/savefiles", name);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, file))) directory = directory.Parent;
        Assert.NotNull(directory);
        return Assert.IsAssignableFrom<SaveFile>(SaveUtil.GetSaveFile(File.ReadAllBytes(Path.Combine(directory.FullName, file))));
    }
}
