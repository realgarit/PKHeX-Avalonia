using Avalonia.Controls;
using Avalonia.Automation;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Moq;
using PKHeX.Core;
using PKHeX.Presentation.ViewModels;
using PKHeX.Avalonia.Views;

namespace PKHeX.Avalonia.Tests;

/// <summary>
/// Gen 6/7 3DS region settings and Vivillon pattern editing (issue #535): the Gen 6 trainer
/// form, the Gen 7 trainer workspace and PK6/PK7 entities in the Pokémon editor.
/// </summary>
public class RegionSettingsTests
{
    private const int UnitedStates = 49, Germany = 78, DistrictOfColumbia = 2, Americas = 1, Europe = 2;

    private static SAV6 LoadGen6(string file) =>
        Assert.IsAssignableFrom<SAV6>(Fixtures.SaveFileFixture.LoadSave(Path.Combine(Fixtures.SaveFileFixture.FindSaveFilesPath()!, file)));

    [Theory]
    [InlineData("gen6_x.main")]
    [InlineData("gen6_or.main")]
    public void Gen6TrainerLoadsEditsAndRoundtripsRegionAndVivillon(string file)
    {
        var save = LoadGen6(file);
        using var vm = new TrainerEditorViewModel(save);
        Assert.True(vm.HasRegionSettings);
        Assert.True(vm.HasVivillon);
        Assert.Equal(save.Country, vm.Country);
        Assert.Equal(save.Region, vm.SubRegion);
        Assert.Equal(save.ConsoleRegion, vm.ConsoleRegion);
        Assert.Equal(save.Vivillon, vm.Vivillon);
        Assert.Contains(vm.CountryList, item => item.Value == vm.Country);
        Assert.Contains(vm.SubRegionList, item => item.Value == vm.SubRegion);
        Assert.Contains(vm.ConsoleRegionList, item => item.Value == vm.ConsoleRegion);
        Assert.Equal(20, vm.VivillonList.Count(item => item.Value is >= 0 and <= 19));

        // Edits stay in the form until Apply.
        var before = save.Data.ToArray();
        vm.Country = UnitedStates; vm.SubRegion = DistrictOfColumbia; vm.ConsoleRegion = Americas; vm.Vivillon = 7;
        Assert.Equal(before, save.Data.ToArray());
        Assert.True(vm.SaveCommand.CanExecute(null));
        vm.SaveCommand.Execute(null);
        Assert.Equal(UnitedStates, save.Country);
        Assert.Equal(DistrictOfColumbia, save.Region);
        Assert.Equal(Americas, save.ConsoleRegion);
        Assert.Equal(7, save.Vivillon);

        var reopened = Assert.IsAssignableFrom<SAV6>(SaveUtil.GetSaveFile(save.Write()));
        Assert.Equal(UnitedStates, reopened.Country);
        Assert.Equal(DistrictOfColumbia, reopened.Region);
        Assert.Equal(Americas, reopened.ConsoleRegion);
        Assert.Equal(7, reopened.Vivillon);
        using var check = new TrainerEditorViewModel(reopened);
        Assert.Equal(UnitedStates, check.Country);
        Assert.Equal(DistrictOfColumbia, check.SubRegion);
        Assert.Equal(7, check.Vivillon);
    }

    [Fact]
    public void Gen6TrainerNoOpApplyKeepsRegionAndVivillonBytes()
    {
        var save = LoadGen6("gen6_x.main");
        var region = save.Status.Data.ToArray();
        var vivillon = save.Vivillon;
        using var vm = new TrainerEditorViewModel(save);
        vm.SaveCommand.Execute(null);
        Assert.Equal(region, save.Status.Data.ToArray());
        Assert.Equal(vivillon, save.Vivillon);
    }

    [Fact]
    public void CountryChangeReloadsSubRegionsAndResetsAnUndefinedSubRegion()
    {
        var save = new SAV6XY();
        using var vm = new TrainerEditorViewModel(save);
        vm.Country = UnitedStates;
        Assert.Contains(vm.SubRegionList, item => item.Value == DistrictOfColumbia);
        vm.SubRegion = DistrictOfColumbia;
        var usNames = vm.SubRegionList.Select(item => item.Text).ToArray();

        vm.Country = Germany;
        Assert.NotEqual(usNames, vm.SubRegionList.Select(item => item.Text).ToArray());
        Assert.Contains(vm.SubRegionList, item => item.Value == vm.SubRegion);
        Assert.DoesNotContain(vm.SubRegionList, item => item.Text.StartsWith("Unknown", StringComparison.Ordinal));

        // A sub-region ID Germany does not define falls back to "—" instead of a stale US ID.
        vm.Country = UnitedStates; vm.SubRegion = 52;
        vm.Country = Germany;
        Assert.True(RegionChoices.IsKnownSubRegion(Germany, vm.SubRegion));
    }

    [Fact]
    public void Gen6UnknownStoredValuesAreShownAndPreserved()
    {
        var save = new SAV6XY { Country = 250, Region = 201, ConsoleRegion = 99, Vivillon = 40 };
        var status = save.Status.Data.ToArray();
        using var vm = new TrainerEditorViewModel(save);
        Assert.Equal(250, vm.Country); Assert.Equal(201, vm.SubRegion); Assert.Equal(99, vm.ConsoleRegion); Assert.Equal(40, vm.Vivillon);
        Assert.Contains(vm.CountryList, item => item.Value == 250);
        Assert.Contains(vm.SubRegionList, item => item.Value == 201);
        Assert.Contains(vm.ConsoleRegionList, item => item.Value == 99);
        Assert.Contains(vm.VivillonList, item => item.Value == 40);
        Assert.True(vm.SaveCommand.CanExecute(null));
        vm.SaveCommand.Execute(null);
        Assert.Equal(status, save.Status.Data.ToArray());
        Assert.Equal(40, save.Vivillon);

        // A new value outside the known patterns is rejected; the stored unknown stays valid.
        vm.Vivillon = 30;
        Assert.False(vm.SaveCommand.CanExecute(null));
        vm.Vivillon = 40;
        Assert.True(vm.SaveCommand.CanExecute(null));
    }

    [Fact]
    public void Gen6DemoSaveHidesUnusedVivillon()
    {
        using var vm = new TrainerEditorViewModel(new SAV6AODemo());
        Assert.True(vm.HasRegionSettings);
        Assert.False(vm.HasVivillon);
    }

    [Fact]
    public void Gen7TrainerWorkspaceOwnsRegionSettingsInsteadOfGenericForm()
    {
        using var vm = new TrainerEditorViewModel(new SAV7SM());
        Assert.True(vm.IsGen7);
        Assert.False(vm.HasRegionSettings);
        Assert.False(vm.HasVivillon);
    }

    [Theory]
    [InlineData("gen7_sun.main")]
    [InlineData("gen7_ultrasun.main")]
    public void Gen7VivillonEditKeepsSharedByteBitsAndRoundtrips(string file)
    {
        var save = Assert.IsAssignableFrom<SAV7>(Fixtures.SaveFileFixture.LoadSave(Path.Combine(Fixtures.SaveFileFixture.FindSaveFilesPath()!, file)));
        save.Misc.Data[0x130] = (byte)(0xA0 | save.Misc.Vivillon);
        var before = save.Data.ToArray();
        using var vm = new Misc7EditorViewModel(save);
        Assert.Equal(save.Misc.Vivillon, vm.Vivillon);
        Assert.Contains(vm.VivillonPatterns, item => item.Value == vm.Vivillon);

        vm.SaveCommand.Execute(null);
        Assert.False(vm.HasError, vm.Error);
        Assert.Equal(before, save.Data.ToArray());

        vm.Vivillon = 12;
        Assert.Equal(before, save.Data.ToArray());
        vm.SaveCommand.Execute(null);
        Assert.False(vm.HasError, vm.Error);
        Assert.Equal(12, save.Misc.Vivillon);
        Assert.Equal(0xA0, save.Misc.Data[0x130] & 0xE0);

        var reopened = Assert.IsAssignableFrom<SAV7>(SaveUtil.GetSaveFile(save.Write()));
        Assert.Equal(12, reopened.Misc.Vivillon);
        using var check = new Misc7EditorViewModel(reopened);
        Assert.Equal(12, check.Vivillon);
    }

    [Fact]
    public void Gen7UnknownVivillonIsShownAndPreserved()
    {
        var save = new SAV7USUM();
        save.Misc.Vivillon = 27;
        var before = save.Data.ToArray();
        using var vm = new Misc7EditorViewModel(save);
        Assert.Equal(27, vm.Vivillon);
        Assert.Contains(vm.VivillonPatterns, item => item.Value == 27);
        Assert.True(vm.CanSave);
        vm.SaveCommand.Execute(null);
        Assert.Equal(before, save.Data.ToArray());
        vm.Vivillon = 25;
        Assert.False(vm.CanSave);
    }

    [Fact]
    public void Gen7CountryChangeResetsUndefinedSubRegion()
    {
        using var vm = new Misc7EditorViewModel(new SAV7SM());
        vm.Country = UnitedStates; vm.SubRegion = 52;
        vm.Country = Germany;
        Assert.True(RegionChoices.IsKnownSubRegion(Germany, vm.SubRegion));
        Assert.Contains(vm.Regions, item => item.Value == vm.SubRegion);
    }

    public static IEnumerable<object[]> EntityFormats => [[GameVersion.X], [GameVersion.OR], [GameVersion.SN], [GameVersion.US]];

    [Theory]
    [MemberData(nameof(EntityFormats))]
    public void EntityRegionOriginLoadsEditsAndPrepares(GameVersion version)
    {
        var save = BlankSaveFile.Get(version);
        var pk = save.BlankPKM;
        pk.Species = (ushort)Species.Vivillon;
        pk.Version = version;
        var origin = Assert.IsAssignableFrom<IRegionOrigin>(pk);
        origin.Country = UnitedStates; origin.Region = DistrictOfColumbia; origin.ConsoleRegion = Americas;
        var vm = Create(pk, save);
        Assert.True(vm.HasRegionOrigin);
        Assert.Equal(UnitedStates, vm.OriginCountry);
        Assert.Equal(DistrictOfColumbia, vm.OriginSubRegion);
        Assert.Equal(Americas, vm.OriginConsoleRegion);
        Assert.Contains(vm.OriginSubRegionList, item => item.Value == DistrictOfColumbia);

        var unchanged = vm.PreparePKM();
        Assert.Equal(new GeoRegion3DS(Americas, UnitedStates, DistrictOfColumbia), ((IRegionOrigin)unchanged).GetRegionOrigin());

        var usNames = vm.OriginSubRegionList.Select(item => item.Text).ToArray();
        vm.OriginCountry = Germany;
        Assert.NotEqual(usNames, vm.OriginSubRegionList.Select(item => item.Text).ToArray());
        Assert.True(RegionChoices.IsKnownSubRegion(Germany, vm.OriginSubRegion));
        var german = vm.OriginSubRegionList.First(item => item.Value > 0).Value;
        vm.OriginSubRegion = german;
        vm.OriginConsoleRegion = Europe;
        var prepared = Assert.IsAssignableFrom<IRegionOrigin>(vm.PreparePKM());
        Assert.Equal(new GeoRegion3DS(Europe, Germany, (byte)german), prepared.GetRegionOrigin());
    }

    [Fact]
    public void EntityUnknownRegionValuesSurvivePrepare()
    {
        var save = BlankSaveFile.Get(GameVersion.SN);
        var pk = new PK7 { Species = 25, Version = GameVersion.SN, Country = 251, Region = 199, ConsoleRegion = 77 };
        var vm = Create(pk, save);
        Assert.Contains(vm.OriginCountryList, item => item.Value == 251);
        Assert.Contains(vm.OriginSubRegionList, item => item.Value == 199);
        Assert.Contains(vm.OriginConsoleRegionList, item => item.Value == 77);
        var prepared = Assert.IsType<PK7>(vm.PreparePKM());
        Assert.Equal(new GeoRegion3DS(77, 251, 199), prepared.GetRegionOrigin());
    }

    [Theory]
    [InlineData(GameVersion.GP)]
    [InlineData(GameVersion.SW)]
    [InlineData(GameVersion.Pt)]
    public void EntitiesWithoutRegionOriginHideTheSection(GameVersion version)
    {
        var save = BlankSaveFile.Get(version);
        var pk = save.BlankPKM;
        pk.Species = 25;
        var vm = Create(pk, save);
        Assert.False(vm.HasRegionOrigin);
    }

    [AvaloniaFact]
    public void Gen6TrainerViewBindsRegionAndVivillonSelections()
    {
        var save = new SAV6XY { Country = UnitedStates, Region = DistrictOfColumbia, ConsoleRegion = Americas, Vivillon = 7 };
        using var vm = new TrainerEditorViewModel(save);
        var view = new TrainerEditor { DataContext = vm };
        var window = new Window { Content = view, Width = 900, Height = 700 };
        using var lifetime = new HeadlessWindowLifetime(window);
        window.Show(); Pump(window);

        ComboBox Find(string key)
        {
            var name = Presentation.Localization.LocalizedStrings.Instance[key];
            return view.GetVisualDescendants().OfType<ComboBox>().Single(box => AutomationProperties.GetName(box) == name && box.IsEffectivelyVisible);
        }
        Assert.Equal(UnitedStates, Find("Trainer7_Country").SelectedValue);
        Assert.Equal(DistrictOfColumbia, Find("Trainer7_SubRegion").SelectedValue);
        Assert.Equal(Americas, Find("Trainer7_ConsoleRegion").SelectedValue);
        Assert.Equal(7, Find("RegionSettings_Vivillon").SelectedValue);

        vm.Country = Germany; Pump(window);
        Assert.Same(vm.SubRegionList, Find("Trainer7_SubRegion").ItemsSource);
        Assert.Equal(vm.SubRegion, Find("Trainer7_SubRegion").SelectedValue);
    }

    private static PokemonEditorViewModel Create(PKM pk, SaveFile save)
    {
        GameInfo.FilteredSources = new FilteredGameDataSource(save, GameInfo.Sources);
        return new PokemonEditorViewModel(pk, save, Mock.Of<ISpriteRenderer>(), Mock.Of<IDialogService>(), Mock.Of<IWindowService>());
    }

    private static void Pump(Window window)
    { Dispatcher.UIThread.RunJobs(); window.UpdateLayout(); AvaloniaHeadlessPlatform.ForceRenderTimerTick(); Dispatcher.UIThread.RunJobs(); }
}
