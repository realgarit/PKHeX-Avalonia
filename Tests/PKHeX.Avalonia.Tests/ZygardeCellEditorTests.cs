using PKHeX.Core;
using PKHeX.Presentation.ViewModels;
using Xunit;

namespace PKHeX.Avalonia.Tests;

public sealed class ZygardeCellEditorTests
{
    [Fact]
    public void SunMoon_UsesZygardeCellSemantics()
    {
        var sav = new SAV7SM();
        var vm = new ZygardeCellEditorViewModel(sav);

        Assert.True(vm.IsSupported);
        Assert.True(vm.IsZygardeCell);
        Assert.False(vm.IsTotemSticker);
        Assert.Equal(95, vm.Cells.Count);

        vm.CollectAllCommand.Execute(null);

        Assert.Equal(95, vm.CellsCollected);
        Assert.Equal(95, vm.CellsTotal);
        Assert.All(vm.Cells, cell => Assert.Equal(2, cell.State));
        Assert.True(sav.State.Edited);
    }

    [Fact]
    public void SunMoon_RowEdits_RecalculateTotalsAndCollectedCount()
    {
        var sav = new SAV7SM();
        var vm = new ZygardeCellEditorViewModel(sav);

        vm.Cells[0].State = 1;
        Assert.Equal(1, vm.CellsTotal);
        Assert.Equal(0, vm.CellsCollected);

        vm.Cells[0].State = 2;
        Assert.Equal(1, vm.CellsTotal);
        Assert.Equal(1, vm.CellsCollected);

        vm.Cells[0].State = 0;
        Assert.Equal(0, vm.CellsTotal);
        Assert.Equal(0, vm.CellsCollected);
    }

    [Fact]
    public void UltraSunMoon_UsesTotemStickerSemantics()
    {
        var sav = new SAV7USUM();
        var vm = new ZygardeCellEditorViewModel(sav);

        Assert.True(vm.IsSupported);
        Assert.True(vm.IsTotemSticker);
        Assert.False(vm.IsZygardeCell);
        Assert.Equal(100, vm.Cells.Count);

        vm.CollectAllCommand.Execute(null);

        Assert.Equal(100, vm.CellsCollected);
        Assert.Equal(0, vm.CellsTotal);
        Assert.All(vm.Cells, cell => Assert.Equal(2, cell.State));
        Assert.Equal(100, sav.GetRecord(72));
        Assert.True(sav.State.Edited);
    }

    [Fact]
    public void UltraSunMoon_RowEdits_RecalculateCollectedCountWithoutZygardeTotal()
    {
        var sav = new SAV7USUM();
        var vm = new ZygardeCellEditorViewModel(sav);

        vm.Cells[0].State = 2;

        Assert.Equal(1, vm.CellsCollected);
        Assert.Equal(0, vm.CellsTotal);
        Assert.Equal(1, sav.GetRecord(72));
    }
}
