using PKHeX.Core;
using PKHeX.Presentation.ViewModels;
using Xunit;

namespace PKHeX.Avalonia.Tests;

public sealed class SealStickers8bEditorTests
{
    [Fact]
    public void LoadsBundledNamesAndRoundTripsCountTotalAndObtained()
    {
        var sav = new SAV8BS();
        var items = sav.SealList.ReadItems();
        var stored = items[2];
        stored.Count = 3;
        stored.TotalCount = 5;
        stored.IsGet = true;
        sav.SealList.WriteItems(items);

        var vm = new SealStickers8bEditorViewModel(sav);
        var item = Assert.Single(vm.Items, x => x.Index == 2);

        Assert.Equal("Heart Sticker B", item.Name);
        Assert.Equal(3, item.Count);
        Assert.Equal(5, item.TotalCount);
        Assert.True(item.IsGet);

        item.Count = 7;
        item.TotalCount = 9;
        item.IsGet = false;
        vm.SaveCommand.Execute(null);

        var saved = sav.SealList.ReadItems()[2];
        Assert.Equal(0, saved.Count);
        Assert.Equal(0, saved.TotalCount);
        Assert.False(saved.IsGet);
        Assert.True(sav.State.Edited);
    }

    [Fact]
    public void CounterEditsKeepCountWithinLifetimeTotal()
    {
        var vm = new SealStickers8bEditorViewModel(new SAV8BS());
        var item = vm.Items[2];

        item.IsGet = true;
        item.TotalCount = 3;
        item.Count = 5;

        Assert.Equal(5, item.Count);
        Assert.Equal(5, item.TotalCount);
        Assert.True(item.IsGet);

        item.TotalCount = 2;
        Assert.Equal(5, item.TotalCount);
    }

    [Fact]
    public void CancelDiscardsBulkAndIndividualEdits()
    {
        var sav = new SAV8BS();
        var items = sav.SealList.ReadItems();
        var original = items[2];
        original.Count = 3;
        original.TotalCount = 5;
        original.IsGet = true;
        sav.SealList.WriteItems(items);
        sav.State.Edited = false;

        var vm = new SealStickers8bEditorViewModel(sav);
        vm.SetAllMaxCommand.Execute(null);
        vm.Items[0].Count = 1;
        vm.CancelCommand.Execute(null);

        var after = sav.SealList.ReadItems()[2];
        Assert.Equal(3, after.Count);
        Assert.Equal(5, after.TotalCount);
        Assert.True(after.IsGet);
        Assert.False(sav.State.Edited);
    }
}
