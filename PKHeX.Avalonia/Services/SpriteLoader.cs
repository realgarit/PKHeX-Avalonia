using System.Collections.Concurrent;
using System.Reflection;
using System.Text;
using SkiaSharp;
using PKHeX.Core;

namespace PKHeX.Avalonia.Services;

/// <summary>
/// Loads Pokémon sprites from embedded resources using SkiaSharp.
/// Mirrors the naming conventions from PKHeX.Drawing.PokeSprite.
/// </summary>
public sealed class SpriteLoader
{
    private readonly Assembly _assembly;
    private readonly ConcurrentDictionary<string, SKBitmap?> _cache = new();
    private readonly HashSet<string> _availableResources;

    private const string SpritePrefix = "PKHeX.Avalonia.Assets.Images.Big_Pokemon_Sprites.";
    private const string ShinyPrefix = "PKHeX.Avalonia.Assets.Images.Big_Shiny_Sprites.";
    private const string ItemPrefix = "PKHeX.Avalonia.Assets.Images.Big_Items.";
    private const string OverlayPrefix = "PKHeX.Avalonia.Assets.Images.Pokemon_Sprite_Overlays.";

    // Species that show default sprite regardless of form
    private static readonly HashSet<ushort> SpeciesDefaultFormSprite =
    [
        (ushort)Species.Mothim,
        (ushort)Species.Scatterbug,
        (ushort)Species.Spewpa,
        (ushort)Species.Rockruff,
        (ushort)Species.Mimikyu,
        (ushort)Species.Sinistea,
        (ushort)Species.Polteageist,
        (ushort)Species.Urshifu,
        (ushort)Species.Dudunsparce,
        (ushort)Species.Poltchageist,
        (ushort)Species.Sinistcha,
    ];

    // Species with gender-specific sprites
    private static readonly HashSet<ushort> SpeciesGenderedSprite =
    [
        (ushort)Species.Hippopotas,
        (ushort)Species.Hippowdon,
        (ushort)Species.Unfezant,
        (ushort)Species.Frillish,
        (ushort)Species.Jellicent,
        (ushort)Species.Pyroar,
    ];

    public SpriteLoader()
    {
        _assembly = Assembly.GetExecutingAssembly();
        _availableResources = new HashSet<string>(_assembly.GetManifestResourceNames());
    }

    /// <summary>
    /// Gets the sprite for a Pokémon.
    /// </summary>
    public SKBitmap? GetSprite(ushort species, byte form, byte gender, uint formarg, bool shiny, EntityContext context)
    {
        if (species == 0)
            return null;

        // Build resource name
        var resourceName = GetResourceName(species, form, gender, formarg, shiny, context);

        // Try cache first
        if (_cache.TryGetValue(resourceName, out var cached))
            return cached;

        // Try to load
        var bitmap = LoadSprite(resourceName);

        // If shiny not found, try non-shiny
        if (bitmap is null && shiny)
        {
            var nonShinyName = GetResourceName(species, form, gender, formarg, false, context);
            bitmap = LoadSprite(nonShinyName);
        }

        // If form not found, try base form
        if (bitmap is null && form != 0)
        {
            var baseFormName = GetResourceName(species, 0, gender, 0, shiny, context);
            bitmap = LoadSprite(baseFormName);

            if (bitmap is null && shiny)
            {
                baseFormName = GetResourceName(species, 0, gender, 0, false, context);
                bitmap = LoadSprite(baseFormName);
            }
        }

        // Ultimate fallback: just species
        if (bitmap is null)
        {
            var speciesOnlyName = $"{SpritePrefix}b_{species}.png";
            bitmap = LoadSprite(speciesOnlyName);
        }

        _cache[resourceName] = bitmap;
        return bitmap;
    }

    /// <summary>
    /// Gets the shiny star overlay.
    /// </summary>
    public SKBitmap? GetShinyOverlay()
    {
        return LoadFromPrefix(OverlayPrefix, "rare_icon_alt.png");
    }

    /// <summary>
    /// Gets the egg sprite.
    /// </summary>
    public SKBitmap? GetEggSprite(ushort species)
    {
        // Manaphy has a special egg
        if (species == (ushort)Species.Manaphy)
            return LoadFromPrefix(SpritePrefix, "b_490_e.png");

        return LoadFromPrefix(SpritePrefix, "b_egg.png");
    }

    /// <summary>
    /// Gets an item sprite by item ID.
    /// </summary>
    public SKBitmap? GetItemSprite(int itemId)
    {
        if (itemId <= 0)
            return null;

        var resourceName = $"{ItemPrefix}bitem_{itemId}.png";
        return LoadSprite(resourceName);
    }

    private string GetResourceName(ushort species, byte form, byte gender, uint formarg, bool shiny, EntityContext context)
    {
        var prefix = shiny ? ShinyPrefix : SpritePrefix;
        var spriteName = GetSpriteName(species, form, gender, formarg, context);
        return $"{prefix}b{spriteName}.png";
    }

    private static string GetSpriteName(ushort species, byte form, byte gender, uint formarg, EntityContext context)
    {
        // Species that always show default form
        if (SpeciesDefaultFormSprite.Contains(species))
            form = 0;

        var sb = new StringBuilder(16);
        sb.Append('_').Append(species);

        if (form != 0)
        {
            sb.Append('-').Append(form);

            // Pikachu special forms
            if (species == (ushort)Species.Pikachu)
            {
                if (context == EntityContext.Gen6)
                    sb.Append('c'); // Cosplay
                else if (form == 8)
                    sb.Append('p'); // Let's Go starter
            }
            // Eevee Let's Go starter
            else if (species == (ushort)Species.Eevee && form == 1)
            {
                sb.Append('p');
            }
        }

        // Gender-specific sprites
        if (gender == 1 && SpeciesGenderedSprite.Contains(species))
            sb.Append('f');

        // Alcremie has both form and formarg
        if (species == (ushort)Species.Alcremie)
        {
            if (form == 0)
                sb.Append('-').Append(form);
            sb.Append('-').Append(formarg);
        }

        // Note: Shiny uses separate folder (ShinyPrefix), not a filename suffix
        return sb.ToString();
    }

    private SKBitmap? LoadFromPrefix(string prefix, string fileName)
    {
        var resourceName = $"{prefix}{fileName}";
        return LoadSprite(resourceName);
    }

    private SKBitmap? LoadSprite(string resourceName)
    {
        if (!_availableResources.Contains(resourceName))
            return null;

        try
        {
            using var stream = _assembly.GetManifestResourceStream(resourceName);
            if (stream is null)
                return null;

            return SKBitmap.Decode(stream);
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Clears the sprite cache.
    /// </summary>
    public void ClearCache()
    {
        foreach (var bitmap in _cache.Values)
            bitmap?.Dispose();
        _cache.Clear();
    }
}
