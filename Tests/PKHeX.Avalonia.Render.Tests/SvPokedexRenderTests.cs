using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Media.Imaging;
using Avalonia.VisualTree;
using PKHeX.Application.Services;
using PKHeX.Avalonia.Views;
using PKHeX.Core;
using PKHeX.Presentation.Localization;
using PKHeX.Presentation.ViewModels;

namespace PKHeX.Avalonia.Render.Tests;

public class SvPokedexRenderTests
{
    [AvaloniaTheory] [InlineData("en", 900, 600, "gen9_violet_indigo_public.main")] [InlineData("de", 620, 420, "gen9_violet_indigo_public.main")]
    [InlineData("ja", 620, 420, "gen9_violet_indigo_public.main")] [InlineData("pt-BR", 620, 420, "gen9_violet_indigo_public.main")]
    [InlineData("en", 900, 600, "gen9_scarlet.main")] [InlineData("de", 620, 420, "gen9_scarlet.main")]
    [InlineData("en", 900, 600, "gen9_scarlet_teal_public.main")] [InlineData("de", 620, 420, "gen9_scarlet_teal_public.main")]
    public async Task SvEntriesRenderWithDataAndNoSourceWrites(string language, int width, int height, string file)
    {
        string previous = LocalizedStrings.Instance.CurrentLanguage; var dataLanguage = GameInfo.CurrentLanguage;
        var strings = GameInfo.Strings; var filtered = GameInfo.FilteredSources;
        var culture = CultureInfo.CurrentCulture; var uiCulture = CultureInfo.CurrentUICulture;
        var defaultCulture = CultureInfo.DefaultThreadCurrentCulture; var defaultUiCulture = CultureInfo.DefaultThreadCurrentUICulture;
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Tests/savefiles/gen9_violet_indigo_public.main"))) directory = directory.Parent;
        Assert.NotNull(directory);
        var save = Assert.IsType<SAV9SV>(SaveUtil.GetSaveFile(File.ReadAllBytes(Path.Combine(directory.FullName, "Tests/savefiles", file))));
        var before = save.AllBlocks.ToDictionary(b => b.Key, b => (b.Type, Data: b.Data.ToArray()));
        using var vm = new PokedexGen9EditorViewModel(save); Window? window = null;
        try
        {
            new LanguageService().SetLanguage(language); LocalizedStrings.Instance.SetLanguage(language); vm.RefreshLanguage();
            var view = new PokedexGen9Editor { DataContext = vm }; window = new Window { Content = view, Width = width, Height = height }; window.Show();
            int[] species = save.SaveRevision == 0 ? [25, 906, 1010] : save.SaveRevision == 1 ? [25, 1017] : [25, 1017, 1024, 1025];
            for (int i = 0; i < species.Length; i++)
            {
                vm.SelectedSpecies = vm.SpeciesList.Single(choice => choice.Value == species[i]); await PKHeX.Testing.HeadlessRenderSettling.Settle(window);
                for (int end = 0; end < 2; end++)
                {
                    if (end == 1) { view.GetVisualDescendants().OfType<ScrollViewer>().First(s => s.IsEffectivelyVisible && s.Content is StackPanel).ScrollToEnd(); await PKHeX.Testing.HeadlessRenderSettling.Settle(window); }
                    using var frame = new RenderTargetBitmap(new PixelSize(width, height)); frame.Render(window);
                    if (Environment.GetEnvironmentVariable("PKHEX_HEADLESS_CAPTURE") == "1" && Environment.GetEnvironmentVariable("PKHEX_HEADLESS_CAPTURE_DIR") is { } path)
                    { Directory.CreateDirectory(path); frame.Save(Path.Combine(path, $"pokedex-sv-r{save.SaveRevision}-{language}-tab{i}-{end}.png"), PngBitmapEncoderOptions.Default); }
                }
            }
            vm.SaveCurrentCommand.Execute(null); Assert.False(vm.HasError, vm.Error);
            foreach (var b in save.AllBlocks) { Assert.Equal(before[b.Key].Type, b.Type); Assert.Equal(before[b.Key].Data, b.Data.ToArray()); }
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
