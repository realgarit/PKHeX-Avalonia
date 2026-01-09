using Avalonia.Media.Imaging;
using PKHeX.Core;
using SkiaSharp;

namespace PKHeX.Avalonia.Services;

/// <summary>
/// Renders PKM sprites using SkiaSharp (no System.Drawing).
/// Uses placeholder sprites for the vertical slice - real sprites can be added later.
/// </summary>
public sealed class AvaloniaSpriteRenderer : ISpriteRenderer
{
    private const int SpriteWidth = 68;
    private const int SpriteHeight = 56;

    public void Initialize(SaveFile sav)
    {
        // Could determine sprite style based on generation in the future
    }

    public Bitmap? GetSprite(PKM pk, bool isEgg = false)
    {
        if (pk.Species == 0)
            return GetEmptySlot();

        return CreatePlaceholderSprite(pk);
    }

    public Bitmap? GetEmptySlot()
    {
        return CreateEmptySlotBitmap();
    }

    private static Bitmap CreatePlaceholderSprite(PKM pk)
    {
        using var surface = SKSurface.Create(new SKImageInfo(SpriteWidth, SpriteHeight));
        var canvas = surface.Canvas;

        // Background color based on primary type
        var typeColor = GetTypeColor(pk.PersonalInfo.Type1);
        if (pk.IsShiny)
        {
            // Add golden tint for shiny
            typeColor = BlendColors(typeColor, new SKColor(255, 215, 0), 0.3f);
        }

        // Draw rounded rectangle background
        using var bgPaint = new SKPaint
        {
            Color = typeColor,
            IsAntialias = true,
            Style = SKPaintStyle.Fill
        };
        canvas.DrawRoundRect(new SKRoundRect(new SKRect(2, 2, SpriteWidth - 2, SpriteHeight - 2), 6), bgPaint);

        // Draw border
        using var borderPaint = new SKPaint
        {
            Color = typeColor.WithAlpha(200),
            IsAntialias = true,
            Style = SKPaintStyle.Stroke,
            StrokeWidth = 2
        };
        canvas.DrawRoundRect(new SKRoundRect(new SKRect(2, 2, SpriteWidth - 2, SpriteHeight - 2), 6), borderPaint);

        // Draw species number using modern SKFont API
        using var typeface = SKTypeface.FromFamilyName("Arial", SKFontStyle.Bold);
        using var font = new SKFont(typeface, 12);

        using var textPaint = new SKPaint
        {
            Color = SKColors.White,
            IsAntialias = true
        };

        using var shadowPaint = new SKPaint
        {
            Color = SKColors.Black.WithAlpha(100),
            IsAntialias = true
        };

        var text = $"#{pk.Species}";
        canvas.DrawText(text, SpriteWidth / 2 + 1, SpriteHeight / 2 + 5, SKTextAlign.Center, font, shadowPaint);
        canvas.DrawText(text, SpriteWidth / 2, SpriteHeight / 2 + 4, SKTextAlign.Center, font, textPaint);

        // Draw egg indicator if applicable
        if (pk.IsEgg)
        {
            using var eggPaint = new SKPaint
            {
                Color = SKColors.White,
                IsAntialias = true,
                Style = SKPaintStyle.Fill
            };
            canvas.DrawOval(new SKRect(SpriteWidth - 16, SpriteHeight - 16, SpriteWidth - 4, SpriteHeight - 4), eggPaint);

            using var eggBorderPaint = new SKPaint
            {
                Color = SKColors.Gray,
                IsAntialias = true,
                Style = SKPaintStyle.Stroke,
                StrokeWidth = 1
            };
            canvas.DrawOval(new SKRect(SpriteWidth - 16, SpriteHeight - 16, SpriteWidth - 4, SpriteHeight - 4), eggBorderPaint);
        }

        return ConvertToBitmap(surface);
    }

    private static Bitmap CreateEmptySlotBitmap()
    {
        using var surface = SKSurface.Create(new SKImageInfo(SpriteWidth, SpriteHeight));
        var canvas = surface.Canvas;
        canvas.Clear(SKColors.Transparent);
        return ConvertToBitmap(surface);
    }

    private static Bitmap ConvertToBitmap(SKSurface surface)
    {
        using var image = surface.Snapshot();
        using var data = image.Encode(SKEncodedImageFormat.Png, 100);
        using var stream = new MemoryStream(data.ToArray());
        return new Bitmap(stream);
    }

    private static SKColor GetTypeColor(int type)
    {
        // Pokemon type colors
        return type switch
        {
            0 => new SKColor(168, 168, 120),   // Normal
            1 => new SKColor(192, 48, 40),     // Fighting
            2 => new SKColor(168, 144, 240),   // Flying
            3 => new SKColor(160, 64, 160),    // Poison
            4 => new SKColor(224, 192, 104),   // Ground
            5 => new SKColor(184, 160, 56),    // Rock
            6 => new SKColor(168, 184, 32),    // Bug
            7 => new SKColor(112, 88, 152),    // Ghost
            8 => new SKColor(184, 184, 208),   // Steel
            9 => new SKColor(240, 128, 48),    // Fire
            10 => new SKColor(104, 144, 240),  // Water
            11 => new SKColor(120, 200, 80),   // Grass
            12 => new SKColor(248, 208, 48),   // Electric
            13 => new SKColor(248, 88, 136),   // Psychic
            14 => new SKColor(152, 216, 216),  // Ice
            15 => new SKColor(112, 56, 248),   // Dragon
            16 => new SKColor(112, 88, 72),    // Dark
            17 => new SKColor(238, 153, 172),  // Fairy
            _ => new SKColor(104, 160, 144)    // Unknown/default
        };
    }

    private static SKColor BlendColors(SKColor color1, SKColor color2, float ratio)
    {
        var r = (byte)(color1.Red * (1 - ratio) + color2.Red * ratio);
        var g = (byte)(color1.Green * (1 - ratio) + color2.Green * ratio);
        var b = (byte)(color1.Blue * (1 - ratio) + color2.Blue * ratio);
        return new SKColor(r, g, b);
    }
}
