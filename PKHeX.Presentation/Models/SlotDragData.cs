
using System;

namespace PKHeX.Presentation.Models;

/// <summary>
/// Data carried during a Pokémon slot drag and drop operation.
/// </summary>
public record SlotDragData(SlotLocation Source, Guid SessionId)
{
    /// <summary>Creates a legacy/sessionless payload for non-UI callers.</summary>
    public SlotDragData(SlotLocation source) : this(source, Guid.Empty) { }
}
