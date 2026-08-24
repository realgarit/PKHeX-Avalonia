using PKHeX.Core;
using System;

namespace PKHeX.Application.Abstractions;

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
    /// Gets the save-session token attached to in-app drag payloads. A new save or a closed save
    /// receives a new token so payloads from the previous session cannot mutate the new save.
    /// </summary>
    Guid SessionId { get; }

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
    /// Event fired when a PKM should be moved/swapped between slots.
    /// </summary>
    event Action<SlotLocation, SlotLocation, bool>? MoveRequested;

    /// <summary>Event fired when an imported entity should replace a slot.</summary>
    event Func<SlotLocation, PKM, Task>? ReplaceRequested;
    
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

    /// <summary>Triggers a view request only for the current save session.</summary>
    void RequestView(Guid sessionId, SlotLocation location);
    
    /// <summary>
    /// Triggers a set request for the given slot.
    /// </summary>
    void RequestSet(SlotLocation location);

    /// <summary>Triggers a set request only for the current save session.</summary>
    void RequestSet(Guid sessionId, SlotLocation location);
    
    /// <summary>
    /// Triggers a delete request for the given slot.
    /// </summary>
    void RequestDelete(SlotLocation location);

    /// <summary>Triggers a delete request only for the current save session.</summary>
    void RequestDelete(Guid sessionId, SlotLocation location);

    /// <summary>
    /// Triggers a move request between two slots.
    /// </summary>
    void RequestMove(SlotLocation source, SlotLocation destination, bool clone);

    /// <summary>Invalidates drag payloads from the current save session.</summary>
    void ResetSession();

    /// <summary>Returns whether a drag payload belongs to the currently active save session.</summary>
    bool IsCurrentSession(Guid sessionId);

    /// <summary>
    /// Triggers a move request only when the payload belongs to the current save session.
    /// </summary>
    void RequestMove(Guid sessionId, SlotLocation source, SlotLocation destination, bool clone);

    /// <summary>Requests an imported entity replacement only for the current save session.</summary>
    Task RequestReplaceAsync(Guid sessionId, SlotLocation destination, PKM replacement);
}
