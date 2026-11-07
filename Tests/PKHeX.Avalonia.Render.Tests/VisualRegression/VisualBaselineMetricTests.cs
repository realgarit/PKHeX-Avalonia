using SkiaSharp;

namespace PKHeX.Avalonia.Render.Tests.VisualRegression;

/// <summary>
/// Guards the comparator itself: antialiasing-sized noise must pass, material layout changes must fail.
/// </summary>
public class VisualBaselineMetricTests
{
    private const int Width = 900, Height = 600;

    [Fact]
    public void IdenticalFramesHaveNoDifferences() =>
        Assert.Equal(0, Differences(Frame(), Frame()));

    [Fact]
    public void OnePixelGlyphEdgeShiftsAndSmallColourNoiseAreTolerated()
    {
        using var expected = Frame();
        using var actual = Frame(textOffset: 1, noise: VisualBaseline.ChannelTolerance);
        Assert.True(Fraction(expected, actual) <= VisualBaseline.MaxDifferentFraction);
    }

    [Fact]
    public void MovedTextRowExceedsTheThreshold()
    {
        using var expected = Frame();
        using var actual = Frame(textOffset: 4);
        Assert.True(Fraction(expected, actual) > VisualBaseline.MaxDifferentFraction);
    }

    [Fact]
    public void ClippedButtonExceedsTheThreshold()
    {
        using var expected = Frame();
        using var actual = Frame(buttonWidth: 60);
        Assert.True(Fraction(expected, actual) > VisualBaseline.MaxDifferentFraction);
    }

    [Fact]
    public void RecolouredSurfaceExceedsTheThreshold()
    {
        using var expected = Frame();
        using var actual = Frame(button: new SKColor(0x2E, 0x7D, 0x32));
        Assert.True(Fraction(expected, actual) > VisualBaseline.MaxDifferentFraction);
    }

    private static int Differences(SKBitmap a, SKBitmap b)
    {
        using (a) using (b) return VisualBaseline.CountDifferences(a, b, null);
    }

    private static double Fraction(SKBitmap a, SKBitmap b) => (double)VisualBaseline.CountDifferences(a, b, null) / (Width * Height);

    /// <summary>A synthetic "dialog": a surface, a row of thin text-like strokes and a coloured button.</summary>
    private static SKBitmap Frame(int textOffset = 0, int noise = 0, int buttonWidth = 120, SKColor? button = null)
    {
        var bitmap = new SKBitmap(Width, Height, SKColorType.Rgba8888, SKAlphaType.Premul);
        using var canvas = new SKCanvas(bitmap);
        canvas.Clear(new SKColor(0x1E, 0x1E, 0x24));
        using var paint = new SKPaint { IsAntialias = true, Color = new SKColor(0xE6, 0xE6, 0xE6), StrokeWidth = 1.5f };
        for (var row = 0; row < 12; row++)
        for (var glyph = 0; glyph < 40; glyph++)
        {
            float x = 40 + glyph * 12 + textOffset, y = 40 + row * 30;
            canvas.DrawLine(x, y, x, y + 12, paint);
            canvas.DrawLine(x, y + 6, x + 6, y + 6, paint);
        }
        paint.Color = button ?? new SKColor(0xB0, 0x4A, 0x55);
        canvas.DrawRect(700, 520, buttonWidth, 40, paint);
        if (noise > 0)
        {
            var pixels = bitmap.Pixels;
            for (var i = 0; i < pixels.Length; i += 7)
            {
                var p = pixels[i];
                pixels[i] = new SKColor((byte)Math.Min(255, p.Red + noise), p.Green, (byte)Math.Max(0, p.Blue - noise), p.Alpha);
            }
            bitmap.Pixels = pixels;
        }
        return bitmap;
    }
}
