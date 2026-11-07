using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.VisualTree;
using Moq;
using PKHeX.Avalonia.Tests.Harness;
using PKHeX.Avalonia.Views;
using PKHeX.Core;
using PKHeX.Presentation.Localization;
using PKHeX.Presentation.ViewModels;

namespace PKHeX.Avalonia.Tests;

public class EncounterDatabaseFilterTests
{
    private static EncounterDatabaseViewModel Create(SaveFile sav, Action<PKM>? onSelect = null) =>
        new(sav, Mock.Of<ISpriteRenderer>(), Mock.Of<IDialogService>(), onSelect ?? (_ => { }));

    private static SaveFile Violet() => BlankSaveFile.Get(GameVersion.VL);

    [Fact]
    public async Task SpeciesSearchReportsCountAndUsesLocalizedTypes()
    {
        var vm = Create(Violet());
        vm.SelectedSpecies = (ushort)Species.Pikachu;
        await vm.SearchCommand.ExecuteAsync(null);

        Assert.NotEmpty(vm.Results);
        var key = vm.IsTruncated ? "EncounterDatabase_ResultCountTruncated" : "EncounterDatabase_ResultCount";
        Assert.Equal(LocalizedStrings.Instance.Format(key, vm.Results.Count), vm.StatusText);
        Assert.True(vm.Results.Count <= EncounterDatabaseViewModel.MaxResults);
        Assert.All(vm.Results, r => Assert.Equal(LocalizedStrings.Instance[$"EncounterDatabase_Type_{EncounterDatabaseViewModel.GetGroup(r.Encounter)}"], r.Type));
    }

    [Fact]
    public async Task VersionListAndAnyGameSearchSkipVersionsWithoutContext()
    {
        // The HOME version range reaches reserved IDs (Winds/Waves) that have no entity context.
        // Core's generator cannot search them, so "Any game" must not pass them on and the picker
        // must not offer them. Gen 3 Switch transfers (FRX/LGX) do have a context and stay listed.
        var vm = Create(Violet());
        var listed = vm.VersionList.Where(v => v.Value != 0).Select(v => (GameVersion)v.Value).ToList();

        Assert.DoesNotContain(GameVersion.WI, listed);
        Assert.DoesNotContain(GameVersion.WA, listed);
        Assert.All(listed, v => Assert.NotEqual(EntityContext.None, v.Context));
        Assert.Contains(GameVersion.FRX, listed);
        Assert.Contains(GameVersion.LGX, listed);

        vm.SelectedSpecies = (ushort)Species.Pikachu;
        await vm.SearchCommand.ExecuteAsync(null);

        Assert.NotEmpty(vm.Results);
    }

    [Fact]
    public async Task CombinedVersionTypeAndLevelFiltersNarrowResults()
    {
        var vm = Create(Violet());
        vm.SelectedSpecies = (ushort)Species.Pikachu;
        vm.SelectedVersion = (int)GameVersion.VL;
        foreach (var t in vm.TypeFilters) t.IsChecked = t.Group == EncounterTypeGroup.Slot;
        vm.LevelMin = 10;
        vm.LevelMax = 30;
        await vm.SearchCommand.ExecuteAsync(null);

        Assert.NotEmpty(vm.Results);
        Assert.All(vm.Results, r =>
        {
            Assert.Equal(EncounterTypeGroup.Slot, EncounterDatabaseViewModel.GetGroup(r.Encounter));
            Assert.True(r.Encounter.Version is GameVersion.VL or GameVersion.SV, r.Encounter.Version.ToString());
            Assert.True(r.Encounter.LevelMax >= 10 && r.Encounter.LevelMin <= 30);
        });
    }

    [Fact]
    public async Task MoveOnlySearchWithAnySpeciesFindsMoveCompatibleEncounters()
    {
        var vm = Create(new SAV3E());
        vm.Move1 = (ushort)Move.VoltTackle;
        Assert.Equal(0, vm.SelectedSpecies);
        Assert.True(vm.SearchCommand.CanExecute(null));
        await vm.SearchCommand.ExecuteAsync(null);

        Assert.NotEmpty(vm.Results);
        // Smeargle can Sketch any move, so Core needs nothing from its encounters (upstream behaves the same).
        Assert.All(vm.Results, r => Assert.Contains(r.Encounter.Species,
            new ushort[] { (ushort)Species.Pichu, (ushort)Species.Pikachu, (ushort)Species.Raichu, (ushort)Species.Smeargle }));
        Assert.Contains(vm.Results, r => r.Encounter.Species == (ushort)Species.Pichu);
    }

    [Fact]
    public async Task ZeroMatchesShowsExplanation()
    {
        var vm = Create(Violet());
        vm.SelectedSpecies = (ushort)Species.Pikachu;
        foreach (var t in vm.TypeFilters) t.IsChecked = t.Group == EncounterTypeGroup.Trade;
        vm.LevelMin = vm.LevelMax = 100;
        await vm.SearchCommand.ExecuteAsync(null);

        Assert.Empty(vm.Results);
        Assert.Equal(LocalizedStrings.Instance["EncounterDatabase_NoResults"], vm.StatusText);
    }

    [Fact]
    public async Task ShinyAndLocationFiltersApply()
    {
        var vm = Create(Violet());
        vm.SelectedSpecies = (ushort)Species.Pikachu;
        await vm.SearchCommand.ExecuteAsync(null);
        var location = vm.Results.First(r => r.Location.Length > 3).Location;

        vm.LocationText = location[..3].ToUpperInvariant();
        vm.ShinyFilter = false;
        await vm.SearchCommand.ExecuteAsync(null);
        Assert.NotEmpty(vm.Results);
        Assert.All(vm.Results, r =>
        {
            Assert.Contains(location[..3], r.Location, StringComparison.CurrentCultureIgnoreCase);
            Assert.False(r.Encounter.IsShiny);
        });
    }

    [Fact]
    public void SearchNeedsATypeAndSpeciesOrMove()
    {
        var vm = Create(Violet());
        Assert.False(vm.SearchCommand.CanExecute(null));
        vm.SelectedSpecies = 25;
        Assert.True(vm.SearchCommand.CanExecute(null));
        foreach (var t in vm.TypeFilters) t.IsChecked = false;
        Assert.False(vm.SearchCommand.CanExecute(null));

        vm.ResetFiltersCommand.Execute(null);
        Assert.Equal(0, vm.SelectedSpecies);
        Assert.All(vm.TypeFilters, t => Assert.True(t.IsChecked));
        Assert.Equal((1, 100), (vm.LevelMin, vm.LevelMax));
        Assert.Null(vm.ShinyFilter);
    }

    [Fact]
    public async Task SelectingOnlyInspectsAndLoadIsExplicit()
    {
        PKM? loaded = null;
        var vm = Create(Violet(), pk => loaded = pk);
        vm.SelectedSpecies = (ushort)Species.Pikachu;
        await vm.SearchCommand.ExecuteAsync(null);
        Assert.False(vm.LoadSelectedCommand.CanExecute(null));

        vm.SelectedResult = vm.Results[0];
        Assert.True(vm.HasSelectedResult);
        Assert.Null(loaded);

        await vm.LoadSelectedCommand.ExecuteAsync(null);
        Assert.Equal(vm.Results[0].Encounter.Species, loaded?.Species); // may be a pre-evolution encounter such as Pichu
        Assert.Equal(LocalizedStrings.Instance.Format("EncounterDatabase_Loaded", vm.Results[0].Species), vm.StatusText);
    }

    [Fact]
    public async Task CancelStopsALongAnySpeciesSearch()
    {
        var vm = Create(Violet());
        vm.Move1 = (ushort)Move.Tackle;
        var search = vm.SearchCommand.ExecuteAsync(null);
        Assert.True(vm.CancelSearchCommand.CanExecute(null));
        vm.CancelSearchCommand.Execute(null);
        await search;
        Assert.False(vm.IsSearching);
        Assert.Equal(LocalizedStrings.Instance["EncounterDatabase_Cancelled"], vm.StatusText);
    }

    [AvaloniaTheory]
    [InlineData(900, 700)]
    [InlineData(650, 540)]
    public async Task ViewKeepsFiltersSearchAndLoadReachable(double width, double height)
    {
        using var app = new HeadlessAppFixture();
        var vm = Create(Violet());
        vm.SelectedSpecies = (ushort)Species.Pikachu;
        await vm.SearchCommand.ExecuteAsync(null);
        vm.SelectedResult = vm.Results[0];
        var view = new EncounterDatabaseView { DataContext = vm, Width = width, Height = height };
        app.Window.Content = view;
        app.Window.Width = width;
        app.Window.Height = height;
        app.Pump();

        var buttons = view.GetVisualDescendants().OfType<Button>().Where(b => b.IsEffectivelyVisible).ToList();
        foreach (var key in new[] { "EncounterDatabaseView_Search", "EncounterDatabase_LoadIntoEditor", "EncounterDatabase_ResetFilters" })
        {
            var button = Assert.Single(buttons, b => Equals(b.Content, LocalizedStrings.Instance[key]));
            var origin = button.TranslatePoint(default, view)!.Value;
            Assert.True(origin.X >= 0 && origin.X + button.Bounds.Width <= width + 0.5, $"{key} clipped at {origin}");
            Assert.True(origin.Y >= 0 && origin.Y + button.Bounds.Height <= height + 0.5, $"{key} clipped at {origin}");
        }
        Assert.Contains(view.GetVisualDescendants().OfType<TextBlock>(), t => t.IsEffectivelyVisible && t.Text == vm.StatusText);
    }
}
