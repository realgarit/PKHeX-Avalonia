using System.Buffers.Binary;
using Moq;
using PKHeX.Avalonia.Tests.Fixtures;
using PKHeX.Core;
using PKHeX.Presentation.ViewModels;

namespace PKHeX.Avalonia.Tests;

public class LgpeTrainerWorkflowTests
{
    private static Mock<IDialogService> Dialog(bool answer = true)
    {
        var dialogs = new Mock<IDialogService>();
        dialogs.Setup(dialog => dialog.ShowConfirmationAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>())).ReturnsAsync(answer);
        return dialogs;
    }
    internal static SAV7b CreateSave() => new(File.ReadAllBytes(Path.Combine(SaveFileFixture.FindSaveFilesPath()!, "gen7b_letsgopikachu.bin")));
    internal static byte[] Record(ushort species = 25)
    {
        var data = new GP1().Data.ToArray();
        BinaryPrimitives.WriteUInt16LittleEndian(data.AsSpan(0x28), species);
        BinaryPrimitives.WriteSingleLittleEndian(data.AsSpan(0x30), 10);
        BinaryPrimitives.WriteInt32LittleEndian(data.AsSpan(0x2C), 123);
        data[0x70] = 1;
        return data;
    }
    [Fact]
    public void GenericTrainerApiAppliesChangesAndResetDoesNotReapplyDiscardedDrafts()
    {
        var save = CreateSave(); var parent = new TrainerEditorViewModel(save);
        parent.TrainerName = "Ash"; parent.Money = 12345; parent.SaveCommand.Execute(null);
        Assert.Equal("Ash", save.OT); Assert.Equal(12345u, save.Money);
        parent.TrainerName = "Discard"; parent.ResetCommand.Execute(null); parent.SaveCommand.Execute(null);
        Assert.Equal("Ash", save.OT);
        parent.LgpeEditor!.RivalName = "Child"; parent.SaveCommand.Execute(null);
        Assert.Equal("Child", save.Misc.RivalName);
        parent.Dispose();
    }

    [Fact]
    public void TrainerMapScaleRotationAndDatesStageAndRoundtrip()
    {
        var save = CreateSave(); var before = save.Data.ToArray();
        using var vm = new Misc7bEditorViewModel(save, Dialog().Object);
        vm.TrainerName = "Test"; vm.RivalName = "Rival"; vm.Money = 12345;
        vm.MapId = ulong.MaxValue; vm.X = 12.5; vm.Y = -4; vm.Z = 56;
        vm.ScaleX = 1.25; vm.ScaleY = 1.5; vm.ScaleZ = 2; vm.Rotation = 135;
        vm.AdventureDate = new DateTimeOffset(2026, 10, 2, 0, 0, 0, TimeSpan.Zero);
        vm.AdventureTime = new TimeSpan(12, 25, 0); vm.AdventureSecond = 15;
        vm.LastSavedDate = new DateTimeOffset(2026, 10, 2, 0, 0, 0, TimeSpan.Zero); vm.LastSavedTime = new TimeSpan(13, 30, 0);
        Assert.True(vm.CanSave); Assert.Equal(before, save.Data.ToArray());
        vm.SaveCommand.Execute(null);
        var reopened = new SAV7b(save.Write());
        Assert.Equal("Test", reopened.OT); Assert.Equal("Rival", reopened.Misc.RivalName); Assert.Equal(12345u, reopened.Money);
        Assert.Equal(ulong.MaxValue, reopened.Coordinates.M); Assert.Equal(12.5f, reopened.Coordinates.X);
        Assert.Equal(1.25f, reopened.Coordinates.SX); Assert.Equal(1.5f, reopened.Coordinates.SY); Assert.Equal(2f, reopened.Coordinates.SZ);
        Assert.Equal(135, Math.Atan2(reopened.Coordinates.RZ, reopened.Coordinates.RW) * 360 / Math.PI, 4);
        Assert.Equal(new DateTime(2026, 10, 2, 12, 25, 15), reopened.PlayerGeoLocation.AdventureBegin.Timestamp);
        Assert.Equal(new DateTime(2026, 10, 2, 13, 30, 0), reopened.Played.LastSavedDate);
    }
    [Theory] [InlineData("Titles")] [InlineData("Fashion")] [InlineData("DeleteAll")]
    public async Task BulkActionsRemainStagedAndCancelOrCloseDiscardsThem(string action)
    {
        var save = CreateSave(); save.Park[0] = GP1.FromData(Record());
        var before = save.Data.ToArray(); save.State.Edited = false;
        using var vm = new Misc7bEditorViewModel(save, Dialog().Object);
        if (action == "Titles") await vm.UnlockAllTrainerTitlesCommand.ExecuteAsync(null);
        else if (action == "Fashion") await vm.UnlockAllFashionCommand.ExecuteAsync(null);
        else await vm.DeleteAllGoParkCommand.ExecuteAsync(null);
        Assert.Equal(before, save.Data.ToArray());
        vm.Dispose(); vm.SaveCommand.Execute(null);
        Assert.Equal(before, save.Data.ToArray()); Assert.False(save.State.Edited);
    }
    [Fact]
    public void NoOpApplyAndResetKeepEverySourceByte()
    {
        var save = CreateSave(); var before = save.Data.ToArray(); save.State.Edited = false;
        using var vm = new Misc7bEditorViewModel(save, Dialog().Object);
        vm.SaveCommand.Execute(null); Assert.Equal(before, save.Data.ToArray()); Assert.False(save.State.Edited);
        vm.RivalName = "Discard"; vm.X = 5; vm.ResetCommand.Execute(null); vm.SaveCommand.Execute(null);
        Assert.Equal(before, save.Data.ToArray()); Assert.False(save.State.Edited);
    }
    [Fact]
    public async Task SlotImportKeepsItsDestinationWhenSelectionChangesDuringThePicker()
    {
        var save = CreateSave(); save.Park.DeleteAll(); var before = save.Data.ToArray();
        var path = Path.Combine(Path.GetTempPath(), $"pkhex-go-picker-{Guid.NewGuid():N}.gp1");
        var picked = new TaskCompletionSource<string?>(TaskCreationOptions.RunContinuationsAsynchronously);
        var dialogs = Dialog(); dialogs.Setup(dialog => dialog.OpenFileAsync(It.IsAny<string>(), It.IsAny<string[]>())).Returns(picked.Task);
        using var vm = new Misc7bEditorViewModel(save, dialogs.Object);
        try
        {
            await File.WriteAllBytesAsync(path, Record(), TestContext.Current.CancellationToken); vm.SelectedSlot = vm.ParkSlots.Single(row => row.Index == 1);
            var pending = vm.ImportSlotCommand.ExecuteAsync(null);
            vm.SelectedSlot = vm.ParkSlots.Single(row => row.Index == 2);
            picked.SetResult(path); await pending;
            Assert.True(vm.ParkSlots.Single(row => row.Index == 1).Occupied);
            Assert.False(vm.ParkSlots.Single(row => row.Index == 2).Occupied);
            Assert.Equal(2, vm.SelectedSlot!.Index); Assert.Equal(before, save.Data.ToArray());
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public async Task ResetDiscardsALateImportPickerResult()
    {
        var save = CreateSave(); save.Park.DeleteAll(); var before = save.Data.ToArray();
        var path = Path.Combine(Path.GetTempPath(), $"pkhex-go-reset-{Guid.NewGuid():N}.gp1");
        var picked = new TaskCompletionSource<string?>(TaskCreationOptions.RunContinuationsAsynchronously);
        var dialogs = Dialog(); dialogs.Setup(dialog => dialog.OpenFileAsync(It.IsAny<string>(), It.IsAny<string[]>())).Returns(picked.Task);
        using var vm = new Misc7bEditorViewModel(save, dialogs.Object);
        try
        {
            await File.WriteAllBytesAsync(path, Record(), TestContext.Current.CancellationToken); var pending = vm.ImportSlotCommand.ExecuteAsync(null);
            vm.ResetCommand.Execute(null); picked.SetResult(path); await pending;
            Assert.False(vm.CanUndo); Assert.False(vm.ParkSlots[0].Occupied); Assert.Equal(before, save.Data.ToArray());
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public async Task FolderCapacityIsCheckedBeforeAnyRecordIsImported()
    {
        var save = CreateSave(); save.Park.DeleteAll();
        for (int index = 0; index < 49; index++) save.Park[index] = GP1.FromData(Record());
        var before = save.Data.ToArray();
        var folder = Path.Combine(Path.GetTempPath(), $"pkhex-go-capacity-{Guid.NewGuid():N}"); Directory.CreateDirectory(folder);
        var first = Path.Combine(folder, "a.gp1"); var second = Path.Combine(folder, "b.gp1");
        var dialogs = Dialog(); dialogs.Setup(dialog => dialog.OpenFolderAsync(It.IsAny<string>())).ReturnsAsync(folder);
        using var vm = new Misc7bEditorViewModel(save, dialogs.Object);
        try
        {
            await File.WriteAllBytesAsync(first, Record(), TestContext.Current.CancellationToken); await File.WriteAllBytesAsync(second, Record(), TestContext.Current.CancellationToken);
            await vm.ImportFolderCommand.ExecuteAsync(null);
            Assert.True(vm.HasError); Assert.False(vm.CanUndo); Assert.False(vm.ParkSlots[49].Occupied);
            Assert.Equal(before, save.Data.ToArray());
        }
        finally { File.Delete(first); File.Delete(second); Directory.Delete(folder); }
    }

    [Fact]
    public async Task FolderImportPreflightsEveryRecordAndPreservesOpaqueBytes()
    {
        var save = CreateSave(); save.Park.DeleteAll(); var before = save.Data.ToArray();
        var folder = Path.Combine(Path.GetTempPath(), $"pkhex-go-import-{Guid.NewGuid():N}"); Directory.CreateDirectory(folder);
        var first = Path.Combine(folder, "a.gp1"); var second = Path.Combine(folder, "B.GP1");
        var record = Record(); record[^1] = 0xA5;
        var dialogs = Dialog(); dialogs.Setup(dialog => dialog.OpenFolderAsync(It.IsAny<string>())).ReturnsAsync(folder);
        using var vm = new Misc7bEditorViewModel(save, dialogs.Object);
        try
        {
            await File.WriteAllBytesAsync(first, record, TestContext.Current.CancellationToken); await File.WriteAllBytesAsync(second, [1, 2, 3], TestContext.Current.CancellationToken);
            await vm.ImportFolderCommand.ExecuteAsync(null);
            Assert.True(vm.HasError); Assert.False(vm.ParkSlots[0].Occupied); Assert.False(vm.CanUndo);
            Assert.Equal(before, save.Data.ToArray());
            await File.WriteAllBytesAsync(second, Record(1), TestContext.Current.CancellationToken);
            await vm.ImportFolderCommand.ExecuteAsync(null);
            Assert.True(vm.CanUndo); Assert.True(vm.ParkSlots[0].Occupied); Assert.True(vm.ParkSlots[1].Occupied);
            Assert.Equal(before, save.Data.ToArray());
            vm.SaveCommand.Execute(null);
            var reopened = new SAV7b(save.Write());
            Assert.Equal(record, reopened.Park[0].Data.ToArray()); Assert.Equal((ushort)1, reopened.Park[1].Species);
        }
        finally { File.Delete(first); File.Delete(second); Directory.Delete(folder); }
    }

    [Fact]
    public async Task FolderExportAndSummaryHonorCurrentParkScope()
    {
        var save = CreateSave(); save.Park.DeleteAll(); var first = Record(); var other = Record(1);
        save.Park[51] = GP1.FromData(first); save.Park[101] = GP1.FromData(other);
        var before = save.Data.ToArray();
        var folder = Path.Combine(Path.GetTempPath(), $"pkhex-go-export-{Guid.NewGuid():N}"); Directory.CreateDirectory(folder);
        string? summary = null;
        var dialogs = Dialog(); dialogs.Setup(dialog => dialog.OpenFolderAsync(It.IsAny<string>())).ReturnsAsync(folder);
        dialogs.Setup(dialog => dialog.SetClipboardTextAsync(It.IsAny<string>())).Callback<string>(text => summary = text).Returns(Task.CompletedTask);
        using var vm = new Misc7bEditorViewModel(save, dialogs.Object) { SelectedArea = 1 };
        var path = Path.Combine(folder, "park02-slot02.gp1");
        try
        {
            await vm.ExportFolderCommand.ExecuteAsync(null);
            Assert.Equal(path, Assert.Single(Directory.GetFiles(folder))); Assert.Equal(first, await File.ReadAllBytesAsync(path, TestContext.Current.CancellationToken));
            await vm.CopySummaryCommand.ExecuteAsync(null);
            Assert.NotNull(summary); Assert.Contains(GameInfo.Strings.Species[25], summary); Assert.DoesNotContain(GameInfo.Strings.Species[1], summary);
            Assert.Equal(before, save.Data.ToArray());
        }
        finally { File.Delete(path); Directory.Delete(folder); }
    }

    [Fact]
    public async Task ExportAndDeleteOperateOnTheCapturedSelectedSlot()
    {
        var save = CreateSave(); var record = Record(); save.Park[51] = GP1.FromData(record);
        var before = save.Data.ToArray(); var path = Path.Combine(Path.GetTempPath(), $"pkhex-go-{Guid.NewGuid():N}.gp1");
        var dialogs = Dialog(); dialogs.Setup(dialog => dialog.SaveFileAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string[]>())).ReturnsAsync(path);
        using var vm = new Misc7bEditorViewModel(save, dialogs.Object) { SelectedArea = 1 };
        vm.SelectedSlot = vm.ParkSlots.Single(row => row.Index == 51);
        try
        {
            await vm.ExportSlotCommand.ExecuteAsync(null); Assert.Equal(record, await File.ReadAllBytesAsync(path, TestContext.Current.CancellationToken));
            await vm.DeleteSlotCommand.ExecuteAsync(null); Assert.Equal(before, save.Data.ToArray());
            Assert.False(vm.SelectedSlot!.Occupied); vm.UndoCommand.Execute(null); Assert.True(vm.SelectedSlot!.Occupied);
            await vm.DeleteSlotCommand.ExecuteAsync(null); vm.SaveCommand.Execute(null);
            Assert.Equal((ushort)0, new SAV7b(save.Write()).Park[51].Species);
        }
        finally { File.Delete(path); }
    }
}
