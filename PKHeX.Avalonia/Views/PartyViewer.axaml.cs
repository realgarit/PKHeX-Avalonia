using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using PKHeX.Avalonia.Models;
using PKHeX.Avalonia.ViewModels;

namespace PKHeX.Avalonia.Views;

public partial class PartyViewer : UserControl
{
    public PartyViewer()
    {
        InitializeComponent();
        
        // Focus the control when it becomes visible for keyboard navigation
        AttachedToVisualTree += (_, _) => Focus();
    }

    private void OnSlotPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (sender is not Button { Tag: PartySlotData slot } || DataContext is not PartyViewerViewModel vm)
            return;
        
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
        // Normal click without modifiers - let Click event handle it for selection
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
