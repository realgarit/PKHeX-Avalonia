using System.Linq;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Moq;
using PKHeX.Application.Abstractions;
using PKHeX.Avalonia.Services;
using PKHeX.Avalonia.Tests.Harness;
using PKHeX.Avalonia.Views;
using PKHeX.Core;
using PKHeX.Infrastructure;
using PKHeX.Presentation.Models;
using PKHeX.Presentation.ViewModels;

namespace PKHeX.Avalonia.Tests;

/// <summary>
/// Issue #529: dragging the Pokémon editor sprite onto a box or party slot sets that slot from
/// the editor, the same as Shift+Click, and only for the save session the viewer shows.
/// </summary>
public class EditorSpriteDragTests
{
    [Fact]
    public void EditorPayloadMatchesOnlyItsOwnSession()
    {
        var session = Guid.NewGuid();
        var transfer = SlotDragTransfer.CreateEditor(session, null, null);

        Assert.True(SlotDragTransfer.IsEditorPayload(transfer, session));
        Assert.False(SlotDragTransfer.IsEditorPayload(transfer, Guid.NewGuid()));
        Assert.True(SlotDragTransfer.HasEditorPayload(transfer));
        Assert.True(SlotDragTransfer.HasCustomPayload(transfer));
        Assert.Null(SlotDragTransfer.TryGet(transfer));
        Assert.False(SlotDragTransfer.HasCrossSessionEntityFile(transfer, Guid.NewGuid()));
    }

    [Fact]
    public void EmptySessionNeverMatches()
    {
        var transfer = SlotDragTransfer.CreateEditor(Guid.Empty, null, null);

        Assert.False(SlotDragTransfer.IsEditorPayload(transfer, Guid.Empty));
    }

    [Fact]
    public void SlotPayloadIsNotAnEditorPayload()
    {
        var session = Guid.NewGuid();
        var transfer = SlotDragTransfer.Create(new SlotDragData(SlotLocation.FromBox(0, 1), session));

        Assert.False(SlotDragTransfer.HasEditorPayload(transfer));
        Assert.False(SlotDragTransfer.IsEditorPayload(transfer, session));
    }

    [AvaloniaFact]
    public void DroppingEditorSpriteOnBoxSlotRequestsSet()
    {
        using var app = new HeadlessAppFixture();
        var save = new SAV6XY();
        var service = new SlotService();
        var vm = new BoxViewerViewModel(save, Mock.Of<ISpriteRenderer>(), service);
        var view = new BoxViewer { DataContext = vm };
        var window = new Window { Content = view, Width = 520, Height = 480 };
        try
        {
            window.Show(); Pump(window);
            var requests = new List<SlotLocation>();
            service.SetRequested += requests.Add;
            var button = view.GetVisualDescendants().OfType<Button>().First(b => b.Tag is SlotData { Slot: 3 });

            var stale = SlotDragTransfer.CreateEditor(Guid.NewGuid(), null, null);
            var staleOver = new DragEventArgs(DragDrop.DragOverEvent, stale, button, default, KeyModifiers.None);
            button.RaiseEvent(staleOver);
            Assert.Equal(DragDropEffects.None, staleOver.DragEffects);
            button.RaiseEvent(new DragEventArgs(DragDrop.DropEvent, stale, button, default, KeyModifiers.None));
            Assert.Empty(requests);

            var transfer = SlotDragTransfer.CreateEditor(vm.SessionId, null, null);
            var over = new DragEventArgs(DragDrop.DragOverEvent, transfer, button, default, KeyModifiers.None);
            button.RaiseEvent(over);
            Assert.True(over.Handled);
            Assert.Equal(DragDropEffects.Copy, over.DragEffects);

            button.RaiseEvent(new DragEventArgs(DragDrop.DropEvent, transfer, button, default, KeyModifiers.None));
            var request = Assert.Single(requests);
            Assert.Equal(SlotLocation.FromBox(vm.CurrentBox, 3), request);
        }
        finally
        {
            window.Content = null;
            window.Close();
        }
    }

    [AvaloniaFact]
    public void DroppingEditorSpriteOnPartySlotRequestsSet()
    {
        using var app = new HeadlessAppFixture();
        var save = new SAV6XY();
        save.SetPartySlotAtIndex(new PK6 { Species = 25 }, 0);
        var service = new SlotService();
        var vm = new PartyViewerViewModel(save, Mock.Of<ISpriteRenderer>(), service);
        var view = new PartyViewer { DataContext = vm };
        var window = new Window { Content = view, Width = 520, Height = 480 };
        try
        {
            window.Show(); Pump(window);
            var requests = new List<SlotLocation>();
            service.SetRequested += requests.Add;
            var button = view.GetVisualDescendants().OfType<Button>().First(b => b.Tag is PartySlotData { Slot: 0 });

            var transfer = SlotDragTransfer.CreateEditor(vm.SessionId, null, null);
            var over = new DragEventArgs(DragDrop.DragOverEvent, transfer, button, default, KeyModifiers.None);
            button.RaiseEvent(over);
            Assert.Equal(DragDropEffects.Copy, over.DragEffects);

            button.RaiseEvent(new DragEventArgs(DragDrop.DropEvent, transfer, button, default, KeyModifiers.None));
            var request = Assert.Single(requests);
            Assert.Equal(SlotLocation.FromParty(0), request);
        }
        finally
        {
            window.Content = null;
            window.Close();
        }
    }

    private static void Pump(Window window)
    { Dispatcher.UIThread.RunJobs(); window.UpdateLayout(); AvaloniaHeadlessPlatform.ForceRenderTimerTick(); Dispatcher.UIThread.RunJobs(); }
}
