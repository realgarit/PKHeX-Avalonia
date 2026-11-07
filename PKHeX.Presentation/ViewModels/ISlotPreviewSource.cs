namespace PKHeX.Presentation.ViewModels;

/// <summary>
/// A slot viewer that can build the rich hover card for one of its slots. Views ask for the card only
/// when it is about to open, so a box load does not run legality analysis and image work twice.
/// </summary>
public interface ISlotPreviewSource
{
    /// <summary>The live hover settings; read each time a card opens, so Settings changes apply at once.</summary>
    AppSettings.SlotHoverSettings HoverSettings { get; }

    /// <summary>Builds the card for a slot, or returns null for an empty slot (no card).</summary>
    SlotPreviewViewModel? GetSlotPreview(SlotLocation location);
}
