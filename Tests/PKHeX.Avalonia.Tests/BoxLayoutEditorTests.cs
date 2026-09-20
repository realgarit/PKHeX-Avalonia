using PKHeX.Core;
using PKHeX.Presentation.ViewModels;
using Xunit;

namespace PKHeX.Avalonia.Tests;

public sealed class BoxLayoutEditorTests
{
    [Fact]
    public void Cancel_DoesNotMutateLiveSave()
    {
        var sav = new SAV4Pt();
        var names = (IBoxDetailName)sav;
        var wallpaper = (IBoxDetailWallpaper)sav;
        names.SetBoxName(0, "Original");
        wallpaper.SetBoxWallpaper(0, 3);
        sav.State.Edited = false;
        var editedBefore = sav.State.Edited;

        var vm = new BoxLayoutEditorViewModel(sav);
        vm.Boxes[0].Name = "Temporary";
        vm.Boxes[0].Wallpaper = 7;
        vm.UnlockedBoxes = Math.Min(sav.BoxCount, 1);
        vm.CancelCommand.Execute(null);

        Assert.Equal("Original", names.GetBoxName(0));
        Assert.Equal(3, wallpaper.GetBoxWallpaper(0));
        Assert.Equal(editedBefore, sav.State.Edited);
    }

    [Fact]
    public void Save_CommitsWorkingCopyAndRejectsInvalidWallpaper()
    {
        var sav = new SAV4Pt();
        var wallpaper = (IBoxDetailWallpaper)sav;
        wallpaper.SetBoxWallpaper(0, 3);
        sav.State.Edited = false;

        var vm = new BoxLayoutEditorViewModel(sav);
        vm.Boxes[0].Name = "Commit";
        vm.Boxes[0].Wallpaper = -1;
        vm.SaveCommand.Execute(null);

        Assert.Equal("Commit", ((IBoxDetailName)sav).GetBoxName(0));
        Assert.Equal(3, wallpaper.GetBoxWallpaper(0));
        Assert.True(sav.State.Edited);
    }

    [Fact]
    public void Reorder_MovesPokemonNamesAndWallpapersTogetherOnSave()
    {
        var sav = new SAV4Pt();
        var names = (IBoxDetailName)sav;
        var wallpapers = (IBoxDetailWallpaper)sav;
        sav.SetBoxSlotAtIndex(new PK4 { Species = (ushort)Species.Bulbasaur, CurrentLevel = 5 }, 0, 0);
        sav.SetBoxSlotAtIndex(new PK4 { Species = (ushort)Species.Charmander, CurrentLevel = 5 }, 1, 0);
        names.SetBoxName(0, "First");
        names.SetBoxName(1, "Second");
        wallpapers.SetBoxWallpaper(0, 2);
        wallpapers.SetBoxWallpaper(1, 7);

        var vm = new BoxLayoutEditorViewModel(sav);
        vm.MoveDownCommand.Execute(vm.Boxes[0]);

        Assert.Equal("Second", vm.Boxes[0].Name);
        Assert.Equal("First", vm.Boxes[1].Name);
        Assert.Equal(7, vm.Boxes[0].Wallpaper);
        Assert.Equal(2, vm.Boxes[1].Wallpaper);

        vm.SaveCommand.Execute(null);

        Assert.Equal((ushort)Species.Charmander, sav.GetBoxSlotAtIndex(0, 0).Species);
        Assert.Equal((ushort)Species.Bulbasaur, sav.GetBoxSlotAtIndex(1, 0).Species);
        Assert.Equal("Second", names.GetBoxName(0));
        Assert.Equal("First", names.GetBoxName(1));
        Assert.Equal(7, wallpapers.GetBoxWallpaper(0));
        Assert.Equal(2, wallpapers.GetBoxWallpaper(1));
    }
}
