using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using Avalonia.VisualTree;
using PKHeX.Avalonia.Services;
using PKHeX.Presentation.Models;
using PKHeX.Presentation.ViewModels;

namespace PKHeX.Avalonia.Views;

public partial class PartyViewer : UserControl
{
    public PartyViewer()
    {
        InitializeComponent();

        // Button handles PointerPressed internally before normal handlers see it. Listen during the
        // tunnel phase so modifier actions run before Button consumes the press and raises Click.
        AddHandler(InputElement.PointerPressedEvent, OnSlotPointerPressed, RoutingStrategies.Tunnel, handledEventsToo: true);

        // Focus the control when it becomes visible for keyboard navigation
        AttachedToVisualTree += (_, _) => Focus();
    }

    private Point _dragStartPoint;
    private bool _isDragging;

    private void OnSlotPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        var button = FindSlotButton(e.Source as Visual);
        if (button?.Tag is not PartySlotData slot || DataContext is not PartyViewerViewModel vm)
            return;
        
        _dragStartPoint = e.GetPosition(this);

        // Only handle left-click for modifier actions
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
        // Normal click without modifiers - let Click event handle it for selection
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

    private async void OnSlotPointerMoved(object? sender, PointerEventArgs e)
    {
        if (sender is not Button button || !e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
            return;

        var currentPoint = e.GetPosition(this);
        var delta = currentPoint - _dragStartPoint;
        if (Math.Abs(delta.X) < 5 && Math.Abs(delta.Y) < 5)
            return;

        if (_isDragging || button.Tag is not PartySlotData slot || slot.IsEmpty || DataContext is not PartyViewerViewModel vm)
            return;

        _isDragging = true;
        try
        {
            // No await between here and DoDragDropAsync: on macOS, yielding the pointer-moved
            // frame means AppKit's [NSApp currentEvent] is no longer the live mouse-down event
            // and the native drag session fails to start. Payload preparation must be synchronous.
            var pk = vm.GetSlotPKM(slot.Slot);
            var storageProvider = TopLevel.GetTopLevel(this)?.StorageProvider;
            var data = SlotDragTransfer.Create(vm.CreateDragData(slot.Slot), pk, storageProvider);

            await DragDrop.DoDragDropAsync(e, data, DragDropEffects.Move | DragDropEffects.Copy);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Trace.TraceWarning($"Party slot drag failed: {ex.Message}");
        }
        finally
        {
            _isDragging = false;
        }
    }

    private void OnSlotDragOver(object? sender, DragEventArgs e)
    {
        if (sender is not Button button || button.Tag is not PartySlotData destSlot || DataContext is not PartyViewerViewModel vm)
            return;

        var data = SlotDragTransfer.TryGet(e.DataTransfer, vm.SessionId);
        if (data != null)
        {
            e.DragEffects = data.Source.Equals(destSlot.Location)
                ? DragDropEffects.None
                : DragDropEffects.Move;
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

    private async void OnSlotDrop(object? sender, DragEventArgs e)
    {
        if (sender is not Button button || button.Tag is not PartySlotData destSlot || DataContext is not PartyViewerViewModel vm)
            return;

        // In-app move/clone between box/party slots (existing behavior).
        var data = SlotDragTransfer.TryGet(e.DataTransfer, vm.SessionId);
        if (data != null)
        {
            vm.RequestMoveCommand.Execute((data, destSlot, e.KeyModifiers.HasFlag(KeyModifiers.Control)));
            e.Handled = true;
            return;
        }

        // OS file(s) dropped from Finder/Explorer/desktop.
        var files = e.DataTransfer.TryGetFiles();
        if (files is not { Length: > 0 })
            return;

        e.Handled = true;
        var paths = files.Select(f => f.TryGetLocalPath()).OfType<string>().ToList();
        if (paths.Count == 0)
            return;

        await vm.HandleFileDropAsync(paths, destSlot.Slot);
    }

    private void OnSlotClicked(object? sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: PartySlotData slot } || DataContext is not PartyViewerViewModel vm)
            return;
        
        // Normal click = Select (modifier clicks are handled by PointerPressed)
        vm.SelectSlotByClickCommand.Execute(slot);
    }

    private void OnSlotDoubleTapped(object? sender, TappedEventArgs e)
    {
        if (sender is Button { Tag: PartySlotData slot } && DataContext is PartyViewerViewModel vm)
        {
            vm.SelectSlotByClickCommand.Execute(slot);
            vm.ActivateSlotCommand.Execute(null);
        }
    }
}
