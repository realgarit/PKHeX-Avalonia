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
}
