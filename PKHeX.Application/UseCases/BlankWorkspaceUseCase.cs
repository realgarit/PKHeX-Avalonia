using System.Linq;
using PKHeX.Core;

namespace PKHeX.Application.UseCases;

/// <summary>
/// Creates the in-memory blank save that backs a standalone Pokémon workspace: the user edits
/// individual Pokémon files against a selected target game without loading or writing a real save.
/// </summary>
/// <remarks>
/// The blank save only supplies the target format, the legality context and the trainer context
/// (OT, IDs, language) for new entities. It has no file path, so nothing writes it to disk unless
/// the caller deliberately exports it.
/// </remarks>
public sealed class BlankWorkspaceUseCase
{
    /// <summary>
    /// Every stored game version that <see cref="BlankSaveFile.Get(GameVersion, string, LanguageID)"/>
    /// can build a blank save for, newest games first (the order of <see cref="GameUtil.GameVersions"/>).
    /// </summary>
    public static IReadOnlyList<GameVersion> SupportedTargets { get; } =
        GameUtil.GameVersions.Where(IsSupportedTarget).ToArray();

    /// <summary>Whether a blank workspace can be created for <paramref name="version"/>.</summary>
    /// <remarks>
    /// <see cref="GameVersion.FRX"/> and <see cref="GameVersion.LGX"/> only mark Virtual Console
    /// transfers; they share the FireRed/LeafGreen save, so the plain games represent them.
    /// </remarks>
    public static bool IsSupportedTarget(GameVersion version) =>
        version is not (GameVersion.FRX or GameVersion.LGX) && version.SaveFileType is not SaveFileType.None;

    /// <summary>
    /// Suggests the target game that matches the format of <paramref name="pk"/>: its own origin game
    /// when that game stores the same format, otherwise the representative game of its context.
    /// </summary>
    public static GameVersion SuggestTarget(PKM pk)
    {
        ArgumentNullException.ThrowIfNull(pk);
        var version = pk.Version;
        if (IsSupportedTarget(version) && version.SaveFileType.Context == pk.Context)
            return version;

        try
        {
            var fallback = pk.Context.GetSingleGameVersion();
            if (IsSupportedTarget(fallback))
                return fallback;
        }
        catch (ArgumentOutOfRangeException)
        {
            // Contexts without a stored game (none at present) fall through to the newest target.
        }

        return SupportedTargets[0];
    }

    /// <summary>
    /// Creates the blank save for <paramref name="version"/>. When <paramref name="trainerSource"/> is
    /// given, the blank save takes its language and a matching default trainer name, the same way
    /// Core picks defaults for a blank save.
    /// </summary>
    public SaveFile Execute(GameVersion version, SaveFile? trainerSource = null)
    {
        if (!IsSupportedTarget(version))
            throw new ArgumentOutOfRangeException(nameof(version), version, "No blank save exists for this game.");

        var sav = BlankSaveFile.Get(version, trainerSource);
        // A freshly created context is clean; it must never count as unsaved user data.
        sav.State.Edited = false;
        return sav;
    }
}
