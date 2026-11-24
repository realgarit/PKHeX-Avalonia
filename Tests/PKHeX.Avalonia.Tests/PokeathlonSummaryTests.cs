using PKHeX.Core;
using PKHeX.Presentation.ViewModels;

namespace PKHeX.Avalonia.Tests;

/// <summary>
/// The Pokéathlon score summary is derived from the block (course bests, medals, event bests, Fame)
/// and must follow edits made in the editor.
/// </summary>
public class PokeathlonSummaryTests
{
    [Fact]
    public void BlankSave_SummaryMatchesCore()
    {
        var sav = new SAV4HGSS();
        var vm = new PokeathlonEditorViewModel(sav);

        Assert.Equal(sav.Pokeathlon.CalculateGlobalScore(), vm.GlobalScore);
        Assert.Equal(Pokeathlon4.FriendshipTrophyThresholds.Length, vm.MaxFriendshipTrophies);
        Assert.Equal(PokeathlonGlobalCounters4.FameInclusiveThreshold.Length, vm.MaxFameLevel);
    }

    [Fact]
    public void GiveAllMedals_RaisesTheGlobalScore()
    {
        var sav = new SAV4HGSS();
        var vm = new PokeathlonEditorViewModel(sav);
        var before = vm.GlobalScore;

        vm.GiveAllMedalsCommand.Execute(null);

        Assert.True(vm.GlobalScore > before);
        Assert.Equal(sav.Pokeathlon.CalculateGlobalScore(), vm.GlobalScore);
        Assert.Equal(Pokeathlon4.CalculateFriendshipTrophyCount(vm.GlobalScore), vm.FriendshipTrophyCount);
    }

    [Fact]
    public void FameEdit_UpdatesFameLevel()
    {
        var vm = new PokeathlonEditorViewModel(new SAV4HGSS());

        vm.Fame = 1000;

        Assert.Equal(PokeathlonGlobalCounters4.GetFameLevel(1000), vm.FameLevel);
        Assert.True(vm.FameLevel > 0);
    }
}
