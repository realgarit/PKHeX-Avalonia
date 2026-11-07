using PKHeX.Core;

namespace PKHeX.Application.Abstractions;

/// <summary>
/// Renders PKM sprites to framework-free PNG bytes. The View layer materializes an Avalonia
/// <c>Bitmap</c> from these bytes via a value converter, keeping the UI framework out of the
/// Application/Presentation layers.
/// </summary>
public interface ISpriteRenderer
{
    byte[]? GetSprite(PKM pk, bool isEgg = false);
    byte[]? GetSlotSprite(PKM pk, PKHeX.Application.Models.SpriteSlotState state) => GetSprite(pk);
    byte[]? GetSprite(ushort species, byte form, byte gender, uint formarg, bool shiny, EntityContext context);
    /// <summary>Renders an item whose ID is already in the modern (Gen 4+) numbering scheme.</summary>
    byte[]? GetItemSprite(int itemId);
    /// <summary>Renders a raw bag item ID using its owning save's context and game.</summary>
    byte[]? GetItemSprite(int itemId, EntityContext context, GameVersion version);
    byte[]? GetEmptySlot();
    /// <summary>Renders the Poké Ball icon for a <see cref="Ball"/> value, or null when none is bundled.</summary>
    byte[]? GetBallSprite(int ball) => null;
    /// <summary>Renders the square type icon for a <see cref="MoveType"/> index, or null when none is bundled.</summary>
    byte[]? GetTypeIcon(int type) => null;
    void Initialize(SaveFile sav);
}
