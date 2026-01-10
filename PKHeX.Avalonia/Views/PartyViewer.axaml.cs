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

    private void OnSlotClicked(object? sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: PartySlotData slot } && DataContext is PartyViewerViewModel vm)
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
