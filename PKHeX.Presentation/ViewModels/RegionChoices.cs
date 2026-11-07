using PKHeX.Core;
using PKHeX.Presentation.Localization;

namespace PKHeX.Presentation.ViewModels;

/// <summary>
/// Localized 3DS region and Vivillon pattern choices shared by the Gen 6/7 trainer editors and
/// the Pokémon editor. Every list keeps a stored value that is not a known choice as an explicit
/// "Unknown (n)" entry, so loading and saving never clamp or drop raw data.
/// </summary>
public static class RegionChoices
{
    /// <summary>Countries from Core's 3DS location table, in the current game-data language.</summary>
    public static IReadOnlyList<ComboItem> Countries(int selected) => WithUnknown(ReadGeo("countries"), selected);

    /// <summary>Sub-regions of <paramref name="country"/>; a country without sub-regions offers only "—" (0).</summary>
    public static IReadOnlyList<ComboItem> SubRegions(int country, int selected) => WithUnknown(ReadSubRegions(country), selected);

    /// <summary>True when <paramref name="subRegion"/> is a listed sub-region of <paramref name="country"/>.</summary>
    public static bool IsKnownSubRegion(int country, int subRegion) => ReadSubRegions(country).Any(item => item.Value == subRegion);

    /// <summary>3DS hardware regions from Core's game data source.</summary>
    public static IReadOnlyList<ComboItem> ConsoleRegions(int selected) => WithUnknown(GameInfo.Sources.Regions, selected);

    /// <summary>Vivillon patterns as named by Core's form list for <paramref name="context"/>.</summary>
    public static IReadOnlyList<ComboItem> VivillonPatterns(int selected, EntityContext context)
    {
        var strings = GameInfo.Strings;
        var names = FormConverter.GetFormList((ushort)Core.Species.Vivillon, strings.Types, strings.forms, GameInfo.GenderSymbolASCII, context);
        return WithUnknown(names.Select((name, index) => new ComboItem(name, index)), selected);
    }

    private static List<ComboItem> ReadSubRegions(int country)
    {
        var values = country is > 0 and <= 999 ? ReadGeo($"sr_{country:000}") : [];
        if (values.All(item => item.Value != 0))
            values.Insert(0, new ComboItem("—", 0));
        return values;
    }

    private static List<ComboItem> ReadGeo(string resource)
    {
        try { return Util.GetCountryRegionList(resource, GameInfo.CurrentLanguage); }
        catch (FormatException) { return []; }
        catch (ArgumentOutOfRangeException) { return []; }
    }

    private static IReadOnlyList<ComboItem> WithUnknown(IEnumerable<ComboItem> choices, int selected)
    {
        var result = choices.ToList();
        if (result.All(item => item.Value != selected))
            result.Add(new ComboItem(LocalizedStrings.Instance.Format("RaidSession_UnknownType", selected), selected));
        return result;
    }
}
