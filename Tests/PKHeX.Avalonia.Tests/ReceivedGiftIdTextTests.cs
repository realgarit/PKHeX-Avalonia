using Moq;
using PKHeX.Application.UseCases;
using PKHeX.Core;
using PKHeX.Presentation.ViewModels;

namespace PKHeX.Avalonia.Tests;

public class ReceivedGiftIdTextTests
{
    [Theory]
    [InlineData(GameVersion.B)]
    [InlineData(GameVersion.W2)]
    public void EncryptedGen5AlbumOpenResetAndNoOpApplyPreserveSourceBytes(GameVersion version)
    {
        var sav = Assert.IsAssignableFrom<SAV5>(BlankSaveFile.Get(version));
        MysteryGiftPreviewLayoutTests.InitializeGen5Album(sav);
        sav.Mystery.SetMysteryGiftReceivedFlag(42, true);
        sav.Mystery.EndAccess();
        sav.State.Edited = false;
        var before = sav.Data.ToArray();
        var vm = new MysteryGiftEditorViewModel(sav, new Mock<IDialogService>().Object);
        Assert.Equal(before, sav.Data.ToArray());
        Assert.Contains("0042", vm.ReceivedFlags);
        vm.ReceivedFlags.Clear();
        vm.ResetCommand.Execute(null);
        Assert.Equal(before, sav.Data.ToArray());
        vm.SaveCommand.Execute(null);
        Assert.Equal(before, sav.Data.ToArray());
        Assert.False(sav.State.Edited);
        vm.ReceivedFlags.Add("0007");
        vm.SaveCommand.Execute(null);
        Assert.True(sav.State.Edited);
        Assert.True(sav.Mystery.GetMysteryGiftReceivedFlag(7));
        sav.Mystery.EndAccess();
    }

    [Fact]
    public async Task ImportExportRoundtripRemainsStagedAndInvalidImportIsAtomic()
    {
        var path = Path.GetTempFileName();
        try
        {
            var sav = new SAV6XY();
            var flags = (IMysteryGiftFlags)((IMysteryGiftStorageProvider)sav).MysteryGiftStorage;
            var dialog = new Mock<IDialogService>();
            dialog.Setup(d => d.OpenFileAsync(It.IsAny<string>(), It.IsAny<string[]>())).ReturnsAsync(path);
            dialog.Setup(d => d.SaveFileAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string[]>())).ReturnsAsync(path);
            var vm = new MysteryGiftEditorViewModel(sav, dialog.Object);
            await File.WriteAllTextAsync(path, "0042\n0007\n0042", TestContext.Current.CancellationToken);
            await vm.ImportReceivedIdsCommand.ExecuteAsync(null);
            Assert.Equal(["0007", "0042"], vm.ReceivedFlags);
            Assert.False(flags.GetMysteryGiftReceivedFlag(42));
            await vm.ExportReceivedIdsCommand.ExecuteAsync(null);
            Assert.Equal(ReceivedGiftIdText.Export([7, 42]), await File.ReadAllTextAsync(path, TestContext.Current.CancellationToken));
            await File.WriteAllTextAsync(path, "0001\n2048", TestContext.Current.CancellationToken);
            await vm.ImportReceivedIdsCommand.ExecuteAsync(null);
            Assert.Equal(["0007", "0042"], vm.ReceivedFlags);
            dialog.Verify(d => d.ShowErrorAsync(It.IsAny<string>(), It.IsAny<string>()), Times.Once);
            vm.SaveCommand.Execute(null);
            Assert.True(flags.GetMysteryGiftReceivedFlag(42));
            Assert.True(flags.GetMysteryGiftReceivedFlag(7));
        }
        finally { File.Delete(path); }
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task BulkActionsRequireConfirmationAndResetDiscardsThem(bool confirmed)
    {
        var sav = new SAV6XY();
        var flags = (IMysteryGiftFlags)((IMysteryGiftStorageProvider)sav).MysteryGiftStorage;
        var dialog = new Mock<IDialogService>();
        dialog.Setup(d => d.ShowConfirmationAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>())).ReturnsAsync(confirmed);
        var vm = new MysteryGiftEditorViewModel(sav, dialog.Object);
        await vm.AllUsedCommand.ExecuteAsync(null);
        Assert.Equal(confirmed ? flags.MysteryGiftReceivedFlagMax : 0, vm.ReceivedFlags.Count);
        Assert.False(flags.GetMysteryGiftReceivedFlag(7));
        vm.ResetCommand.Execute(null);
        Assert.Empty(vm.ReceivedFlags);
    }

    [Fact]
    public void ParseDeduplicatesSortsAndIncludesBoundaryIds()
    {
        Assert.True(ReceivedGiftIdText.TryParse("2047; 0000\n42,42\t7", 2048, out var ids));
        Assert.Equal([0, 7, 42, 2047], ids);
        Assert.True(ReceivedGiftIdText.TryParse(ReceivedGiftIdText.Export(ids), 2048, out var roundtrip));
        Assert.Equal(ids, roundtrip);
    }

    [Theory]
    [InlineData("42,-1")]
    [InlineData("42,2048")]
    [InlineData("42,no")]
    [InlineData("42,999999999999999999")]
    public void InvalidInputHasNoPartialResult(string text)
    {
        Assert.False(ReceivedGiftIdText.TryParse(text, 2048, out var ids));
        Assert.Empty(ids);
    }

    [Fact]
    public void NoOpApplyPreservesFlagZeroAndEditedState()
    {
        var sav = new SAV6XY();
        var flags = (IMysteryGiftFlags)((IMysteryGiftStorageProvider)sav).MysteryGiftStorage;
        flags.SetMysteryGiftReceivedFlag(0, true);
        sav.State.Edited = false;
        var before = sav.Data.ToArray();
        var vm = new MysteryGiftEditorViewModel(sav, new Mock<IDialogService>().Object);
        vm.SaveCommand.Execute(null);
        Assert.True(flags.GetMysteryGiftReceivedFlag(0));
        Assert.Equal(before, sav.Data);
        Assert.False(sav.State.Edited);
    }

    [Fact]
    public void ApplyChangesStagedFlagsAndPreservesIndependentChanges()
    {
        var sav = new SAV6XY();
        var flags = (IMysteryGiftFlags)((IMysteryGiftStorageProvider)sav).MysteryGiftStorage;
        flags.SetMysteryGiftReceivedFlag(42, true);
        var vm = new MysteryGiftEditorViewModel(sav, new Mock<IDialogService>().Object);
        vm.ReceivedFlags.Remove("0042");
        vm.ReceivedFlags.Add("0007");
        flags.SetMysteryGiftReceivedFlag(99, true);
        vm.SaveCommand.Execute(null);
        Assert.False(flags.GetMysteryGiftReceivedFlag(42));
        Assert.True(flags.GetMysteryGiftReceivedFlag(7));
        Assert.True(flags.GetMysteryGiftReceivedFlag(99));
    }

    [Fact]
    public void ResetRestoresReceivedIds()
    {
        var sav = new SAV6XY();
        var flags = (IMysteryGiftFlags)((IMysteryGiftStorageProvider)sav).MysteryGiftStorage;
        flags.SetMysteryGiftReceivedFlag(42, true);
        var vm = new MysteryGiftEditorViewModel(sav, new Mock<IDialogService>().Object);
        vm.ReceivedFlags.Clear();
        vm.ResetCommand.Execute(null);
        Assert.Contains("0042", vm.ReceivedFlags);
    }
}
