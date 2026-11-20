using PKHeX.Core;
using PKHeX.Presentation.ViewModels;

namespace PKHeX.Avalonia.Tests;

/// <summary>
/// Gen 6 trainer fields added to the generic trainer form: PSS messages, Game Sync ID, Mega
/// Evolution unlocks and Battle Maison streaks.
/// </summary>
public class Gen6TrainerExtrasTests
{
    [Fact]
    public void XY_ShowsStatusAndMaison_ButNotMegaRayquaza()
    {
        using var vm = new TrainerEditorViewModel(new SAV6XY());

        Assert.True(vm.HasGen6Status);
        Assert.True(vm.HasMaison);
        Assert.False(vm.HasMegaRayquaza);
        Assert.Equal(5, vm.MaisonRows.Count);
    }

    [Fact]
    public void ORASDemo_HasStatusButNoMaison()
    {
        using var vm = new TrainerEditorViewModel(new SAV6AODemo());

        Assert.True(vm.HasGen6Status);
        Assert.False(vm.HasMaison);
        Assert.Empty(vm.MaisonRows);
    }

    [Fact]
    public void OtherGenerations_HideTheGen6Fields()
    {
        using var vm = new TrainerEditorViewModel(new SAV5B2W2());

        Assert.False(vm.HasGen6Status);
        Assert.False(vm.HasMaison);
        Assert.True(vm.IsGameSyncIdValid);
    }

    [Fact]
    public void Save_WritesEditedGen6Fields()
    {
        var sav = new SAV6AO();
        using var vm = new TrainerEditorViewModel(sav);

        vm.PssMessage1 = "Hello there";
        vm.IsMegaEvolutionUnlocked = true;
        vm.IsMegaRayquazaUnlocked = true;
        vm.GameSyncId = "0123456789abcdef";
        var single = vm.MaisonRows.Single(r => r.Style == BattleStyle6.Single);
        single.SuperBest = 50;
        vm.SaveCommand.Execute(null);

        Assert.Equal("Hello there", sav.Status.Saying1);
        Assert.True(sav.Status.IsMegaEvolutionUnlocked);
        Assert.True(sav.Status.IsMegaRayquazaUnlocked);
        Assert.Equal("0123456789ABCDEF", sav.Status.GameSyncID);
        Assert.Equal(50, sav.Maison.GetMaisonStat(BattleStyle6.Single, streak: true, super: true));
        Assert.Equal(0, sav.Maison.GetMaisonStat(BattleStyle6.Single, streak: false, super: true));
    }

    [Theory]
    [InlineData("0123")]
    [InlineData("0123456789ABCDEG")]
    public void InvalidGameSyncId_BlocksSave(string value)
    {
        using var vm = new TrainerEditorViewModel(new SAV6XY());

        vm.GameSyncId = value;

        Assert.False(vm.IsGameSyncIdValid);
        Assert.False(vm.SaveCommand.CanExecute(null));
    }
}
