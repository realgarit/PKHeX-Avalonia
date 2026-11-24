using PKHeX.Core;
using PKHeX.Presentation.ViewModels;

namespace PKHeX.Avalonia.Tests;

/// <summary>
/// Black/White's Entralink missions-completed counter and the Entrée Forest Delete All action.
/// Both stage on the editor's clone and reach the save only on Save.
/// </summary>
public class EntralinkExtrasTests
{
    [Fact]
    public void BlackWhite_MissionsComplete_RoundTripsOnSave()
    {
        var sav = new SAV5BW();
        sav.Entralink.MissionsComplete = 7;
        var vm = new EntralinkEditorViewModel(sav);

        Assert.True(vm.IsBW);
        Assert.Equal(7, vm.MissionsComplete);

        vm.MissionsComplete = 42;
        Assert.Equal(7, sav.Entralink.MissionsComplete); // staged only
        vm.SaveCommand.Execute(null);

        Assert.Equal(42, sav.Entralink.MissionsComplete);
    }

    [Fact]
    public void DeleteAllSlots_EmptiesTheForestOnSave_AndCancelKeepsIt()
    {
        var sav = new SAV5BW();
        var forest = sav.EntreeForest;
        forest.Slots[0].Species = (ushort)Species.Pikachu;
        forest.EndAccess();

        var cancelled = new EntralinkEditorViewModel(sav);
        cancelled.DeleteAllSlotsCommand.Execute(null);
        cancelled.CancelCommand.Execute(null);
        Assert.Equal((ushort)Species.Pikachu, sav.EntreeForest.Slots[0].Species);
        sav.EntreeForest.EndAccess();

        var vm = new EntralinkEditorViewModel(sav);
        vm.DeleteAllSlotsCommand.Execute(null);
        Assert.All(vm.Areas.SelectMany(a => a.Slots), slot => Assert.Equal(0, slot.Species));
        vm.SaveCommand.Execute(null);

        Assert.Equal(0, sav.EntreeForest.Slots[0].Species);
    }
}
