using System.Reflection;
using Moq;
using PKHeX.Application.Abstractions;
using PKHeX.Core;
using PKHeX.Presentation.ViewModels;
using Xunit;

namespace PKHeX.Avalonia.Tests;

/// <summary>
/// Regression coverage for GitHub issue #163: setting a box/party slot with a PKM prepared from a
/// cross-format editor (e.g. a PK3 loaded via the Encounter Database into a Gen-4 save) must not
/// throw. <see cref="SaveFile.SetBoxSlot"/> throws <see cref="ArgumentException"/> when the PKM's
/// concrete type doesn't match <see cref="SaveFile.PKMType"/>, so the VM must convert or reject
/// (with a dialog) before ever calling into the save file.
/// </summary>
public class SlotOperationsTests
{
    private static MainWindowViewModel CreateViewModel(
        Mock<IDialogService> dialogServiceMock,
        ISlotService? slotService = null,
        UndoRedoService? undoRedo = null)
    {
        return new MainWindowViewModel(
            new Mock<ISaveFileGateway>().Object,
            dialogServiceMock.Object,
            new Mock<IWindowService>().Object,
            new Mock<ISpriteRenderer>().Object,
            slotService ?? new Mock<ISlotService>().Object,
            new Mock<IClipboardService>().Object,
            new Mock<IQrCodeService>().Object,
            UpdateTestDoubles.Coordinator(),
            new Mock<ISaveBackupService>().Object,
            new AppSettings(),
            new FakeSettingsStore(),
            new Mock<IThemeService>().Object,
            undoRedo ?? new UndoRedoService(),
            new LanguageService(),
            new Mock<IAutoLegalityService>().Object,
            new Mock<PKHeX.Application.Abstractions.LiveHex.ILiveHexService>().Object,
            new Mock<ILivingDexService>().Object,
            new Mock<PKHeX.Application.Abstractions.GiftRecords.IGiftRecordProvider>().Object);
    }

    private static void InvokeOnBoxSetSlot(MainWindowViewModel vm, int box, int slot)
    {
        var method = typeof(MainWindowViewModel).GetMethod("OnBoxSetSlot", BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.NotNull(method);
        method!.Invoke(vm, [box, slot]);
    }

    private static Task InvokeMoveSlotAsync(
        MainWindowViewModel vm,
        SlotLocation source,
        SlotLocation destination,
        bool clone)
    {
        var method = typeof(MainWindowViewModel).GetMethod("MoveSlotAsync", BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.NotNull(method);
        return Assert.IsAssignableFrom<Task>(method!.Invoke(vm, [source, destination, clone]));
    }

    [Fact]
    public void OnBoxSetSlot_with_foreign_format_pkm_does_not_throw()
    {
        // Currently open save is Gen-4, but the editor holds a PK3 (e.g. loaded from the Encounter
        // Database), simulating the crash scenario from issue #163.
        var sav4 = SaveFileFactory.CreateBlankSave(GameVersion.Pt);
        var sav3 = SaveFileFactory.CreateBlankSave(GameVersion.E);
        var pk3 = SaveFileFactory.CreateTestPKM(sav3);

        var (editorVm, _, _) = TestHelpers.CreateTestViewModel(pk3, sav3);

        var dialogServiceMock = new Mock<IDialogService>();
        dialogServiceMock
            .Setup(d => d.ShowErrorAsync(It.IsAny<string>(), It.IsAny<string>()))
            .Returns(Task.CompletedTask);

        var vm = CreateViewModel(dialogServiceMock);
        vm.CurrentSave = sav4;
        vm.CurrentPokemonEditor = editorVm;

        var exception = Record.Exception(() => InvokeOnBoxSetSlot(vm, 0, 0));

        Assert.Null(exception);

        var slotPk = sav4.GetBoxSlotAtIndex(0, 0);
        var converted = slotPk.Species != 0 && slotPk.GetType() == sav4.PKMType;
        var rejected = slotPk.Species == 0;

        // Either the PK3 was converted into a valid PK4 written to the slot, or the conversion
        // failed and the VM surfaced an error dialog instead of writing/crashing.
        Assert.True(converted || rejected);
        if (rejected)
            dialogServiceMock.Verify(d => d.ShowErrorAsync(It.IsAny<string>(), It.IsAny<string>()), Times.Once);
    }

    [Fact]
    public void MovingPartyPokemonToAnEmptyBoxCompactsTheRemainingParty()
    {
        var sav = new SAV6XY();
        sav.SetPartySlotAtIndex(new PK6 { Species = 1 }, 0);
        sav.SetPartySlotAtIndex(new PK6 { Species = 2 }, 1);
        sav.SetPartySlotAtIndex(new PK6 { Species = 3 }, 2);

        var slotService = new SlotService();
        var undoRedo = new UndoRedoService();
        var dialogService = new Mock<IDialogService>();
        var vm = CreateViewModel(dialogService, slotService, undoRedo);
        vm.CurrentSave = sav;
        undoRedo.Initialize(sav);

        slotService.RequestMove(SlotLocation.FromParty(0), SlotLocation.FromBox(0, 0), clone: false);

        Assert.Equal(2, sav.PartyCount);
        Assert.Equal(2, sav.GetPartySlotAtIndex(0).Species);
        Assert.Equal(3, sav.GetPartySlotAtIndex(1).Species);
        Assert.Equal(0, sav.GetPartySlotAtIndex(2).Species);
        Assert.Equal(1, sav.GetBoxSlotAtIndex(0, 0).Species);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void MovingOrCopyingBoxPokemonToPartyIsOneUndoableAtomicChange(bool clone)
    {
        var sav = new SAV6XY();
        sav.SetBoxSlotAtIndex(new PK6 { Species = 25 }, 0, 0);

        var slotService = new SlotService();
        var undoRedo = new UndoRedoService();
        var vm = CreateViewModel(new Mock<IDialogService>(), slotService, undoRedo);
        vm.CurrentSave = sav;
        undoRedo.Initialize(sav);

        slotService.RequestMove(SlotLocation.FromBox(0, 0), SlotLocation.FromParty(0), clone);

        Assert.Equal(1, undoRedo.ChangeCount);
        Assert.True(undoRedo.CanUndo);
        Assert.Equal(25, sav.GetPartySlotAtIndex(0).Species);
        Assert.Equal(clone ? 25 : 0, sav.GetBoxSlotAtIndex(0, 0).Species);

        undoRedo.Undo();

        Assert.True(undoRedo.CanRedo);
        Assert.Equal(0, sav.GetPartySlotAtIndex(0).Species);
        Assert.Equal(25, sav.GetBoxSlotAtIndex(0, 0).Species);

        undoRedo.Redo();

        Assert.True(undoRedo.CanUndo);
        Assert.Equal(25, sav.GetPartySlotAtIndex(0).Species);
        Assert.Equal(clone ? 25 : 0, sav.GetBoxSlotAtIndex(0, 0).Species);
    }

    [Fact]
    public void MovingPartyPokemonToEmptyBoxIsAtomicAndUndoRestoresCompactedParty()
    {
        var sav = new SAV6XY();
        sav.SetPartySlotAtIndex(new PK6 { Species = 1 }, 0);
        sav.SetPartySlotAtIndex(new PK6 { Species = 2 }, 1);
        sav.SetPartySlotAtIndex(new PK6 { Species = 3 }, 2);

        var slotService = new SlotService();
        var undoRedo = new UndoRedoService();
        var vm = CreateViewModel(new Mock<IDialogService>(), slotService, undoRedo);
        vm.CurrentSave = sav;
        undoRedo.Initialize(sav);

        slotService.RequestMove(SlotLocation.FromParty(0), SlotLocation.FromBox(0, 0), clone: false);

        Assert.Equal(1, undoRedo.ChangeCount);
        Assert.Equal(2, sav.GetPartySlotAtIndex(0).Species);
        Assert.Equal(3, sav.GetPartySlotAtIndex(1).Species);
        Assert.Equal(0, sav.GetPartySlotAtIndex(2).Species);
        Assert.Equal(1, sav.GetBoxSlotAtIndex(0, 0).Species);

        undoRedo.Undo();

        Assert.Equal(1, sav.GetPartySlotAtIndex(0).Species);
        Assert.Equal(2, sav.GetPartySlotAtIndex(1).Species);
        Assert.Equal(3, sav.GetPartySlotAtIndex(2).Species);
        Assert.Equal(0, sav.GetBoxSlotAtIndex(0, 0).Species);

        undoRedo.Redo();

        Assert.Equal(2, sav.GetPartySlotAtIndex(0).Species);
        Assert.Equal(3, sav.GetPartySlotAtIndex(1).Species);
        Assert.Equal(0, sav.GetPartySlotAtIndex(2).Species);
        Assert.Equal(1, sav.GetBoxSlotAtIndex(0, 0).Species);
    }

    [Fact]
    public void CopyingIntoOccupiedSlot_RequiresConfirmationAndCancelPreservesBothSlots()
    {
        var sav = new SAV6XY();
        sav.SetBoxSlotAtIndex(new PK6 { Species = 25 }, 0, 0);
        sav.SetBoxSlotAtIndex(new PK6 { Species = 1 }, 0, 1);

        var slotService = new SlotService();
        var undoRedo = new UndoRedoService();
        var dialogService = new Mock<IDialogService>();
        dialogService
            .Setup(d => d.ShowConfirmationAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync(false);
        var vm = CreateViewModel(dialogService, slotService, undoRedo);
        vm.CurrentSave = sav;
        undoRedo.Initialize(sav);

        slotService.RequestMove(SlotLocation.FromBox(0, 0), SlotLocation.FromBox(0, 1), clone: true);

        dialogService.Verify(
            d => d.ShowConfirmationAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()),
            Times.Once);
        Assert.Equal(25, sav.GetBoxSlotAtIndex(0, 0).Species);
        Assert.Equal(1, sav.GetBoxSlotAtIndex(0, 1).Species);
        Assert.Equal(0, undoRedo.ChangeCount);
    }

    [Fact]
    public void CopyingIntoOccupiedSlot_AppliesAsOneUndoableChangeAfterConfirmation()
    {
        var sav = new SAV6XY();
        sav.SetBoxSlotAtIndex(new PK6 { Species = 25 }, 0, 0);
        sav.SetBoxSlotAtIndex(new PK6 { Species = 1 }, 0, 1);

        var slotService = new SlotService();
        var undoRedo = new UndoRedoService();
        var dialogService = new Mock<IDialogService>();
        dialogService
            .Setup(d => d.ShowConfirmationAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync(true);
        var vm = CreateViewModel(dialogService, slotService, undoRedo);
        vm.CurrentSave = sav;
        undoRedo.Initialize(sav);

        slotService.RequestMove(SlotLocation.FromBox(0, 0), SlotLocation.FromBox(0, 1), clone: true);

        dialogService.Verify(
            d => d.ShowConfirmationAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()),
            Times.Once);
        Assert.Equal(25, sav.GetBoxSlotAtIndex(0, 0).Species);
        Assert.Equal(25, sav.GetBoxSlotAtIndex(0, 1).Species);
        Assert.Equal(1, undoRedo.ChangeCount);

        undoRedo.Undo();
        Assert.Equal(25, sav.GetBoxSlotAtIndex(0, 0).Species);
        Assert.Equal(1, sav.GetBoxSlotAtIndex(0, 1).Species);
    }

    [Fact]
    public void CopyingIntoEmptySlot_DoesNotPromptForConfirmation()
    {
        var sav = new SAV6XY();
        sav.SetBoxSlotAtIndex(new PK6 { Species = 25 }, 0, 0);

        var slotService = new SlotService();
        var undoRedo = new UndoRedoService();
        var dialogService = new Mock<IDialogService>();
        var vm = CreateViewModel(dialogService, slotService, undoRedo);
        vm.CurrentSave = sav;
        undoRedo.Initialize(sav);

        slotService.RequestMove(SlotLocation.FromBox(0, 0), SlotLocation.FromBox(0, 1), clone: true);

        dialogService.Verify(
            d => d.ShowConfirmationAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()),
            Times.Never);
        Assert.Equal(25, sav.GetBoxSlotAtIndex(0, 0).Species);
        Assert.Equal(25, sav.GetBoxSlotAtIndex(0, 1).Species);
        Assert.Equal(1, undoRedo.ChangeCount);
    }

    [Fact]
    public void MovingBetweenOccupiedSlots_SwapsWithoutConfirmation()
    {
        var sav = new SAV6XY();
        sav.SetBoxSlotAtIndex(new PK6 { Species = 25 }, 0, 0);
        sav.SetBoxSlotAtIndex(new PK6 { Species = 1 }, 0, 1);

        var slotService = new SlotService();
        var undoRedo = new UndoRedoService();
        var dialogService = new Mock<IDialogService>();
        var vm = CreateViewModel(dialogService, slotService, undoRedo);
        vm.CurrentSave = sav;
        undoRedo.Initialize(sav);

        slotService.RequestMove(SlotLocation.FromBox(0, 0), SlotLocation.FromBox(0, 1), clone: false);

        dialogService.Verify(
            d => d.ShowConfirmationAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()),
            Times.Never);
        Assert.Equal(1, sav.GetBoxSlotAtIndex(0, 0).Species);
        Assert.Equal(25, sav.GetBoxSlotAtIndex(0, 1).Species);
        Assert.Equal(1, undoRedo.ChangeCount);
    }

    [Fact]
    public void UnsupportedLgpePartyTransferLeavesSaveAndUndoHistoryUnchanged()
    {
        var sav = new SAV7b();
        sav.SetBoxSlotAtIndex(new PB7 { Species = 25 }, 0, 0);

        var slotService = new SlotService();
        var undoRedo = new UndoRedoService();
        var vm = CreateViewModel(new Mock<IDialogService>(), slotService, undoRedo);
        vm.CurrentSave = sav;
        undoRedo.Initialize(sav);

        slotService.RequestMove(SlotLocation.FromBox(0, 0), SlotLocation.FromParty(0), clone: false);

        Assert.Equal(0, undoRedo.ChangeCount);
        Assert.False(undoRedo.CanUndo);
        Assert.Equal(25, sav.GetBoxSlotAtIndex(0, 0).Species);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task ConfirmedCopyAbortsWhenEitherSlotChangesDuringPrompt(bool changeSource)
    {
        var sav = new SAV6XY();
        sav.SetBoxSlotAtIndex(new PK6 { Species = 25 }, 0, 0);
        sav.SetBoxSlotAtIndex(new PK6 { Species = 1 }, 0, 1);

        var confirmation = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var dialogService = new Mock<IDialogService>();
        dialogService
            .Setup(d => d.ShowConfirmationAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
            .Returns(confirmation.Task);
        var slotService = new SlotService();
        var undoRedo = new UndoRedoService();
        var vm = CreateViewModel(dialogService, slotService, undoRedo);
        vm.CurrentSave = sav;
        undoRedo.Initialize(sav);

        var operation = InvokeMoveSlotAsync(
            vm,
            SlotLocation.FromBox(0, 0),
            SlotLocation.FromBox(0, 1),
            clone: true);
        sav.SetBoxSlotAtIndex(new PK6 { Species = 4 }, 0, changeSource ? 0 : 1);
        confirmation.SetResult(true);
        await operation;

        Assert.Equal(changeSource ? 4 : 25, sav.GetBoxSlotAtIndex(0, 0).Species);
        Assert.Equal(changeSource ? 1 : 4, sav.GetBoxSlotAtIndex(0, 1).Species);
        Assert.Equal(0, undoRedo.ChangeCount);
    }

    [Fact]
    public async Task ConfirmedCopyAbortsWhenSaveSessionChangesDuringPrompt()
    {
        var sav = new SAV6XY();
        sav.SetBoxSlotAtIndex(new PK6 { Species = 25 }, 0, 0);
        sav.SetBoxSlotAtIndex(new PK6 { Species = 1 }, 0, 1);

        var confirmation = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var dialogService = new Mock<IDialogService>();
        dialogService
            .Setup(d => d.ShowConfirmationAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
            .Returns(confirmation.Task);
        var slotService = new SlotService();
        var undoRedo = new UndoRedoService();
        var vm = CreateViewModel(dialogService, slotService, undoRedo);
        vm.CurrentSave = sav;
        undoRedo.Initialize(sav);

        var operation = InvokeMoveSlotAsync(
            vm,
            SlotLocation.FromBox(0, 0),
            SlotLocation.FromBox(0, 1),
            clone: true);
        slotService.ResetSession();
        confirmation.SetResult(true);
        await operation;

        Assert.Equal(25, sav.GetBoxSlotAtIndex(0, 0).Species);
        Assert.Equal(1, sav.GetBoxSlotAtIndex(0, 1).Species);
        Assert.Equal(0, undoRedo.ChangeCount);
    }

    [Fact]
    public async Task ExternalReplacementIntoOccupiedSlotRequiresConfirmationAndIsUndoable()
    {
        var sav = new SAV6XY();
        sav.SetBoxSlotAtIndex(new PK6 { Species = 1 }, 0, 0);

        var dialogService = new Mock<IDialogService>();
        dialogService
            .Setup(d => d.ShowConfirmationAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync(true);
        var slotService = new SlotService();
        var undoRedo = new UndoRedoService();
        var vm = CreateViewModel(dialogService, slotService, undoRedo);
        vm.CurrentSave = sav;
        undoRedo.Initialize(sav);

        await slotService.RequestReplaceAsync(
            slotService.SessionId,
            SlotLocation.FromBox(0, 0),
            new PK6 { Species = 25 });

        dialogService.Verify(
            d => d.ShowConfirmationAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()),
            Times.Once);
        Assert.Equal(25, sav.GetBoxSlotAtIndex(0, 0).Species);
        Assert.Equal(1, undoRedo.ChangeCount);

        undoRedo.Undo();
        Assert.Equal(1, sav.GetBoxSlotAtIndex(0, 0).Species);
    }

    [Fact]
    public async Task CancellingExternalReplacementPreservesOccupiedSlotAndHistory()
    {
        var sav = new SAV6XY();
        sav.SetBoxSlotAtIndex(new PK6 { Species = 1 }, 0, 0);

        var dialogService = new Mock<IDialogService>();
        dialogService
            .Setup(d => d.ShowConfirmationAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync(false);
        var slotService = new SlotService();
        var undoRedo = new UndoRedoService();
        var vm = CreateViewModel(dialogService, slotService, undoRedo);
        vm.CurrentSave = sav;
        undoRedo.Initialize(sav);

        await slotService.RequestReplaceAsync(
            slotService.SessionId,
            SlotLocation.FromBox(0, 0),
            new PK6 { Species = 25 });

        Assert.Equal(1, sav.GetBoxSlotAtIndex(0, 0).Species);
        Assert.Equal(0, undoRedo.ChangeCount);
    }

    [Fact]
    public async Task ExternalReplacementIntoEmptySlotSkipsConfirmationAndIsUndoable()
    {
        var sav = new SAV6XY();
        var dialogService = new Mock<IDialogService>();
        var slotService = new SlotService();
        var undoRedo = new UndoRedoService();
        var vm = CreateViewModel(dialogService, slotService, undoRedo);
        vm.CurrentSave = sav;
        undoRedo.Initialize(sav);

        await slotService.RequestReplaceAsync(
            slotService.SessionId,
            SlotLocation.FromBox(0, 0),
            new PK6 { Species = 25 });

        dialogService.Verify(
            d => d.ShowConfirmationAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()),
            Times.Never);
        Assert.Equal(25, sav.GetBoxSlotAtIndex(0, 0).Species);
        Assert.Equal(1, undoRedo.ChangeCount);

        undoRedo.Undo();
        Assert.Equal(0, sav.GetBoxSlotAtIndex(0, 0).Species);
    }
}
