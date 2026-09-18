using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using PKHeX.Avalonia.Controls;

namespace PKHeX.Avalonia.Views;

public partial class PartyViewer : UserControl
{
    public PartyViewer()
    {
        InitializeComponent();
        PartySlotInteraction.Attach(this);
    }

    private void OnSlotPointerMoved(object? sender, PointerEventArgs e) => PartySlotInteraction.OnSlotPointerMoved(this, sender, e);

    private void OnSlotDragOver(object? sender, DragEventArgs e) => PartySlotInteraction.OnSlotDragOver(this, sender, e);

    private void OnSlotDrop(object? sender, DragEventArgs e) => PartySlotInteraction.OnSlotDrop(this, sender, e);

    private void OnSlotClicked(object? sender, RoutedEventArgs e) => PartySlotInteraction.OnSlotClicked(this, sender, e);

    private void OnSlotDoubleTapped(object? sender, TappedEventArgs e) => PartySlotInteraction.OnSlotDoubleTapped(this, sender, e);
}
