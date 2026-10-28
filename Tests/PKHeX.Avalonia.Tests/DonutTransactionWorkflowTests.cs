using Moq;
using PKHeX.Core;
using PKHeX.Presentation.ViewModels;

namespace PKHeX.Avalonia.Tests;

public class DonutTransactionWorkflowTests
{
    private static Mock<IDialogService> Dialog(bool confirm = true)
    {
        var dialog = new Mock<IDialogService>();
        dialog.Setup(d => d.ShowConfirmationAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>())).ReturnsAsync(confirm);
        return dialog;
    }
    [Fact]
    public void DirectEditsHexUnknownValuesAndResetRemainStaged()
    {
        var save = new SAV9ZA(); var record = save.Donuts.GetDonut(0);
        record.MillisecondsSince1970 = 1; record.Flavor0 = ulong.MaxValue; record.Reserved = 12345;
        var before = save.Donuts.Data.ToArray(); save.State.Edited = false;
        using var vm = new DonutEditorViewModel(save);
        var row = vm.SelectedDonut!;
        Assert.True(row.IsOccupied); Assert.Contains("FFFFFFFFFFFFFFFF", row.Flavor0Name);
        row.Stars = 3; row.Berry1 = 65000; row.Flavor0Text = "wrong";
        Assert.True(row.HasError); Assert.False(vm.CanSave); Assert.Equal(ulong.MaxValue, row.Flavor0);
        row.Flavor0Text = "0xABCD"; Assert.False(row.HasError); Assert.Equal(0xABCDul, row.Flavor0);
        Assert.Equal(before, save.Donuts.Data.ToArray()); Assert.False(save.State.Edited);
        vm.ResetCurrentCommand.Execute(null); Assert.Equal(ulong.MaxValue, vm.SelectedDonut!.Flavor0);
        vm.SaveCommand.Execute(null); Assert.Equal(before, save.Donuts.Data.ToArray()); Assert.False(save.State.Edited);
    }
    [Theory] [InlineData("RandomizeAll")] [InlineData("CloneCurrent")] [InlineData("ShinyAssortment")] [InlineData("Compress")] [InlineData("Generate")]
    public async Task EveryPocketActionIsStagedUndoableAndRequiresConfirmation(string action)
    {
        var save = new SAV9ZA(); save.Donuts.GetDonut(3).MillisecondsSince1970 = 1;
        var before = save.Donuts.Data.ToArray(); save.State.Edited = false;
        using var vm = new DonutEditorViewModel(save, Dialog().Object);
        var command = action switch { "RandomizeAll" => vm.RandomizeAllCommand, "CloneCurrent" => vm.CloneCurrentCommand,
            "ShinyAssortment" => vm.ShinyAssortmentCommand, "Compress" => vm.CompressCommand, _ => vm.GenerateCommand };
        await command.ExecuteAsync(null);
        Assert.Equal(before, save.Donuts.Data.ToArray()); Assert.False(save.State.Edited); Assert.True(vm.CanUndo);
        vm.UndoCommand.Execute(null); vm.SaveCommand.Execute(null);
        Assert.Equal(before, save.Donuts.Data.ToArray()); Assert.False(save.State.Edited);
    }
    [Fact]
    public async Task DeclinedActionAndCancellationPreserveSource()
    {
        var save = new SAV9ZA(); var before = save.Donuts.Data.ToArray();
        using var vm = new DonutEditorViewModel(save, Dialog(false).Object);
        await vm.RandomizeAllCommand.ExecuteAsync(null); Assert.False(vm.CanUndo);
        vm.SelectedDonut!.Stars = 4; vm.CancelCommand.Execute(null); vm.SaveCommand.Execute(null);
        Assert.Equal(before, save.Donuts.Data.ToArray()); Assert.False(save.State.Edited);
    }
    [Fact]
    public async Task LocalFileRoundtripRejectsWrongSizeAndCommitsOnlyAfterSave()
    {
        var path = Path.GetTempFileName();
        try
        {
            var save = new SAV9ZA(); var before = save.Donuts.Data.ToArray();
            var dialog = Dialog(); dialog.Setup(d => d.SaveFileAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string[]>())).ReturnsAsync(path);
            using var vm = new DonutEditorViewModel(save, dialog.Object);
            await File.WriteAllBytesAsync(path, new byte[71], TestContext.Current.CancellationToken); await vm.ImportPathAsync(path); Assert.NotEmpty(vm.Error);
            var data = new byte[72]; data[0] = 1; data[8] = 5; data[^1] = 0xA5;
            await File.WriteAllBytesAsync(path, data, TestContext.Current.CancellationToken); await vm.ImportPathAsync(path); Assert.True(vm.SelectedDonut!.IsOccupied);
            Assert.Equal(before, save.Donuts.Data.ToArray());
            await vm.ExportCommand.ExecuteAsync(null); Assert.Equal(data, await File.ReadAllBytesAsync(path, TestContext.Current.CancellationToken));
            vm.SaveCommand.Execute(null); Assert.Equal(data, save.Donuts.GetDonut(0).Data.ToArray()); Assert.True(save.State.Edited);
        }
        finally { File.Delete(path); }
    }
    [Fact]
    public async Task GeneratorUsesConfirmedRangeAndCloseDuringConfirmationDiscards()
    {
        var save = new SAV9ZA();
        var confirmation = new TaskCompletionSource<bool>();
        var dialog = Dialog();
        dialog.Setup(d => d.ShowConfirmationAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>())).Returns(() => confirmation.Task);
        using var vm = new DonutEditorViewModel(save, dialog.Object);
        vm.GenerateStart = 1; vm.GenerateEnd = 2;
        var pending = vm.GenerateCommand.ExecuteAsync(null);
        vm.GenerateStart = 10; vm.GenerateEnd = 11;
        confirmation.SetResult(true); await pending;
        Assert.True(vm.Donuts[1].IsOccupied);
        Assert.False(vm.Donuts[10].IsOccupied);
        vm.CancelCommand.Execute(null);
        Assert.False(PKHeX.Application.Services.DonutDataSession.IsOccupied(save.Donuts.GetDonut(1)));

        confirmation = new TaskCompletionSource<bool>();
        using var closing = new DonutEditorViewModel(save, dialog.Object);
        pending = closing.RandomizeAllCommand.ExecuteAsync(null);
        closing.Dispose(); confirmation.SetResult(true); await pending;
        Assert.False(save.State.Edited); Assert.False(closing.CanUndo);
    }

    [Fact]
    public void EmptyAndReversedGeneratorRangesShowFeedback()
    {
        using var vm = new DonutEditorViewModel(new SAV9ZA()); vm.GenerateStart = 10; vm.GenerateEnd = 10;
        vm.GenerateCommand.Execute(null); Assert.NotEmpty(vm.Error);
        vm.GenerateStart = 11; vm.GenerateCommand.Execute(null); Assert.NotEmpty(vm.Error);
    }
}
