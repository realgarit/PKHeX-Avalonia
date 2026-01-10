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

    private void OnSlotClicked(object? sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: SlotData slot } && DataContext is BoxViewerViewModel vm)
        {
            vm.SelectSlotByClickCommand.Execute(slot);
        }
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
