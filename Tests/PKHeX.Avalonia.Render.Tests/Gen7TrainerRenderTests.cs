using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Media.Imaging;
using Avalonia.VisualTree;
using PKHeX.Avalonia.Views;
using PKHeX.Core;
using PKHeX.Presentation.Localization;
using PKHeX.Presentation.ViewModels;
using PKHeX.Application.Services;
using System.Globalization;

namespace PKHeX.Avalonia.Render.Tests;

public class Gen7TrainerRenderTests
{
    [AvaloniaTheory]
    [InlineData("gen7_sun.main", "en", 900, 600)] [InlineData("gen7_ultrasun.main", "de", 620, 420)]
    [InlineData("gen7_sun.main", "ja", 620, 420)] [InlineData("gen7_ultrasun.main", "pt-BR", 620, 420)]
    public async Task TrainerSectionsRenderWithPinnedApplyAndNoSourceWrites(string file, string language, int width, int height)
    {
        var previous = LocalizedStrings.Instance.CurrentLanguage;
        var previousData = GameInfo.CurrentLanguage; var previousStrings = GameInfo.Strings; var previousFiltered = GameInfo.FilteredSources;
        var culture = CultureInfo.CurrentCulture; var uiCulture = CultureInfo.CurrentUICulture;
        var defaultCulture = CultureInfo.DefaultThreadCurrentCulture; var defaultUiCulture = CultureInfo.DefaultThreadCurrentUICulture;
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Tests/savefiles", file))) directory = directory.Parent;
        Assert.NotNull(directory);
        var save = Assert.IsAssignableFrom<SAV7>(SaveUtil.GetSaveFile(File.ReadAllBytes(Path.Combine(directory.FullName, "Tests/savefiles", file))));
        var before = save.Data.ToArray(); using var vm = new Misc7EditorViewModel(save); Window? window = null;
        try
        {
            new LanguageService().SetLanguage(language); LocalizedStrings.Instance.SetLanguage(language); vm.RefreshLanguage();
            var view = new Misc7Editor { DataContext = vm }; window = new Window { Content = view, Width = width, Height = height }; window.Show();
            var tabs = view.FindControl<TabControl>("Trainer7Sections")!;
            for (int i = 0; i < tabs.Items.Count; i++)
            {
                tabs.SelectedIndex = i; await PKHeX.Testing.HeadlessRenderSettling.Settle(window);
                using var frame = new RenderTargetBitmap(new PixelSize(width, height)); frame.Render(window);
                if (Environment.GetEnvironmentVariable("PKHEX_HEADLESS_CAPTURE") == "1" && Environment.GetEnvironmentVariable("PKHEX_HEADLESS_CAPTURE_DIR") is { } path)
                { Directory.CreateDirectory(path); frame.Save(Path.Combine(path, $"trainer7-{Path.GetFileNameWithoutExtension(file)}-{language}-tab{i}.png"), PngBitmapEncoderOptions.Default); }
                if (i is 0 or 1 or 2)
                {
                    var scroll = view.GetVisualDescendants().OfType<ScrollViewer>().First(control => control.IsEffectivelyVisible && control.Content is StackPanel);
                    scroll.ScrollToEnd(); await PKHeX.Testing.HeadlessRenderSettling.Settle(window);
                    using var end = new RenderTargetBitmap(new PixelSize(width, height)); end.Render(window);
                    if (Environment.GetEnvironmentVariable("PKHEX_HEADLESS_CAPTURE") == "1" && Environment.GetEnvironmentVariable("PKHEX_HEADLESS_CAPTURE_DIR") is { } endPath)
                        end.Save(Path.Combine(endPath, $"trainer7-{Path.GetFileNameWithoutExtension(file)}-{language}-tab{i}-end.png"), PngBitmapEncoderOptions.Default);
                }
            }
            vm.SaveCommand.Execute(null); Assert.False(vm.HasError); Assert.Equal(before, save.Data.ToArray());
        }
        finally
        {
            window?.Close(); LocalizedStrings.Instance.SetLanguage(previous);
            GameInfo.CurrentLanguage = previousData; GameInfo.Strings = previousStrings; GameInfo.FilteredSources = previousFiltered;
            CultureInfo.CurrentCulture = culture; CultureInfo.CurrentUICulture = uiCulture;
            CultureInfo.DefaultThreadCurrentCulture = defaultCulture; CultureInfo.DefaultThreadCurrentUICulture = defaultUiCulture;
        }
    }
}
