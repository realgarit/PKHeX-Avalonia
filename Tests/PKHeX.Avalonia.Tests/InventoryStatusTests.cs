using Avalonia.Controls;
using Avalonia.Automation;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Headless.XUnit;
using Avalonia.VisualTree;
using Moq;
using PKHeX.Avalonia.Tests.Fixtures;
using PKHeX.Avalonia.Tests.Harness;
using PKHeX.Avalonia.Views;
using PKHeX.Core;
using PKHeX.Presentation.ViewModels;

namespace PKHeX.Avalonia.Tests;

public class InventoryStatusTests
{
    private static SAV9ZA LoadZa() => Assert.IsType<SAV9ZA>(SaveFileFixture.LoadSave(
        Path.Combine(SaveFileFixture.FindSaveFilesPath()!, "gen9a_legendsza.main")));
    private static InventoryEditorViewModel Editor(SaveFile save) => new(save, new Mock<ISpriteRenderer>().Object);

    [Theory]
    [InlineData(0u)]
    [InlineData(0x1Bu)]
    [InlineData(0xA000001Fu)]
    public void Za_AllFlagsRoundTripAndUnknownBitsSurvive(uint flags)
    {
        var save = LoadZa();
        var source = save.Items.GetItem(17);
        source.Flags = flags;
        source.Padding = 0x12345678;
        source.Count = 2;
        source.Write(InventoryPouch9a.GetItemSpan(save.Items.Data, 17));
        var before = save.Items.Data.ToArray();
        var vm = Editor(save);
        var row = vm.Pouches.SelectMany(p => p.Items).Single(i => i.ItemId == 17);
        Assert.Equal((flags & 2) != 0, row.IsFavorite);
        Assert.Equal((flags & 1) != 0, row.IsNew);
        Assert.Equal((flags & 8) != 0, row.IsNewShop);
        Assert.Equal((flags & 16) != 0, row.IsHeld);
        row.IsFavorite = !row.IsFavorite;
        row.IsNew = !row.IsNew;
        row.IsNewShop = !row.IsNewShop;
        row.IsHeld = !row.IsHeld;
        Assert.Equal(before, save.Items.Data.ToArray());
        vm.SaveCommand.Execute(null);
        var actual = save.Items.GetItem(17);
        Assert.Equal(flags ^ 0x1Bu, actual.Flags);
        Assert.Equal(0x12345678u, actual.Padding);
        Assert.Equal(2, actual.Count);
        var reloaded = Editor(save).Pouches.SelectMany(p => p.Items).Single(i => i.ItemId == 17);
        Assert.Equal(row.IsFavorite, reloaded.IsFavorite);
        Assert.Equal(row.IsNew, reloaded.IsNew);
        Assert.Equal(row.IsNewShop, reloaded.IsNewShop);
        Assert.Equal(row.IsHeld, reloaded.IsHeld);
        actual.Write(InventoryPouch9a.GetItemSpan(before, 17));
        Assert.Equal(before, save.Items.Data.ToArray());
    }

    [Fact]
    public void Za_EmptyItemFlagsAndResetAreStaged()
    {
        var save = LoadZa();
        var source = save.Items.GetItem(17);
        source.Count = 0;
        source.Flags = 0xA0000004;
        source.Write(InventoryPouch9a.GetItemSpan(save.Items.Data, 17));
        var before = save.Items.Data.ToArray();
        var vm = Editor(save);
        var pouch = vm.Pouches.Single(p => p.Items.Any(i => i.ItemId == 17));
        pouch.Items.Single(i => i.ItemId == 17).IsFavorite = true;
        vm.ResetCommand.Execute(null);
        Assert.False(pouch.Items.Single(i => i.ItemId == 17).IsFavorite);
        Assert.Equal(before, save.Items.Data.ToArray());
        pouch.Items.Single(i => i.ItemId == 17).IsFavorite = true;
        vm.SaveCommand.Execute(null);
        Assert.Equal(0xA0000006u, save.Items.GetItem(17).Flags);
    }

    [Fact]
    public void Za_NoOpApplyPreservesEntireItemTable()
    {
        var save = LoadZa();
        var before = save.Items.Data.ToArray();
        Editor(save).SaveCommand.Execute(null);
        Assert.Equal(before, save.Items.Data.ToArray());
    }

    [Fact]
    public void Za_SortAndGiveAllKeepFlagsWithUnchangedIdentities_ClearRemovesMarks()
    {
        var save = LoadZa();
        var source = save.Items.GetItem(17);
        source.Flags = 0x1B;
        source.Count = 7;
        source.Write(InventoryPouch9a.GetItemSpan(save.Items.Data, 17));
        var vm = Editor(save);
        var pouch = vm.Pouches.Single(p => p.Items.Any(i => i.ItemId == 17));
        pouch.SortByCount();
        vm.SaveCommand.Execute(null);
        Assert.Equal(0x1Bu, save.Items.GetItem(17).Flags);
        // Give All must not depend on presentation sort order.
        pouch.SortByCount();
        pouch.GiveAllItems();
        vm.SaveCommand.Execute(null);
        Assert.Equal(0x1Bu, save.Items.GetItem(17).Flags);
        pouch.ClearAllItems();
        vm.SaveCommand.Execute(null);
        Assert.Equal(0, save.Items.GetItem(17).Count);
        Assert.Equal(0u, save.Items.GetItem(17).Flags & 0x1B);
    }

    [Fact]
    public void ReplacementClearsKnownMarksAndSortingMovesCompleteGenericRecords()
    {
        var item = new InventoryItem7 { Index = 17, Count = 3, IsNew = true };
        var row = new InventoryItemViewModel(item, "Potion", [], 99, new Mock<ISpriteRenderer>().Object);
        row.ItemId = 18;
        Assert.False(row.IsNew);
        Assert.True(item.IsNew);
        Assert.False(Assert.IsType<InventoryItem7>(row.CreateRecord()).IsNew);
        var save = new SAV7SM();
        var vm = Editor(save);
        var pouch = vm.Pouches.First(p => p.Items.Count > 2 && p.ItemList.Count > 2);
        var first = pouch.Items[0];
        first.ItemId = pouch.ItemList[1].Value;
        first.Count = 2;
        first.IsNew = true;
        var second = pouch.Items[1];
        second.ItemId = pouch.ItemList[2].Value;
        second.Count = 5;
        second.IsNew = false;
        pouch.SortByCount();
        vm.SaveCommand.Execute(null);
        var actual = save.Inventory.Pouches.Single(p => p.Type == pouch.PouchType);
        Assert.True(Assert.IsAssignableFrom<IItemNewFlag>(actual.Items.Single(i => i.Index == first.ItemId)).IsNew);
        Assert.False(Assert.IsAssignableFrom<IItemNewFlag>(actual.Items.Single(i => i.Index == second.ItemId)).IsNew);
    }

    [AvaloniaTheory]
    [InlineData(true)]
    [InlineData(false)]
    public void StatusColumnsFollowSaveCapabilities(bool za)
    {
        using var app = new HeadlessAppFixture();
        app.Window.Content = new InventoryEditor { DataContext = Editor(za ? LoadZa() : new SAV3E()) };
        app.Window.Width = 900;
        app.Window.Height = 600;
        app.Pump();
        var grid = app.Window.GetVisualDescendants().OfType<DataGrid>().Single();
        Assert.Equal(za ? 7 : 3, grid.Columns.Count(c => c.IsVisible));
        if (za)
        {
            var check = grid.GetVisualDescendants().OfType<CheckBox>().First();
            Assert.False(string.IsNullOrWhiteSpace(AutomationProperties.GetName(check)));
            Assert.False(string.IsNullOrWhiteSpace(AutomationProperties.GetHelpText(check)));
            var before = check.IsChecked;
            Assert.True(check.Focus());
            app.PressKey(PhysicalKey.Space);
            app.Pump();
            Assert.Equal(!before, check.IsChecked);
        }
        if (za && Environment.GetEnvironmentVariable("PKHEX_HEADLESS_CAPTURE") == "1")
        {
            var directory = Environment.GetEnvironmentVariable("PKHEX_HEADLESS_CAPTURE_DIR")!;
            Directory.CreateDirectory(directory);
            Assert.NotNull(app.CaptureFrame(Path.Combine(directory, "inventory-za-status.png")));
        }
    }
}
