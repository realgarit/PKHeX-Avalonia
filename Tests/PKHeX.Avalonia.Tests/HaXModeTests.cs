using System.Text.Json;
using PKHeX.Application.Services;
using PKHeX.Core;
using PKHeX.Presentation.ViewModels;

namespace PKHeX.Avalonia.Tests;

public sealed class HaXModeTests
{
    [Fact]
    public void StartupArgument_EnablesHaXMode()
    {
        var settings = new AppSettings();

        var startup = StartupUtil.FormLoadInitialActions(
            new[] { "--HaX" },
            settings,
            new Version(1, 0, 0));

        Assert.True(startup.HaX);
    }

    [Fact]
    public void RuntimeHaXState_IsNotPersisted()
    {
        var settings = new AppSettings { IsHaXMode = true };

        var restored = JsonSerializer.Deserialize<AppSettings>(JsonSerializer.Serialize(settings));

        Assert.NotNull(restored);
        Assert.False(restored!.IsHaXMode);
    }

    [Fact]
    public void HaXEditor_UsesUnrestrictedSources_AndPersistsHackedStats()
    {
        var sav = new SAV9SV();
        GameInfo.FilteredSources = new FilteredGameDataSource(sav, GameInfo.Sources, HaX: true);

        var pkm = new PK9 { Species = 906, CurrentLevel = 50 };
        var (vm, _, _) = TestHelpers.CreateTestViewModel(pkm, sav, haXMode: true);

        Assert.True(vm.IsHaXMode);
        Assert.Contains(vm.SpeciesList, item => item.Value == sav.MaxSpeciesID);
        Assert.Contains(vm.MoveList, item => item.Value == sav.MaxMoveID);
        Assert.Equal(GameInfo.FilteredSources.Abilities.Count, vm.AbilityList.Count);

        vm.HaXStatHP = ushort.MaxValue;
        vm.HaXStatATK = 60000;
        vm.HaXStatDEF = 50000;
        vm.HaXStatSPA = 40000;
        vm.HaXStatSPD = 30000;
        vm.HaXStatSPE = 20000;
        vm.IvHP = 31; // A normal editor change must not recalculate away hacked stats.

        var result = vm.PreparePKM();

        Assert.Equal(ushort.MaxValue, result.Stat_HPMax);
        Assert.Equal(60000, result.Stat_ATK);
        Assert.Equal(50000, result.Stat_DEF);
        Assert.Equal(40000, result.Stat_SPA);
        Assert.Equal(30000, result.Stat_SPD);
        Assert.Equal(20000, result.Stat_SPE);
    }

    [Fact]
    public void NormalEditor_DoesNotPersistHaXStatEdits()
    {
        var sav = new SAV3E();
        GameInfo.FilteredSources = new FilteredGameDataSource(sav, GameInfo.Sources);

        var pkm = new PK3 { Species = 25, CurrentLevel = 50 };
        var (vm, _, _) = TestHelpers.CreateTestViewModel(pkm, sav);

        vm.HaXStatHP = ushort.MaxValue;
        var result = vm.PreparePKM();

        Assert.NotEqual(ushort.MaxValue, result.Stat_HPMax);
    }

    [Fact]
    public void HaXFilteredSource_ExpandsSpeciesMovesAndItems()
    {
        var sav = new SAV9SV();
        var legal = new FilteredGameDataSource(sav, GameInfo.Sources);
        var hax = new FilteredGameDataSource(sav, GameInfo.Sources, HaX: true);

        Assert.True(hax.Species.Count >= legal.Species.Count);
        Assert.True(hax.Moves.Count >= legal.Moves.Count);
        Assert.True(hax.Items.Count >= legal.Items.Count);
    }
}
