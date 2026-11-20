using PKHeX.Core;
using PKHeX.Presentation.ViewModels;

namespace PKHeX.Avalonia.Tests;

/// <summary>
/// Super Training fields beyond bags, regular stages and record times: distribution stages, the
/// counter, the tutorial tracker, clearing one record, and clearing the whole block.
/// </summary>
public class SuperTrainingExtrasTests
{
    [Fact]
    public void Distributions_LoadAndWriteThrough()
    {
        var sav = new SAV6XY();
        var vm = new SuperTrainingEditorViewModel(sav);

        Assert.Equal(SuperTrainDistributionViewModel.Count, vm.Distributions.Count);
        vm.Distributions[2].IsUnlocked = true;

        Assert.True(sav.SuperTrain.GetIsDistributionUnlocked(2));
    }

    [Fact]
    public void UnlockAll_RefreshesDistributions()
    {
        var vm = new SuperTrainingEditorViewModel(new SAV6AO());

        vm.UnlockAllCommand.Execute(null);

        Assert.All(vm.Distributions, d => Assert.True(d.IsUnlocked));
    }

    [Fact]
    public void CounterAndTutorial_AreClampedAndWritten()
    {
        var sav = new SAV6XY();
        var vm = new SuperTrainingEditorViewModel(sav);

        vm.Counter = 50;
        vm.TutorialIndex = 4;

        Assert.Equal(10, sav.SuperTrain.Counter);
        Assert.Equal(4u, sav.SuperTrain.TutorialIndex);
    }

    [Fact]
    public void ClearRecord1_ZeroesOnlyTheFirstRecord()
    {
        var sav = new SAV6XY();
        var block = sav.SuperTrain;
        block.SetTime1(0, 12.5f);
        block.SetTime2(0, 20f);
        block.GetHolder1(0).Species = (ushort)Species.Pikachu;
        var vm = new SuperTrainingEditorViewModel(sav);

        vm.Records[0].ClearRecord1Command.Execute(null);

        Assert.Equal(0f, block.GetTime1(0));
        Assert.Equal(0, block.GetHolder1(0).Species);
        Assert.Equal(20f, block.GetTime2(0));
        Assert.Equal(0f, vm.Records[0].Time1);
    }

    [Fact]
    public void ClearAllData_ResetsBlockAndReloadsViews()
    {
        var sav = new SAV6XY();
        sav.SuperTrain.UnlockAllStages(true);
        sav.SuperTrain.SetBag(0, 3);
        var vm = new SuperTrainingEditorViewModel(sav);

        vm.ClearAllDataCommand.Execute(null);

        Assert.False(sav.SuperTrain.GetIsRegimenUnlocked(0));
        Assert.All(vm.Stages, s => Assert.False(s.IsUnlocked));
        Assert.All(vm.Distributions, d => Assert.False(d.IsUnlocked));
        Assert.Equal(0, vm.Bags[0].BagId);
    }
}
