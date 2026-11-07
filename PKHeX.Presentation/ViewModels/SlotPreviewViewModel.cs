using PKHeX.Application.Models;
using PKHeX.Core;
using PKHeX.Presentation.Localization;

namespace PKHeX.Presentation.ViewModels;

/// <summary>Legality state shown by the slot preview card.</summary>
public enum SlotPreviewLegality
{
    Legal,
    Illegal,
    /// <summary>HaX mode: legality checks are off, so the card makes no legality claim.</summary>
    NotChecked,
}

/// <summary>One move row of the slot preview card.</summary>
public sealed record SlotPreviewMove(string Name, string TypeName, byte[]? TypeIcon, bool IsIllegal)
{
    /// <summary>Text for screen readers and the plain tooltip, e.g. "Thunderbolt (Electric)".</summary>
    public string AccessibleText => IsIllegal
        ? LocalizedStrings.Instance.Format("SlotPreview_IllegalMove", $"{Name} ({TypeName})")
        : $"{Name} ({TypeName})";
}

/// <summary>
/// Framework-free content of the rich slot hover card (issue #244). Built once per hover from a PKM and
/// its <see cref="LegalityAnalysis"/>; images cross to the view as PNG bytes.
/// </summary>
public sealed class SlotPreviewViewModel
{
    public required string Title { get; init; }
    /// <summary>Species name, shown under the title when the Pokémon has a nickname or is an egg.</summary>
    public required string SpeciesName { get; init; }
    public bool ShowSpeciesName => !string.Equals(Title, SpeciesName, StringComparison.Ordinal);
    public required string GenderSymbol { get; init; }
    public required string GenderText { get; init; }
    public bool HasGender => GenderSymbol.Length != 0;
    public bool IsFemale { get; init; }
    public bool IsShiny { get; init; }
    public required string LevelText { get; init; }
    public byte[]? Sprite { get; init; }
    public byte[]? BallSprite { get; init; }
    public required string BallName { get; init; }
    public required string HeldItemText { get; init; }
    public bool HasHeldItem => HeldItemText.Length != 0;
    public required IReadOnlyList<SlotPreviewMove> Moves { get; init; }
    public bool HasMoves => Moves.Count != 0;
    public SlotPreviewLegality Legality { get; init; }
    public bool IsLegal => Legality == SlotPreviewLegality.Legal;
    public bool IsIllegal => Legality == SlotPreviewLegality.Illegal;
    public bool IsNotChecked => Legality == SlotPreviewLegality.NotChecked;
    public required string LegalityText { get; init; }
    /// <summary>First invalid checks of the legality report, or an unavailable-move hint.</summary>
    public required string LegalityHint { get; init; }
    public bool HasLegalityHint => LegalityHint.Length != 0;
    public required string EncounterText { get; init; }
    public required string EncounterLocation { get; init; }
    public bool HasEncounter => EncounterText.Length != 0;
    public bool HasEncounterLocation => EncounterLocation.Length != 0;

    /// <summary>The whole card as plain text, one fact per line, for screen readers and fallbacks.</summary>
    public string AccessibleText
    {
        get
        {
            var s = LocalizedStrings.Instance;
            var lines = new List<string>();
            var header = ShowSpeciesName ? $"{Title} ({SpeciesName})" : Title;
            if (GenderText.Length != 0) header += $", {GenderText}";
            lines.Add($"{header}, {LevelText}");
            if (BallName.Length != 0) lines.Add(s.Format("SlotPreview_Ball", BallName));
            if (HasHeldItem) lines.Add(s.Format("SlotPreview_HeldItem", HeldItemText));
            if (HasMoves) lines.Add($"{s["SlotPreview_Moves"]}: {string.Join(", ", Moves.Select(m => m.AccessibleText))}");
            lines.Add(LegalityText);
            if (HasLegalityHint) lines.Add(LegalityHint);
            if (HasEncounter) lines.Add(s.Format("SlotPreview_Encounter", EncounterText));
            if (HasEncounterLocation) lines.Add(s.Format("SlotPreview_Location", EncounterLocation));
            return string.Join(Environment.NewLine, lines);
        }
    }

    private const int MaxHintLines = 3;

    /// <summary>
    /// Builds the card for <paramref name="pk"/>, or returns null for an empty slot (no card is shown).
    /// </summary>
    public static SlotPreviewViewModel? Create(PKM pk, SaveFile save, StorageSlotType origin, ISpriteRenderer renderer, bool haXMode, bool includeEncounter)
    {
        if (pk.Species == 0)
            return null;

        var s = LocalizedStrings.Instance;
        var strings = GameInfo.Strings;
        var speciesName = StringResourceLookup.Species(pk.Species);
        var title = pk.IsEgg ? s["SlotState_Egg"] : pk.Nickname;
        if (string.IsNullOrWhiteSpace(title)) title = speciesName;

        LegalityAnalysis? la = null;
        if (!haXMode)
        {
            try { la = new LegalityAnalysis(pk, save.Personal, origin); }
            catch (Exception) { la = null; }
        }

        var moves = new List<SlotPreviewMove>(4);
        Span<ushort> ids = stackalloc ushort[4];
        pk.GetMoves(ids);
        for (var i = 0; i < ids.Length; i++)
        {
            var move = ids[i];
            if (move == 0) continue;
            var type = SafeMoveType(move, pk.Context);
            var illegal = la is { Parsed: true } && la.Info.Moves[i] is { IsParsed: true, Valid: false };
            moves.Add(new SlotPreviewMove(
                StringResourceLookup.Move(move),
                type < 0 ? string.Empty : StringResourceLookup.Lookup(strings.types, type),
                type < 0 ? null : renderer.GetTypeIcon(type),
                illegal));
        }

        var legality = la is null ? SlotPreviewLegality.NotChecked
            : la.Valid ? SlotPreviewLegality.Legal : SlotPreviewLegality.Illegal;
        var hint = legality switch
        {
            SlotPreviewLegality.Illegal => FirstLines(SafeReport(la!), MaxHintLines),
            SlotPreviewLegality.Legal when pk.Format >= 8 && MoveInfo.IsDummiedMoveAny(pk) => s["SlotState_MoveHint"],
            _ => string.Empty,
        };

        var (encounter, location) = includeEncounter && la is not null ? DescribeEncounter(la) : (string.Empty, string.Empty);
        var ball = pk.Ball;
        var gender = pk.Gender;

        return new SlotPreviewViewModel
        {
            Title = title,
            SpeciesName = speciesName,
            GenderSymbol = pk.IsEgg ? string.Empty : gender switch { 0 => "♂", 1 => "♀", _ => string.Empty },
            GenderText = pk.IsEgg ? string.Empty : gender switch { 0 => s["SlotPreview_Male"], 1 => s["SlotPreview_Female"], _ => string.Empty },
            IsFemale = gender == 1,
            IsShiny = !pk.IsEgg && pk.IsShiny,
            LevelText = s.Format("SlotPreview_Level", pk.CurrentLevel),
            Sprite = renderer.GetSprite(pk),
            BallSprite = ball == 0 ? null : renderer.GetBallSprite(ball),
            BallName = ball == 0 ? string.Empty : StringResourceLookup.Lookup(strings.balllist, ball),
            HeldItemText = pk.HeldItem > 0 ? StringResourceLookup.Item(pk.HeldItem) : string.Empty,
            Moves = moves,
            Legality = legality,
            LegalityText = legality switch
            {
                SlotPreviewLegality.Legal => s["SlotPreview_Legal"],
                SlotPreviewLegality.Illegal => s["SlotPreview_Illegal"],
                _ => s["SlotPreview_NotChecked"],
            },
            LegalityHint = hint,
            EncounterText = encounter,
            EncounterLocation = location,
        };
    }

    private static int SafeMoveType(ushort move, EntityContext context)
    {
        try { return MoveInfo.GetType(move, context); }
        catch (Exception) { return -1; }
    }

    private static string SafeReport(LegalityAnalysis la)
    {
        try { return la.Report(); }
        catch (Exception) { return string.Empty; }
    }

    private static (string Encounter, string Location) DescribeEncounter(LegalityAnalysis la)
    {
        try
        {
            var enc = la.EncounterOriginal;
            if (enc is EncounterInvalid)
                return (string.Empty, string.Empty);
            return (enc.GetEncounterName(GameInfo.Strings.specieslist), enc.GetEncounterLocation() ?? string.Empty);
        }
        catch (Exception)
        {
            return (string.Empty, string.Empty);
        }
    }

    private static string FirstLines(string text, int count)
    {
        var lines = text.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return string.Join(Environment.NewLine, lines.Take(count));
    }
}
