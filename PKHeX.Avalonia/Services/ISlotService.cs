using PKHeX.Core;
using System;

namespace PKHeX.Avalonia.Services;

/// <summary>
/// Represents a slot location in the save file (box or party).
/// </summary>
public readonly struct SlotLocation
{
    public int Box { get; init; }
    public int Slot { get; init; }
    public bool IsParty { get; init; }
    
    public static SlotLocation FromBox(int box, int slot) => new() { Box = box, Slot = slot, IsParty = false };
    public static SlotLocation FromParty(int slot) => new() { Box = -1, Slot = slot, IsParty = true };
}

/// <summary>
/// Service interface for slot context menu operations.
/// </summary>
public interface ISlotService
{
    /// <summary>
    /// Gets the currently held PKM in the "clipboard" for Set operations.
    /// </summary>
    PKM? ClipboardPKM { get; }
    
    /// <summary>
    /// Event fired when a slot should be viewed (loaded to preview/editor).
    /// </summary>
    event Action<SlotLocation>? ViewRequested;
    
    /// <summary>
    /// Event fired when the clipboard PKM should be set to a slot.
    /// </summary>
    event Action<SlotLocation>? SetRequested;
    
    /// <summary>
    /// Event fired when a slot should be deleted (cleared).
    /// </summary>
    event Action<SlotLocation>? DeleteRequested;
    
    /// <summary>
    /// Sets the clipboard PKM for future Set operations.
    /// </summary>
    void SetClipboard(PKM pk);
    
    /// <summary>
    /// Clears the clipboard.
    /// </summary>
    void ClearClipboard();
    
    /// <summary>
    /// Triggers a view request for the given slot.
    /// </summary>
    void RequestView(SlotLocation location);
    
    /// <summary>
    /// Triggers a set request for the given slot.
    /// </summary>
    void RequestSet(SlotLocation location);
    
    /// <summary>
    /// Triggers a delete request for the given slot.
    /// </summary>
    void RequestDelete(SlotLocation location);
}

/// <summary>
/// Default implementation of ISlotService.
/// </summary>
public class SlotService : ISlotService
{
    public PKM? ClipboardPKM { get; private set; }
    
    public event Action<SlotLocation>? ViewRequested;
    public event Action<SlotLocation>? SetRequested;
    public event Action<SlotLocation>? DeleteRequested;
    
    public void SetClipboard(PKM pk)
    {
        ClipboardPKM = pk.Clone();
    }
    
    public void ClearClipboard()
    {
        ClipboardPKM = null;
    }
    
    public void RequestView(SlotLocation location)
    {
        ViewRequested?.Invoke(location);
    }
    
    public void RequestSet(SlotLocation location)
    {
        SetRequested?.Invoke(location);
    }
    
    public void RequestDelete(SlotLocation location)
    {
        DeleteRequested?.Invoke(location);
    }
}
