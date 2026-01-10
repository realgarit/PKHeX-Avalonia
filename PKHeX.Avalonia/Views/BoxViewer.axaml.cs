using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using PKHeX.Avalonia.Models;
using PKHeX.Avalonia.ViewModels;

namespace PKHeX.Avalonia.Views;

public partial class BoxViewer : UserControl
{
    public BoxViewer()
    {
        InitializeComponent();

        // Focus the control when it becomes visible for keyboard navigation
        AttachedToVisualTree += (_, _) => Focus();
    }

    private Point _dragStartPoint;

    private void OnSlotPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (sender is not Button { Tag: SlotData slot } || DataContext is not BoxViewerViewModel vm)
            return;
        
        _dragStartPoint = e.GetPosition(this);

        // Only handle left-click for modifier actions
        if (!e.GetCurrentPoint(null).Properties.IsLeftButtonPressed)
            return;
        
        var modifiers = e.KeyModifiers;
        
        if (modifiers.HasFlag(KeyModifiers.Control))
        {
            // Ctrl+Click = View
            vm.ViewSlotCommand.Execute(slot);
            e.Handled = true;
        }
        else if (modifiers.HasFlag(KeyModifiers.Shift))
        {
            // Shift+Click = Set
            vm.SetSlotCommand.Execute(slot);
            e.Handled = true;
        }
        else if (modifiers.HasFlag(KeyModifiers.Alt))
        {
            // Alt+Click = Delete
            vm.DeleteSlotCommand.Execute(slot);
            e.Handled = true;
        }
        // Normal click without modifiers - let Click event handle it for selection
    }

    private async void OnSlotPointerMoved(object? sender, PointerEventArgs e)
    {
        if (sender is not Button button || !e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
            return;

        var currentPoint = e.GetPosition(this);
        var delta = currentPoint - _dragStartPoint;
        if (Math.Abs(delta.X) < 5 && Math.Abs(delta.Y) < 5)
            return;

        if (button.Tag is not SlotData slot || slot.IsEmpty)
            return;

        var data = new DataObject();
        data.Set("SlotDragData", new SlotDragData(slot.Location));

        await DragDrop.DoDragDrop(e, data, DragDropEffects.Move | DragDropEffects.Copy);
    }

    private void OnSlotDrop(object? sender, DragEventArgs e)
    {
        if (sender is not Button button || button.Tag is not SlotData destSlot || DataContext is not BoxViewerViewModel vm)
            return;

        var data = e.Data.Get("SlotDragData") as SlotDragData;
        if (data == null) return;

        bool clone = e.KeyModifiers.HasFlag(KeyModifiers.Control);
        
        // Use a service or ViewModel method to request the move
        vm.RequestMoveCommand.Execute((data, destSlot, e.KeyModifiers));
    }

    private void OnSlotClicked(object? sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: SlotData slot } || DataContext is not BoxViewerViewModel vm)
            return;
        
        // Normal click = Select (modifier clicks are handled by PointerPressed)
        vm.SelectSlotByClickCommand.Execute(slot);
    }

    private void OnSlotDoubleTapped(object? sender, TappedEventArgs e)
    {
        if (sender is Button { Tag: SlotData slot } && DataContext is BoxViewerViewModel vm)
        {
            // Select the slot first
            vm.SelectSlotByClickCommand.Execute(slot);
            // Then activate it
            vm.ActivateSlotCommand.Execute(null);
        }
    }
}
