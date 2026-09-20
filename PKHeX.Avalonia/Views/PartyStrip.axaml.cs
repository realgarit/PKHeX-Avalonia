using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using PKHeX.Avalonia.Controls;

namespace PKHeX.Avalonia.Views;

/// <summary>
/// Compact, interactive party strip for the box workspace. It deliberately accepts the existing
/// <see cref="PKHeX.Presentation.ViewModels.PartyViewerViewModel"/> through DataContext so the strip
/// and detached PartyViewer share one slot collection and one operation service.
/// </summary>
public partial class PartyStrip : UserControl
{
    public PartyStrip()
    {
        InitializeComponent();
        PartySlotInteraction.Attach(this);
    }

    private void OnSlotPointerMoved(object? sender, PointerEventArgs e) => PartySlotInteraction.OnSlotPointerMoved(this, sender, e);

    private void OnSlotDragOver(object? sender, DragEventArgs e) => PartySlotInteraction.OnSlotDragOver(this, sender, e);

    private void OnSlotDragLeave(object? sender, DragEventArgs e) => PartySlotInteraction.OnSlotDragLeave(this, sender, e);

    private void OnSlotDrop(object? sender, DragEventArgs e) => PartySlotInteraction.OnSlotDrop(this, sender, e);

    private void OnSlotClicked(object? sender, RoutedEventArgs e) => PartySlotInteraction.OnSlotClicked(this, sender, e);

    private void OnSlotDoubleTapped(object? sender, TappedEventArgs e) => PartySlotInteraction.OnSlotDoubleTapped(this, sender, e);
}
