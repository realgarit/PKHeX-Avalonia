using System.Globalization;
using System.Text;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media.Imaging;
using SkiaSharp;

namespace PKHeX.Avalonia.Render.Tests.VisualRegression;

/// <summary>
/// Compares real-Skia headless frames against the reviewed PNG baselines committed under
/// <c>Tests/PKHeX.Avalonia.Render.Tests/VisualRegression/Baselines</c>. See docs/visual-regression.md.
/// </summary>
/// <remarks>
/// <para><b>Metric.</b> A pixel only counts as different when its largest RGBA channel delta is above
/// <see cref="ChannelTolerance"/> AND no pixel in the 3x3 neighbourhood of the other image is within
/// that tolerance. This is checked in both directions, so one-pixel antialiasing and glyph-edge shifts
/// are absorbed, while moved, resized, recoloured, clipped or missing content is not. A frame fails when
/// more than <see cref="MaxDifferentFraction"/> of its pixels differ, or when its size changes.</para>
/// <para><b>Platforms.</b> Baselines are generated on Windows. Inter is bundled, so layout and glyph
/// advances match on every OS, but Skia rasterizes glyph coverage through the platform scaler
/// (DirectWrite, CoreText, FreeType), so edge pixels differ slightly between operating systems. The
/// diff is therefore enforced on the operating systems in <see cref="IsEnforced"/>; on the others the
/// same frames are still rendered, compared and published as artifacts, and the measured difference is
/// written to the summary, but a mismatch does not fail the run.</para>
/// <para><b>Regenerate.</b> Set <c>PKHEX_VISUAL_UPDATE=1</c> and run the render test project; every
/// frame is rewritten to the source baseline folder instead of being compared.</para>
/// </remarks>
internal static class VisualBaseline
{
    /// <summary>Largest per-channel delta (0–255) that still counts as the same colour.</summary>
    public const int ChannelTolerance = 24;

    /// <summary>Largest share of the frame that may differ after neighbourhood matching.</summary>
    public const double MaxDifferentFraction = 0.002;

    public static bool UpdateMode => Environment.GetEnvironmentVariable("PKHEX_VISUAL_UPDATE") == "1";

    /// <summary>
    /// True when a mismatch fails the test on this OS. <c>PKHEX_VISUAL_ENFORCE=1</c> forces
    /// enforcement everywhere, <c>PKHEX_VISUAL_ENFORCE=0</c> turns it off.
    /// </summary>
    public static bool IsEnforced => Environment.GetEnvironmentVariable("PKHEX_VISUAL_ENFORCE") switch
    {
        "1" => true,
        "0" => false,
        _ => OperatingSystem.IsWindows(),
    };

    public static string BaselineDirectory
    {
        get
        {
            var directory = new DirectoryInfo(AppContext.BaseDirectory);
            while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "PKHeX.sln")))
                directory = directory.Parent;
            Assert.NotNull(directory);
            return Path.Combine(directory.FullName, "Tests", "PKHeX.Avalonia.Render.Tests", "VisualRegression", "Baselines");
        }
    }

    public static string ArtifactDirectory =>
        Environment.GetEnvironmentVariable("PKHEX_VISUAL_ARTIFACT_DIR")
        ?? Path.Combine(AppContext.BaseDirectory, "TestResults", "visual-regression");

    /// <summary>Renders <paramref name="window"/>'s client area to a decoded bitmap.</summary>
    public static SKBitmap Render(Window window)
    {
        var size = new PixelSize((int)window.ClientSize.Width, (int)window.ClientSize.Height);
        using var frame = new RenderTargetBitmap(size);
        frame.Render(window);
        using var stream = new MemoryStream();
        frame.Save(stream, PngBitmapEncoderOptions.Default);
        var decoded = SKBitmap.Decode(stream.ToArray());
        Assert.NotNull(decoded);
        var normalized = decoded.Copy(SKColorType.Rgba8888);
        decoded.Dispose();
        return normalized;
    }

    /// <summary>
    /// Compares <paramref name="actual"/> with the baseline <paramref name="name"/>.png. Returns a failure
    /// description, or <see langword="null"/> when the frame matches (or was written in update mode).
    /// </summary>
    public static string? Check(string name, SKBitmap actual)
    {
        // A blank frame means the renderer produced nothing; never accept or store it.
        Assert.Contains(actual.Pixels, p => p != actual.GetPixel(0, 0));
        Directory.CreateDirectory(ArtifactDirectory);
        WritePng(actual, Path.Combine(ArtifactDirectory, name + ".actual.png"));

        var baselinePath = Path.Combine(BaselineDirectory, name + ".png");
        if (UpdateMode)
        {
            Directory.CreateDirectory(BaselineDirectory);
            WritePng(actual, baselinePath);
            Report(name, "updated", 0, 0);
            return null;
        }

        if (!File.Exists(baselinePath))
            return Fail(name, $"{name}: no baseline at {baselinePath}. Run with PKHEX_VISUAL_UPDATE=1 to create it.", "missing", 0, 0);

        using var expected = SKBitmap.Decode(baselinePath).Copy(SKColorType.Rgba8888);
        if (expected.Width != actual.Width || expected.Height != actual.Height)
        {
            WritePng(expected, Path.Combine(ArtifactDirectory, name + ".expected.png"));
            return Fail(name, $"{name}: size {actual.Width}x{actual.Height} differs from baseline {expected.Width}x{expected.Height}.", "size", 1, 1);
        }

        using var diff = new SKBitmap(actual.Width, actual.Height, SKColorType.Rgba8888, SKAlphaType.Premul);
        var different = CountDifferences(expected, actual, diff);
        var total = actual.Width * actual.Height;
        var fraction = (double)different / total;
        if (fraction <= MaxDifferentFraction)
        {
            Report(name, different == 0 ? "match" : "within tolerance", different, fraction);
            return null;
        }

        WritePng(expected, Path.Combine(ArtifactDirectory, name + ".expected.png"));
        WritePng(diff, Path.Combine(ArtifactDirectory, name + ".diff.png"));
        return Fail(name, string.Create(CultureInfo.InvariantCulture,
            $"{name}: {different} of {total} pixels differ ({fraction:P3}, limit {MaxDifferentFraction:P3}). See {name}.diff.png in the visual-regression artifacts."),
            "different", different, fraction);
    }

    /// <summary>Fails the test when any frame mismatched on an enforcing OS.</summary>
    public static void AssertAll(IReadOnlyCollection<string> failures)
    {
        if (failures.Count == 0 || !IsEnforced)
            return;
        Assert.Fail("Visual baselines do not match. Review the visual-regression artifacts; if the change is intended, "
                    + "regenerate with PKHEX_VISUAL_UPDATE=1 (docs/visual-regression.md)." + Environment.NewLine
                    + string.Join(Environment.NewLine, failures));
    }

    private static string Fail(string name, string message, string status, int different, double fraction)
    {
        Report(name, IsEnforced ? status : status + " (not enforced on this OS)", different, fraction);
        return message;
    }

    private static readonly object SummaryLock = new();
    private static bool _summaryStarted;

    private static void Report(string name, string status, int different, double fraction)
    {
        var line = string.Create(CultureInfo.InvariantCulture, $"{name}\t{status}\t{different}\t{fraction:P3}{Environment.NewLine}");
        lock (SummaryLock)
        {
            Directory.CreateDirectory(ArtifactDirectory);
            var path = Path.Combine(ArtifactDirectory, "visual-summary.txt");
            if (!_summaryStarted)
            {
                _summaryStarted = true;
                File.WriteAllText(path, $"frame\tstatus\tdifferent pixels\tshare (os: {Environment.OSVersion}, enforced: {IsEnforced}){Environment.NewLine}", Encoding.UTF8);
            }
            File.AppendAllText(path, line, Encoding.UTF8);
        }
    }

    internal static int CountDifferences(SKBitmap expected, SKBitmap actual, SKBitmap? diff)
    {
        int width = expected.Width, height = expected.Height;
        var a = expected.Pixels;
        var b = actual.Pixels;
        var marks = diff is null ? null : new SKColor[a.Length];
        var count = 0;
        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                var i = y * width + x;
                var differs = !Close(a[i], b[i]) && (!HasNeighbour(a, b[i], x, y, width, height) || !HasNeighbour(b, a[i], x, y, width, height));
                if (differs)
                    count++;
                if (marks is not null)
                {
                    // Faded baseline for context, solid magenta for every pixel that counted.
                    var c = a[i];
                    marks[i] = differs ? new SKColor(255, 0, 255) : new SKColor((byte)(c.Red / 4 + 160), (byte)(c.Green / 4 + 160), (byte)(c.Blue / 4 + 160));
                }
            }
        }
        if (diff is not null)
            diff.Pixels = marks;
        return count;
    }

    private static bool HasNeighbour(SKColor[] image, SKColor colour, int x, int y, int width, int height)
    {
        for (var dy = -1; dy <= 1; dy++)
        {
            var ny = y + dy;
            if ((uint)ny >= (uint)height) continue;
            for (var dx = -1; dx <= 1; dx++)
            {
                var nx = x + dx;
                if ((uint)nx >= (uint)width) continue;
                if (Close(image[ny * width + nx], colour))
                    return true;
            }
        }
        return false;
    }

    private static bool Close(SKColor a, SKColor b) =>
        Math.Abs(a.Red - b.Red) <= ChannelTolerance && Math.Abs(a.Green - b.Green) <= ChannelTolerance
        && Math.Abs(a.Blue - b.Blue) <= ChannelTolerance && Math.Abs(a.Alpha - b.Alpha) <= ChannelTolerance;

    private static void WritePng(SKBitmap bitmap, string path)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        using var pixmap = bitmap.PeekPixels();
        using var data = pixmap.Encode(new SKPngEncoderOptions(SKPngEncoderFilterFlags.AllFilters, 9));
        using var file = File.Create(path);
        Assert.NotNull(data);
        data.SaveTo(file);
    }
}
