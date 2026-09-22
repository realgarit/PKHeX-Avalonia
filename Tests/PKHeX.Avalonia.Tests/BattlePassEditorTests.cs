using PKHeX.Core;
using PKHeX.Presentation.ViewModels;

namespace PKHeX.Avalonia.Tests;

public class BattlePassEditorTests
{
    [Fact]
    public void CancelDiscardsBattlePassChanges()
    {
        var source = new SAV4BR();
        var before = source.Data.ToArray();
        var vm = new BattlePassEditorViewModel(source);

        vm.SelectedPass!.Name = "Temporary";
        vm.UnlockAllCommand.Execute(null);
        vm.CancelCommand.Execute(null);

        Assert.Equal(before, source.Data.ToArray());
    }

    [Fact]
    public void SaveCommitsBattlePassChangesAndCloses()
    {
        var source = new SAV4BR();
        var closed = false;
        var vm = new BattlePassEditorViewModel(source)
        {
            CloseRequested = () => closed = true,
        };

        vm.SelectedPass!.Name = "Committed";
        vm.SaveCommand.Execute(null);

        Assert.Equal("Committed", source.BattlePasses[0].Name);
        Assert.True(closed);
    }
}
