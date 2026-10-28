using System.Text;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Media.Imaging;
using Avalonia.VisualTree;
using Moq;
using PKHeX.Application.Abstractions;
using PKHeX.Application.Services;
using PKHeX.Avalonia.Services;
using PKHeX.Avalonia.Views;
using PKHeX.Core;
using PKHeX.Presentation.Localization;
using PKHeX.Presentation.ViewModels;

namespace PKHeX.Avalonia.Render.Tests;

/// <summary>
/// The Encounter Database keeps its status line in its own row above the results, and both level
/// fields show "100" in full. Both defects depend on real text metrics, so they are checked with Skia.
/// </summary>
public sealed class EncounterDatabaseLayoutRenderTests
{
    public static IEnumerable<object[]> Cases =>
        LocalizedStrings.SupportedLanguages.Select(language => new object[] { language, AppDensity.Compact, 650, 540 })
            .Append(["en", AppDensity.Compact, 900, 700])
            .Append(["de", AppDensity.Comfortable, 650, 540])
            .Append(["es", AppDensity.Comfortable, 650, 540])
            .Append(["pt-BR", AppDensity.Comfortable, 900, 700]);

    [AvaloniaTheory]
    [MemberData(nameof(Cases))]
    public async Task StatusStaysAboveResultsAndLevelsShowThreeDigits(string language, AppDensity density, int width, int height)
    {
        var previousLanguage = LocalizedStrings.Instance.CurrentLanguage;
        var densityService = new UiDensityService(new AppSettings(), Mock.Of<ISettingsStore>());
        var directory = Environment.GetEnvironmentVariable("PKHEX_HEADLESS_CAPTURE_DIR")
            ?? Path.Combine(AppContext.BaseDirectory, "TestResults");
        Directory.CreateDirectory(directory);
        var capture = Path.Combine(directory, $"encounter-layout-{language}-{density}-{width}x{height}");
        var diagnostics = new StringBuilder();
        Window? window = null;
        try
        {
            LocalizedStrings.Instance.SetLanguage(language);
            densityService.ApplyDensity(density);
            var vm = new EncounterDatabaseViewModel(BlankSaveFile.Get(GameVersion.VL), Mock.Of<ISpriteRenderer>(), Mock.Of<IDialogService>(), _ => { });
            vm.SelectedSpecies = (ushort)Species.Pikachu;
            await vm.SearchCommand.ExecuteAsync(null);
            vm.SelectedResult = vm.Results[0];
            // Set after the search: the filter only applies to the next search, and both fields now show "100".
            vm.LevelMin = 100;

            var view = new EncounterDatabaseView { DataContext = vm, Width = width, Height = height };
            window = new Window { Content = view, Width = width, Height = height };
            window.Show();
            await PKHeX.Testing.HeadlessRenderSettling.Settle(window);
            // Retain the real frame even when a geometry assertion fails on a CI platform.
            using (var frame = new RenderTargetBitmap(new PixelSize(width, height)))
            {
                frame.Render(window);
                frame.Save(capture + ".png", PngBitmapEncoderOptions.Default);
            }

            Rect InView(Visual visual) => new(visual.TranslatePoint(default, view)!.Value, visual.Bounds.Size);
            var filters = InView(view.GetVisualDescendants().OfType<Border>().Single(b => b.Classes.Contains("tool-search-surface")));
            var status = InView(view.GetVisualDescendants().OfType<TextBlock>().Single(t => t.IsEffectivelyVisible && t.Text == vm.StatusText));
            var surface = InView(view.GetVisualDescendants().OfType<Border>().Single(b => b.Classes.Contains("database-results-surface")));
            var list = view.GetVisualDescendants().OfType<ListBox>().Single();
            var item = list.GetVisualDescendants().OfType<ListBoxItem>().First();
            var itemInList = new Rect(item.TranslatePoint(default, list)!.Value, item.Bounds.Size);
            var load = InView(view.GetVisualDescendants().OfType<Button>()
                .Single(b => b.IsEffectivelyVisible && Equals(b.Content, LocalizedStrings.Instance["EncounterDatabase_LoadIntoEditor"])));
            diagnostics.AppendLine($"filters={filters} status={status} surface={surface} list={list.Bounds} item={itemInList} load={load}");

            Assert.True(filters.Bottom <= status.Top + 0.5, $"The status line {status} overlaps the filters {filters}.");
            Assert.True(status.Bottom <= surface.Top + 0.5, $"The results {surface} overlap the status line {status}.");
            Assert.True(surface.Bottom <= height + 0.5, $"The results {surface} extend below the view.");
            Assert.True(load.Top >= 0 && load.Bottom <= height + 0.5 && load.Right <= width + 0.5, $"Load into editor is clipped at {load}.");
            // Comfortable density is taller, so only compact density guarantees a full row at the minimum size.
            if (density == AppDensity.Compact)
                Assert.True(itemInList.Top >= 0 && itemInList.Bottom <= list.Bounds.Height + 0.5, $"The first result {itemInList} does not fit in the list {list.Bounds}.");

            foreach (var key in new[] { "EncounterDatabase_LevelMin", "EncounterDatabase_LevelMax" })
            {
                var input = view.GetVisualDescendants().OfType<NumericUpDown>()
                    .Single(control => AutomationProperties.GetName(control) == LocalizedStrings.Instance[key]);
                NumericTextFit.AssertFits(input, "100", key, diagnostics);
            }
        }
        finally
        {
            File.WriteAllText(capture + ".txt", diagnostics.ToString());
            window?.Close();
            densityService.ApplyDensity(AppDensity.Compact);
            LocalizedStrings.Instance.SetLanguage(previousLanguage);
        }
    }
}
