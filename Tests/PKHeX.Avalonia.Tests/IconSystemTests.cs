using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using PKHeX.Avalonia.Converters;
using PKHeX.Core;
using PKHeX.Presentation.Localization;
using PKHeX.Presentation.ViewModels;

namespace PKHeX.Avalonia.Tests;

/// <summary>
/// Guardrail for the shared vector icon set (issue #245): views draw icons from
/// <c>Styles/Icons.axaml</c> instead of emoji or raw arrow/shape glyphs, whose rendering depends on the
/// platform font, and the icon inventory stays on one 16 DIP grid.
/// </summary>
public class IconSystemTests
{
    /// <summary>
    /// The complete inventory. Adding an icon means adding it here, on the 16 x 16 grid, with
    /// provenance in docs/icons.md, so one-off icons cannot drift into views unnoticed.
    /// </summary>
    private static readonly string[] Inventory =
    [
        "Icon.ChevronLeft", "Icon.ChevronRight", "Icon.ChevronUp", "Icon.ChevronDown", "Icon.ArrowRight", "Icon.ArrowLeft",
        "Icon.Close", "Icon.Check", "Icon.Search", "Icon.Reset", "Icon.Filter", "Icon.Folder", "Icon.Tool",
        "Icon.Star", "Icon.Heart", "Icon.Circle", "Icon.Triangle", "Icon.Square", "Icon.Diamond",
        "Icon.Pouch.Items", "Icon.Pouch.KeyItems", "Icon.Pouch.TMHMs", "Icon.Pouch.Medicine", "Icon.Pouch.Berries",
        "Icon.Pouch.Balls", "Icon.Pouch.BattleItems", "Icon.Pouch.MailItems", "Icon.Pouch.PCItems", "Icon.Pouch.FreeSpace",
        "Icon.Pouch.Gem", "Icon.Pouch.Candy", "Icon.Pouch.Ingredients",
    ];

    private static readonly Regex IconKeyPattern = new(@"x:Key=""(Icon\.[A-Za-z.]+)""", RegexOptions.Compiled);
    private static readonly Regex IconReferencePattern = new(@"(?:StaticResource|DynamicResource)\s+(Icon\.[A-Za-z.]+)", RegexOptions.Compiled);
    private static readonly Regex SymbolOnlyContentPattern = new(@"\b(?:Content|Text)\s*=\s*""([^""{}]{1,3})""", RegexOptions.Compiled);

    /// <summary>
    /// Characters whose look depends on the platform's emoji or symbol font: arrows, geometric shapes,
    /// miscellaneous symbols, dingbats and the emoji planes.
    /// </summary>
    private static bool IsPlatformGlyph(int codePoint) =>
        codePoint is >= 0x2190 and <= 0x21FF   // arrows
            or >= 0x2300 and <= 0x23FF          // misc technical (⌘ ⏎ ...)
            or >= 0x25A0 and <= 0x25FF          // geometric shapes (▲ ▶ ◆ ...)
            or >= 0x2600 and <= 0x27BF          // misc symbols and dingbats (★ ♥ ✕ ✓ ...)
            or >= 0x2B00 and <= 0x2BFF          // misc symbols and arrows
            or >= 0x1F000;                      // emoji and pictographs

    [Fact]
    public void Views_DoNotUseEmojiOrRawGlyphsForIcons()
    {
        var root = FindRepoRoot();
        var violations = new List<string>();
        foreach (var file in Directory.GetFiles(Path.Combine(root, "PKHeX.Avalonia", "Views"), "*.axaml", SearchOption.AllDirectories)
                     .Concat(Directory.GetFiles(Path.Combine(root, "PKHeX.Avalonia", "Styles"), "*.axaml")))
        {
            var relative = Path.GetRelativePath(root, file).Replace('\\', '/');
            var lines = File.ReadAllLines(file);
            for (int i = 0; i < lines.Length; i++)
            {
                foreach (var rune in lines[i].EnumerateRunes())
                {
                    if (IsPlatformGlyph(rune.Value))
                        violations.Add($"{relative}:{i + 1}: U+{rune.Value:X4} '{rune}' -> use a PathIcon from Styles/Icons.axaml");
                }
                foreach (Match match in SymbolOnlyContentPattern.Matches(lines[i]))
                {
                    var value = match.Groups[1].Value.Trim();
                    if (value.Length > 0 && !value.Any(char.IsLetterOrDigit) && value.Any(c => char.GetUnicodeCategory(c) is UnicodeCategory.MathSymbol or UnicodeCategory.OtherSymbol))
                        violations.Add($"{relative}:{i + 1}: symbol-only label \"{value}\" -> use a PathIcon from Styles/Icons.axaml");
                }
            }
        }
        Assert.True(violations.Count == 0, "Platform-dependent glyphs in views:\n" + string.Join('\n', violations));
    }

    [Fact]
    public void ToolbarLabels_DoNotCarryArrowGlyphs()
    {
        // These labels sit beside a vector chevron now; a glyph in the text would draw it twice.
        foreach (var language in new[] { "en", "de", "es", "fr", "it", "ja", "ko", "pt-BR", "zh-Hans", "zh-Hant" })
        {
            var json = File.ReadAllText(Path.Combine(FindRepoRoot(), "PKHeX.Presentation", "Localization", "Strings", language + ".json"));
            var strings = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, string>>(json)!;
            foreach (var key in new[] { "EntitySeekView_Prev", "EntitySeekView_Next", "PKMDatabaseView_ToggleFilters", "MysteryGiftDatabaseView_ToggleFilters" })
                Assert.DoesNotContain(strings[key].EnumerateRunes(), rune => IsPlatformGlyph(rune.Value));
        }
    }

    [Fact]
    public void IconInventory_IsExactlyTheDocumentedSet()
    {
        var root = FindRepoRoot();
        var source = File.ReadAllText(Path.Combine(root, "PKHeX.Avalonia", "Styles", "Icons.axaml"));
        var defined = IconKeyPattern.Matches(source).Select(m => m.Groups[1].Value).ToArray();
        Assert.Equal(Inventory.Order(StringComparer.Ordinal), defined.Order(StringComparer.Ordinal));

        var provenance = File.ReadAllText(Path.Combine(root, "docs", "icons.md"));
        foreach (var key in Inventory)
            Assert.Contains($"`{key}`", provenance);

        // Every icon a view or style references exists in the shared set.
        foreach (var file in Directory.GetFiles(Path.Combine(root, "PKHeX.Avalonia"), "*.axaml", SearchOption.AllDirectories)
                     .Concat(Directory.GetFiles(Path.Combine(root, "PKHeX.Avalonia"), "*.cs", SearchOption.AllDirectories))
                     .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}") && !path.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}")))
        {
            foreach (Match match in IconReferencePattern.Matches(File.ReadAllText(file)))
                Assert.Contains(match.Groups[1].Value, Inventory);
            foreach (Match match in Regex.Matches(File.ReadAllText(file), @"""(Icon\.[A-Za-z.]+)"""))
                Assert.Contains(match.Groups[1].Value, Inventory);
        }
    }

    [AvaloniaFact]
    public void IconGeometries_StayOnTheSixteenDipGrid()
    {
        var app = global::Avalonia.Application.Current!;
        foreach (var key in Inventory)
        {
            Assert.True(app.TryFindResource(key, out var resource), key);
            var geometry = Assert.IsAssignableFrom<Geometry>(resource);
            var bounds = geometry.Bounds;
            // Every icon keeps the safe margin of the 16 x 16 box. (The headless drawing stub measures
            // arcs by their end points only; IconRenderTests checks the rendered ink with real Skia.)
            Assert.True(bounds.X >= 0.5 && bounds.Y >= 0.5 && bounds.Right <= 15.5 && bounds.Bottom <= 15.75, $"{key} {bounds}");
        }
    }

    [Fact]
    public void IconOnlyActions_HaveTooltipAndLocalizedAutomationName()
    {
        var root = FindRepoRoot();
        var violations = new List<string>();
        var button = new Regex(@"<Button\b(?<attrs>[^>]*?)>\s*<PathIcon\b[^>]*/>\s*</Button>", RegexOptions.Singleline);
        foreach (var file in Directory.GetFiles(Path.Combine(root, "PKHeX.Avalonia", "Views"), "*.axaml", SearchOption.AllDirectories))
        {
            foreach (Match match in button.Matches(File.ReadAllText(file)))
            {
                var attrs = match.Groups["attrs"].Value;
                if (!Regex.IsMatch(attrs, @"AutomationProperties\.Name\s*=\s*""\{(loc:Loc|Binding)"))
                    violations.Add($"{Path.GetFileName(file)}: icon-only button without a localized automation name: {attrs.Trim()}");
                if (!attrs.Contains("ToolTip.Tip"))
                    violations.Add($"{Path.GetFileName(file)}: icon-only button without a tooltip: {attrs.Trim()}");
            }
        }
        Assert.True(violations.Count == 0, string.Join('\n', violations));
    }

    [AvaloniaFact]
    public void EveryPouchType_HasALocalizedNameAndAnIcon()
    {
        var converter = new InventoryPouchIconConverter();
        foreach (var type in Enum.GetValues<InventoryType>())
        {
            var name = InventoryPouchViewModel.GetPouchName(type);
            Assert.False(string.IsNullOrWhiteSpace(name));
            Assert.NotEqual($"InventoryPouch_{type}", name);
            Assert.IsAssignableFrom<Geometry>(converter.Convert(type, typeof(Geometry), null, CultureInfo.InvariantCulture));
        }
        Assert.Equal("Key Items", WithLanguage("en", () => InventoryPouchViewModel.GetPouchName(InventoryType.KeyItems)));
        Assert.Equal("Mega Stones", WithLanguage("en", () => InventoryPouchViewModel.GetPouchName(InventoryType.MegaStones)));
        Assert.Equal("TMs & HMs", WithLanguage("en", () => InventoryPouchViewModel.GetPouchName(InventoryType.TMHMs)));
    }

    private static T WithLanguage<T>(string language, Func<T> read)
    {
        var previous = LocalizedStrings.Instance.CurrentLanguage;
        try { LocalizedStrings.Instance.SetLanguage(language); return read(); }
        finally { LocalizedStrings.Instance.SetLanguage(previous); }
    }

    private static string FindRepoRoot()
    {
        var dir = AppDomain.CurrentDomain.BaseDirectory;
        while (!Directory.Exists(Path.Combine(dir, "PKHeX.Avalonia")))
            dir = Directory.GetParent(dir)?.FullName ?? throw new DirectoryNotFoundException("Could not find repository root");
        return dir;
    }
}
