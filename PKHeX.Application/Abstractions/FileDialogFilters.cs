using System.Linq;
using PKHeX.Core;

namespace PKHeX.Application.Abstractions;

/// <summary>
/// Framework-free file-dialog glob patterns. The host turns these into platform picker filters;
/// the Application/Presentation layers only deal in the patterns themselves.
/// </summary>
public static class FileDialogFilters
{
    /// <summary>
    /// Patterns for the "Open Save File" dialog. Covers classic saves (*.sav), raw dumps (*.bin),
    /// Switch saves (*.main, or the literal extensionless file name "main"), and anything else via
    /// wildcard — save files come in many names, so the dialog must never block a selection.
    /// </summary>
    public static readonly string[] OpenSaveFile = ["*.sav", "*.bin", "*.main", "main", "*"];

    /// <summary>
    /// Patterns for the "Open Pokémon File" dialog: every Core entity extension (pk1–pk9 and the
    /// side-game formats), plus a wildcard because mystery gift and encounter files also import.
    /// </summary>
    public static readonly string[] OpenEntityFile =
        [.. EntityFileExtension.GetExtensions().Select(static e => $"*.{e}"), "*"];

    /// <summary>Pattern for the "Save Pokémon As" dialog for an entity with <paramref name="extension"/>.</summary>
    public static string[] EntityFile(string extension) => [$"*.{extension}"];
}
