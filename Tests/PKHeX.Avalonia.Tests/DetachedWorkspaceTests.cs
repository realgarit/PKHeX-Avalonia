using System.Linq;
using System.Reflection;
using System.Windows.Input;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Moq;
using PKHeX.Avalonia.Services;
using PKHeX.Avalonia.Tests.Harness;
using PKHeX.Avalonia.Views;
using PKHeX.Core;
using PKHeX.Presentation.Localization;
using PKHeX.Presentation.Models;
using PKHeX.Presentation.ViewModels;

namespace PKHeX.Avalonia.Tests;

/// <summary>
/// Regression coverage for the detached Box/Party workspaces. The same existing ViewModel must
/// drive both the embedded and modeless views so selection, drag payloads, refreshes, and save
/// lifetime all remain shared.
/// </summary>
public sealed class DetachedWorkspaceTests
{
    [AvaloniaFact]
    public void ViewLocator_Resolves_BoxAndPartyViewerViews()
    {
        var save = new SAV6XY();
        var spriteRenderer = Mock.Of<ISpriteRenderer>();
        var box = new BoxViewerViewModel(save, spriteRenderer);
        var party = new PartyViewerViewModel(save, spriteRenderer);

        var boxView = PKHeX.Avalonia.ViewLocator.Build(box);
        var partyView = PKHeX.Avalonia.ViewLocator.Build(party);

        Assert.IsType<BoxViewer>(boxView);
        Assert.IsType<PartyViewer>(partyView);
        Assert.Same(box, boxView.DataContext);
        Assert.Same(party, partyView.DataContext);
    }

    [Fact]
    public void HeadlessWindowService_ModelsLiveSingletonAndCurrentFocus()
    {
        var service = new NoopWindowService();
        var box = new object();
        var party = new object();

        service.ShowTool(box, "Box");

        Assert.Equal(1, service.ActiveToolCount);
        Assert.Contains(box, service.ActiveTools);
        Assert.Same(box, service.FocusedTool);

        service.ShowTool(box, "Box");

        Assert.Equal(1, service.ActiveToolCount);
        Assert.Single(service.ShownTools);
        Assert.Single(service.FocusedTools);
        Assert.Same(box, service.FocusedTool);

        service.ShowTool(party, "Party");

        Assert.Equal(2, service.ActiveToolCount);
        Assert.Contains(party, service.ActiveTools);
        Assert.Same(party, service.FocusedTool);

        service.CloseAllTools();

        Assert.Empty(service.ActiveTools);
        Assert.Null(service.FocusedTool);

        service.ShowTool(box, "Box");

        Assert.Equal(1, service.ActiveToolCount);
        Assert.Same(box, service.FocusedTool);
        Assert.Equal(3, service.ShownTools.Count);
    }

    [AvaloniaFact(Skip = "Avalonia.Headless does not provide a classic desktop lifetime; command invocation is covered by the headless window-service double below.")]
    public void WindowService_UsesOneWindowPerExistingViewModel_AndClosesToolsTogether()
    {
        var application = global::Avalonia.Application.Current;
        Assert.NotNull(application);
        var lifetime = Assert.IsAssignableFrom<IClassicDesktopStyleApplicationLifetime>(application!.ApplicationLifetime);
        var previousMainWindow = lifetime.MainWindow;
        var mainWindow = new Window { Width = 640, Height = 480 };
        lifetime.MainWindow = mainWindow;
        mainWindow.Show();

        try
        {
            var service = new WindowService();
            var save = new SAV6XY();
            var box = new BoxViewerViewModel(save, Mock.Of<ISpriteRenderer>());
            var party = new PartyViewerViewModel(save, Mock.Of<ISpriteRenderer>());
            var initialWindowCount = lifetime.Windows.Count;

            service.ShowTool(box, "Box");
            Dispatcher.UIThread.RunJobs();

            var tool = lifetime.Windows.SingleOrDefault(window =>
                window.Content is Control content && ReferenceEquals(content.DataContext, box));
            Assert.NotNull(tool);
            Assert.IsType<BoxViewer>(tool!.Content);
            Assert.Equal(initialWindowCount + 1, lifetime.Windows.Count);

            service.ShowTool(party, "Party");
            Dispatcher.UIThread.RunJobs();

            var partyTool = lifetime.Windows.SingleOrDefault(window =>
                window.Content is Control content && ReferenceEquals(content.DataContext, party));
            Assert.NotNull(partyTool);
            Assert.IsType<PartyViewer>(partyTool!.Content);
            Assert.Equal(initialWindowCount + 2, lifetime.Windows.Count);

            service.ShowTool(box, "Box");
            Dispatcher.UIThread.RunJobs();
            Assert.Equal(initialWindowCount + 2, lifetime.Windows.Count);
            Assert.Same(tool, lifetime.Windows.Single(window => ReferenceEquals(window.Content, tool.Content)));

            service.ShowTool(party, "Party");
            Dispatcher.UIThread.RunJobs();
            Assert.Equal(initialWindowCount + 2, lifetime.Windows.Count);
            Assert.Contains(partyTool, lifetime.Windows);

            service.CloseAllTools();
            Dispatcher.UIThread.RunJobs();
            Assert.Equal(initialWindowCount, lifetime.Windows.Count);
        }
        finally
        {
            mainWindow.Close();
            lifetime.MainWindow = previousMainWindow;
            Dispatcher.UIThread.RunJobs();
        }
    }

    [AvaloniaFact]
    public void DetachedCommands_UseExistingViewModels_AndSaveSwitchInvalidatesThem()
    {
        using var app = new HeadlessAppFixture();
        var firstSave = new SAV6XY();
        firstSave.SetBoxSlotAtIndex(new PK6 { Species = 25 }, 0, 0);
        app.LoadSaveInstance(firstSave);

        var oldBox = app.BoxViewer;
        var oldParty = app.ViewModel.PartyViewer;
        Assert.NotNull(oldBox);
        Assert.NotNull(oldParty);
        var oldPayload = CreateDragData(oldBox!, 0);

        ExecuteCommand(oldBox!, "OpenDetachedToolCommand");
        ExecuteCommand(oldParty!, "OpenDetachedToolCommand");
        ExecuteCommand(app.ViewModel, "OpenBoxWorkspaceCommand");
        ExecuteCommand(app.ViewModel, "OpenPartyWorkspaceCommand");

        Assert.Equal(2, app.Windows.ActiveToolCount);
        Assert.Equal(2, app.Windows.ShownTools.Count);
        Assert.Equal(2, app.Windows.FocusedTools.Count);
        Assert.Contains(app.Windows.ShownTools, tool => ReferenceEquals(tool.ViewModel, oldBox));
        Assert.Contains(app.Windows.ShownTools, tool => ReferenceEquals(tool.ViewModel, oldParty));
        Assert.Same(oldBox, app.Windows.FocusedTools[0]);
        Assert.Same(oldParty, app.Windows.FocusedTools[1]);
        Assert.Same(oldParty, app.Windows.ShownTools[^1].ViewModel);
        Assert.Equal(LocalizedStrings.Instance["Tab_Box"], app.Windows.ShownTools[0].Title);
        Assert.Equal(LocalizedStrings.Instance["Tab_Party"], app.Windows.ShownTools[^1].Title);

        var closeCountBeforeSwitch = app.Windows.CloseAllToolsCount;
        var secondSave = new SAV6XY();
        secondSave.SetBoxSlotAtIndex(new PK6 { Species = 25 }, 0, 0);
        app.LoadSaveInstance(secondSave);

        Assert.Equal(closeCountBeforeSwitch + 1, app.Windows.CloseAllToolsCount);
        Assert.Equal(0, app.Windows.ActiveToolCount);
        Assert.NotSame(oldBox, app.BoxViewer);
        Assert.NotSame(oldParty, app.ViewModel.PartyViewer);

        var currentBox = app.BoxViewer!;
        var newPayload = CreateDragData(currentBox, 0);
        Assert.NotEqual(ReadSessionId(oldPayload), ReadSessionId(newPayload));
        var currentParty = app.ViewModel.PartyViewer!;
        currentParty.RequestMoveCommand.Execute((oldPayload, currentParty.Slots[0], false));
        Assert.Equal(0, secondSave.GetPartySlotAtIndex(0).Species);
        var closeCountBeforeClose = app.Windows.CloseAllToolsCount;
        ExecuteCommand(app.ViewModel, "CloseFileCommand");

        Assert.Equal(closeCountBeforeClose + 1, app.Windows.CloseAllToolsCount);
        Assert.Equal(0, app.Windows.ActiveToolCount);
        Assert.Null(app.ViewModel.CurrentSave);
        // Closing the save invalidates the service session, but a detached viewer keeps its own
        // immutable identity; its already-captured payload therefore cannot silently become a new
        // session payload after the close.
        Assert.Equal(ReadSessionId(newPayload), ReadSessionId(CreateDragData(currentBox, 0)));
    }

    [Fact]
    public void RepeatedCrossWindowDrops_MoveAndCopyThroughSharedSaveAndRefreshBothViews()
    {
        var save = new SAV6XY();
        save.SetBoxSlotAtIndex(new PK6 { Species = 25 }, 0, 0);
        save.SetPartySlotAtIndex(new PK6 { Species = 1 }, 0);

        var slotService = new SlotService();
        var undoRedo = new UndoRedoService();
        var main = CreateMainWindowViewModel(slotService, undoRedo);
        main.CurrentSave = save;
        undoRedo.Initialize(save);

        var box = new BoxViewerViewModel(save, Mock.Of<ISpriteRenderer>(), slotService);
        var party = new PartyViewerViewModel(save, Mock.Of<ISpriteRenderer>(), slotService);
        main.BoxViewer = box;
        main.PartyViewer = party;

        // Simulate a drop from the embedded/detached Box view into Party slot 2.
        party.RequestMoveCommand.Execute((
            CreateDragData(box, 0),
            party.Slots[1],
            false));

        Assert.Equal(0, save.GetBoxSlotAtIndex(0, 0).Species);
        Assert.Equal(25, save.GetPartySlotAtIndex(1).Species);
        Assert.Equal(25, party.Slots[1].Species);
        Assert.Equal(0, box.Slots[0].Species);

        // Simulate a second drop in the opposite direction, this time with Ctrl-copy.
        box.RequestMoveCommand.Execute((
            CreateDragData(party, 1),
            box.Slots[1],
            true));

        Assert.Equal(25, save.GetPartySlotAtIndex(1).Species);
        Assert.Equal(25, save.GetBoxSlotAtIndex(0, 1).Species);
        Assert.Equal(25, box.Slots[1].Species);
        Assert.Equal(25, party.Slots[1].Species);
        Assert.Equal(2, undoRedo.ChangeCount);
    }

    [Fact]
    public void SlotService_RejectsAllRequestsFromPreviousSession()
    {
        var service = new SlotService();
        var staleSession = service.SessionId;
        var viewCount = 0;
        var setCount = 0;
        var deleteCount = 0;
        var moveCount = 0;
        service.ViewRequested += _ => viewCount++;
        service.SetRequested += _ => setCount++;
        service.DeleteRequested += _ => deleteCount++;
        service.MoveRequested += (_, _, _) => moveCount++;

        service.ResetSession();
        service.RequestView(staleSession, SlotLocation.FromBox(0, 0));
        service.RequestSet(staleSession, SlotLocation.FromBox(0, 0));
        service.RequestDelete(staleSession, SlotLocation.FromBox(0, 0));
        service.RequestMove(staleSession, SlotLocation.FromBox(0, 0), SlotLocation.FromParty(0), false);
        Assert.Equal(0, viewCount);
        Assert.Equal(0, setCount);
        Assert.Equal(0, deleteCount);
        Assert.Equal(0, moveCount);

        service.RequestView(service.SessionId, SlotLocation.FromBox(0, 0));
        service.RequestSet(service.SessionId, SlotLocation.FromBox(0, 0));
        service.RequestDelete(service.SessionId, SlotLocation.FromBox(0, 0));
        service.RequestMove(service.SessionId, SlotLocation.FromBox(0, 0), SlotLocation.FromParty(0), false);
        Assert.Equal(1, viewCount);
        Assert.Equal(1, setCount);
        Assert.Equal(1, deleteCount);
        Assert.Equal(1, moveCount);
    }

    [Fact]
    public void ViewerSessionIdentityDoesNotFollowSlotServiceAfterSaveSessionReset()
    {
        var slotService = new SlotService();
        var viewer = new BoxViewerViewModel(new SAV6XY(), Mock.Of<ISpriteRenderer>(), slotService);
        var originalSession = viewer.SessionId;
        var originalPayload = viewer.CreateDragData(0);

        slotService.ResetSession();

        Assert.Equal(originalSession, viewer.SessionId);
        Assert.Equal(originalSession, originalPayload.SessionId);
        Assert.NotEqual(originalSession, slotService.SessionId);
        Assert.Null(SlotDragTransfer.TryGet(SlotDragTransfer.Create(originalPayload), slotService.SessionId));
    }

    [Fact]
    public void PreviousSessionViewersRejectCommandsAndDirectMutations()
    {
        var save = new SAV6XY();
        save.SetBoxSlotAtIndex(new PK6 { Species = 25 }, 0, 0);
        save.SetPartySlotAtIndex(new PK6 { Species = 1 }, 0);

        var service = new SlotService();
        var windows = new NoopWindowService();
        var box = new BoxViewerViewModel(save, Mock.Of<ISpriteRenderer>(), service, windows);
        var party = new PartyViewerViewModel(save, Mock.Of<ISpriteRenderer>(), service, windowService: windows);
        var requestCount = 0;
        service.ViewRequested += _ => requestCount++;
        service.SetRequested += _ => requestCount++;
        service.DeleteRequested += _ => requestCount++;
        service.MoveRequested += (_, _, _) => requestCount++;

        service.ResetSession();

        box.ViewSlotCommand.Execute(box.Slots[0]);
        box.SetSlotCommand.Execute(box.Slots[0]);
        box.DeleteSlotCommand.Execute(box.Slots[0]);
        box.RequestMoveCommand.Execute((box.CreateDragData(0), box.Slots[1], false));
        box.OpenDetachedToolCommand.Execute(null);
        box.SetSlotPKM(0, new PK6 { Species = 4 });
        box.ClearSlot(0);

        party.ViewSlotCommand.Execute(party.Slots[0]);
        party.SetSlotCommand.Execute(party.Slots[0]);
        party.DeleteSlotCommand.Execute(party.Slots[0]);
        party.RequestMoveCommand.Execute((party.CreateDragData(0), party.Slots[1], false));
        party.OpenDetachedToolCommand.Execute(null);
        party.SetSlotPKM(0, new PK6 { Species = 4 });

        Assert.Equal(0, requestCount);
        Assert.Empty(windows.ShownTools);
        Assert.Equal(25, save.GetBoxSlotAtIndex(0, 0).Species);
        Assert.Equal(1, save.GetPartySlotAtIndex(0).Species);
    }

    private static void ExecuteCommand(object target, string propertyName)
    {
        var property = target.GetType().GetProperty(propertyName, BindingFlags.Instance | BindingFlags.Public);
        Assert.NotNull(property);
        var command = Assert.IsAssignableFrom<ICommand>(property!.GetValue(target));
        Assert.True(command.CanExecute(null), $"{propertyName} should be executable");
        command.Execute(null);
    }

    private static SlotDragData CreateDragData(object viewer, int slot)
    {
        var method = viewer.GetType().GetMethod("CreateDragData", BindingFlags.Instance | BindingFlags.Public);
        Assert.NotNull(method);
        return Assert.IsType<SlotDragData>(method!.Invoke(viewer, new object[] { slot }));
    }

    private static object ReadSessionId(SlotDragData data)
    {
        var property = data.GetType().GetProperty("SessionId", BindingFlags.Instance | BindingFlags.Public);
        Assert.NotNull(property);
        return property!.GetValue(data)!;
    }

    private static MainWindowViewModel CreateMainWindowViewModel(ISlotService slotService, UndoRedoService undoRedo) =>
        new(
            new Mock<ISaveFileGateway>().Object,
            new Mock<IDialogService>().Object,
            new Mock<IWindowService>().Object,
            new Mock<ISpriteRenderer>().Object,
            slotService,
            new Mock<IClipboardService>().Object,
            new Mock<IQrCodeService>().Object,
            UpdateTestDoubles.Coordinator(),
            new Mock<ISaveBackupService>().Object,
            new AppSettings(),
            new FakeSettingsStore(),
            new Mock<IThemeService>().Object,
            undoRedo,
            new LanguageService(),
            new Mock<IAutoLegalityService>().Object,
            new Mock<PKHeX.Application.Abstractions.LiveHex.ILiveHexService>().Object,
            new Mock<ILivingDexService>().Object,
            new Mock<PKHeX.Application.Abstractions.GiftRecords.IGiftRecordProvider>().Object);

}
