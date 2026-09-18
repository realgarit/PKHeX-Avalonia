using System.Linq;
using System.Runtime.CompilerServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using Avalonia.VisualTree;
using PKHeX.Avalonia.Services;
using PKHeX.Avalonia.Views;
using PKHeX.Presentation.Models;
using PKHeX.Presentation.ViewModels;

namespace PKHeX.Avalonia.Controls;

/// <summary>
/// Shared view-layer routing for party slots. Both the detached PartyViewer and the compact inline
/// PartyStrip use this helper so modifier clicks, drag/drop and double-click activation keep exactly
/// the same meaning in either presentation.
/// </summary>
internal static class PartySlotInteraction
{
    private sealed class InteractionState
    {
        public Point DragStartPoint;
        public bool IsDragging;
    }

    private static readonly ConditionalWeakTable<UserControl, InteractionState> States = new();

    public static void Attach(UserControl owner)
    {
        _ = States.GetValue(owner, static _ => new InteractionState());
        owner.AddHandler(InputElement.PointerPressedEvent,
            (sender, args) => OnSlotPointerPressed(owner, args),
            RoutingStrategies.Tunnel,
            handledEventsToo: true);
        owner.AttachedToVisualTree += (_, _) => owner.Focus();
    }

    public static void OnSlotPointerPressed(UserControl owner, PointerPressedEventArgs e)
    {
        var button = FindSlotButton(e.Source as Visual);
        if (button?.Tag is not PartySlotData slot || owner.DataContext is not PartyViewerViewModel vm)
            return;

        States.GetValue(owner, static _ => new InteractionState()).DragStartPoint = e.GetPosition(owner);

        // Only modifier actions consume the pointer press. A normal click is left to Button.Click so
        // the existing selection command remains the single source of ordinary click semantics.
        if (!e.GetCurrentPoint(null).Properties.IsLeftButtonPressed)
            return;

        switch (SlotClickActionResolver.Resolve(e.KeyModifiers))
        {
            case SlotClickAction.View:
                vm.ViewSlotCommand.Execute(slot);
                break;
            case SlotClickAction.Set:
                vm.SetSlotCommand.Execute(slot);
                break;
            case SlotClickAction.Delete:
                vm.DeleteSlotCommand.Execute(slot);
                break;
            default:
                return;
        }

        e.Handled = true;
    }

    public static async void OnSlotPointerMoved(UserControl owner, object? sender, PointerEventArgs e)
    {
        if (sender is not Button button || !e.GetCurrentPoint(owner).Properties.IsLeftButtonPressed)
            return;

        var state = States.GetValue(owner, static _ => new InteractionState());
        var currentPoint = e.GetPosition(owner);
        var delta = currentPoint - state.DragStartPoint;
        if (Math.Abs(delta.X) < 5 && Math.Abs(delta.Y) < 5)
            return;

        if (state.IsDragging || button.Tag is not PartySlotData slot || slot.IsEmpty || owner.DataContext is not PartyViewerViewModel vm)
            return;

        state.IsDragging = true;
        try
        {
            // Keep payload creation synchronous until DoDragDropAsync starts. Native macOS drag
            // sessions require the call to happen in the live pointer-moved frame.
            var pk = vm.GetSlotPKM(slot.Slot);
            var storageProvider = TopLevel.GetTopLevel(owner)?.StorageProvider;
            var data = SlotDragTransfer.Create(vm.CreateDragData(slot.Slot), pk, storageProvider);

            await DragDrop.DoDragDropAsync(e, data, DragDropEffects.Move | DragDropEffects.Copy);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Trace.TraceWarning($"Party slot drag failed: {ex.Message}");
        }
        finally
        {
            state.IsDragging = false;
        }
    }

    public static void OnSlotDragOver(UserControl owner, object? sender, DragEventArgs e)
    {
        if (sender is not Button button || button.Tag is not PartySlotData destSlot || owner.DataContext is not PartyViewerViewModel vm)
            return;

        var data = SlotDragTransfer.TryGet(e.DataTransfer, vm.SessionId);
        if (data != null)
        {
            e.DragEffects = SlotDragTransfer.GetDropEffect(data, destSlot.Location, e.KeyModifiers);
        }
        else if (SlotDragTransfer.HasCustomPayload(e.DataTransfer))
        {
            // A stale in-app payload may also carry an exported file. The session token wins.
            e.DragEffects = DragDropEffects.None;
        }
        else if (e.DataTransfer.TryGetFiles() is { Length: > 0 })
        {
            e.DragEffects = DragDropEffects.Copy;
        }
        else
        {
            e.DragEffects = DragDropEffects.None;
        }

        e.Handled = true;
    }

    public static async void OnSlotDrop(UserControl owner, object? sender, DragEventArgs e)
    {
        if (sender is not Button button || button.Tag is not PartySlotData destSlot || owner.DataContext is not PartyViewerViewModel vm)
            return;

        var data = SlotDragTransfer.TryGet(e.DataTransfer, vm.SessionId);
        if (data != null)
        {
            vm.RequestMoveCommand.Execute((data, destSlot, e.KeyModifiers.HasFlag(KeyModifiers.Control)));
            e.Handled = true;
            return;
        }

        if (SlotDragTransfer.HasCustomPayload(e.DataTransfer))
        {
            // Ignore stale or invalid in-app payloads, including payloads that also carry a file.
            e.Handled = true;
            return;
        }

        var files = e.DataTransfer.TryGetFiles();
        if (files is not { Length: > 0 })
            return;

        e.Handled = true;
        var paths = files.Select(f => f.TryGetLocalPath()).OfType<string>().ToList();
        if (paths.Count == 0)
            return;

        await vm.HandleFileDropAsync(paths, destSlot.Slot);
    }

    public static void OnSlotClicked(UserControl owner, object? sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: PartySlotData slot } || owner.DataContext is not PartyViewerViewModel vm)
            return;

        // Normal click = Select. Modifier clicks were handled during tunneling.
        vm.SelectSlotByClickCommand.Execute(slot);
    }

    public static void OnSlotDoubleTapped(UserControl owner, object? sender, TappedEventArgs e)
    {
        if (sender is Button { Tag: PartySlotData slot } && owner.DataContext is PartyViewerViewModel vm)
        {
            vm.SelectSlotByClickCommand.Execute(slot);
            vm.ActivateSlotCommand.Execute(null);
        }
    }

    private static Button? FindSlotButton(Visual? source)
    {
        for (var visual = source; visual is not null; visual = visual.GetVisualParent())
        {
            if (visual is Button button)
                return button;
        }

        return null;
    }
}
