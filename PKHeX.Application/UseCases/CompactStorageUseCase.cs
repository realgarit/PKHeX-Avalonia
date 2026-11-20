using PKHeX.Core;

namespace PKHeX.Application.UseCases;

/// <summary>
/// Runs the pre-write storage cleanup that some save formats need before they are serialized.
/// </summary>
/// <remarks>
/// Let's Go Pikachu/Eevee (<see cref="SAV7b"/>) keeps its 1000 storage slots as a packed list: the
/// header stores a slot count, and the party and starter are indexes into that list. Placing a
/// Pokémon after the end of the list, or emptying a slot inside it, leaves a gap that the game does
/// not expect. <see cref="IStorageCleanup.FixStoragePreWrite"/> closes those gaps, updates the count
/// and repoints the party/starter indexes. WinForms PKHeX runs the same step before every export
/// (<c>SAVEditor.ExportSaveFile</c>); the Stadium saves implement the interface too.
/// </remarks>
public sealed class CompactStorageUseCase
{
    /// <summary>Compacts <paramref name="sav"/>'s storage in place when its format requires it.</summary>
    /// <returns><see langword="true"/> when slots moved, so any view of the storage must be reloaded.</returns>
    public bool Execute(SaveFile sav)
    {
        ArgumentNullException.ThrowIfNull(sav);
        return sav is IStorageCleanup cleanup && cleanup.FixStoragePreWrite();
    }
}
