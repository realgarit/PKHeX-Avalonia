using System;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using PKHeX.Avalonia.Services;
using PKHeX.Presentation.ViewModels;

namespace PKHeX.Avalonia.Views;

public partial class PokemonEditor : UserControl
{
    public PokemonEditor()
    {
        InitializeComponent();
    }

    private Point _spriteDragStart;
    // Avalonia 12 starts every platform drag from the press that began the gesture, so the press
    // is kept until the move threshold is met (same pattern as the box and party slots).
    private PointerPressedEventArgs? _spriteDragTrigger;
    private bool _isSpriteDragging;

    private void OnSpritePointerPressed(object? sender, PointerPressedEventArgs e)
    {
        _spriteDragTrigger = null;
        if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
            return;

        _spriteDragStart = e.GetPosition(this);
        _spriteDragTrigger = e;
    }

    // Dragging the sprite onto a box or party slot sets that slot from the editor, like upstream
    // PKHeX. Dropping it outside the app exports the entity file where the platform allows it.
    private async void OnSpritePointerMoved(object? sender, PointerEventArgs e)
    {
        if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
            return;

        var delta = e.GetPosition(this) - _spriteDragStart;
        if (Math.Abs(delta.X) < 5 && Math.Abs(delta.Y) < 5)
            return;

        if (_isSpriteDragging || _spriteDragTrigger is not { } trigger || DataContext is not PokemonEditorViewModel vm)
            return;

        var topLevel = TopLevel.GetTopLevel(this);
        if (topLevel?.DataContext is not MainWindowViewModel host)
            return;

        _isSpriteDragging = true;
        try
        {
            // No await before DoDragDropAsync: macOS needs the native drag to start in the live
            // pointer-moved frame, so the payload is built synchronously.
            var data = SlotDragTransfer.CreateEditor(host.SlotSessionId, vm.PreparePKM(), topLevel.StorageProvider);
            await DragDrop.DoDragDropAsync(trigger, data, DragDropEffects.Copy);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Trace.TraceWarning($"Editor sprite drag failed: {ex.Message}");
        }
        finally
        {
            _isSpriteDragging = false;
            _spriteDragTrigger = null;
        }
    }

    // Only OS file drops are meaningful here. In-app payloads (including the editor's own sprite
    // drag, which also carries its exported file) are not loaded back into the editor.
    private void OnEditorDragOver(object? sender, DragEventArgs e)
    {
        e.DragEffects = !SlotDragTransfer.HasEditorPayload(e.DataTransfer) && e.DataTransfer.TryGetFiles() is { Length: > 0 }
            ? DragDropEffects.Copy
            : DragDropEffects.None;
        e.Handled = true;
    }

    // Dropping an entity file directly onto the editor loads it without writing to a box/party
    // slot; dropping a save file routes through the host's "open save" path.
    private async void OnEditorDrop(object? sender, DragEventArgs e)
    {
        if (DataContext is not PokemonEditorViewModel vm)
            return;

        if (SlotDragTransfer.HasEditorPayload(e.DataTransfer))
        {
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

        await vm.HandleFileDropAsync(paths);
    }

    private void ExpBar_PointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (sender is not Border bar)
            return;
        if (!e.GetCurrentPoint(bar).Properties.IsLeftButtonPressed)
            return;
        ApplyExpFromPointer(bar, e);
    }

    private void ExpBar_PointerMoved(object? sender, PointerEventArgs e)
    {
        if (sender is not Border bar)
            return;
        // Only react while the left button is held (drag).
        if (!e.GetCurrentPoint(bar).Properties.IsLeftButtonPressed)
            return;
        ApplyExpFromPointer(bar, e);
    }

    private void ApplyExpFromPointer(Border bar, PointerEventArgs e)
    {
        if (DataContext is not PokemonEditorViewModel vm)
            return;

        var width = bar.Bounds.Width;
        if (width <= 0)
            return;

        // Holding Shift or Ctrl snaps to the current level's high edge (mirrors upstream's modifier behavior).
        if (e.KeyModifiers.HasFlag(KeyModifiers.Shift) || e.KeyModifiers.HasFlag(KeyModifiers.Control))
        {
            vm.SetExpToLevelEdgeHigh();
            return;
        }

        var x = e.GetPosition(bar).X;
        var fraction = x / width;
        vm.SetExpFromFraction(fraction);
    }
}
