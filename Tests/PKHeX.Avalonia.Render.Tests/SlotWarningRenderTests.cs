using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Media.Imaging;
using Avalonia.Styling;
using Avalonia.VisualTree;
using PKHeX.Application.Services;
using PKHeX.Avalonia.Services;
using PKHeX.Avalonia.Views;
using PKHeX.Core;
using PKHeX.Presentation.Localization;
using PKHeX.Presentation.Models;
using PKHeX.Presentation.ViewModels;
using SkiaSharp;
using Path = Avalonia.Controls.Shapes.Path;

namespace PKHeX.Avalonia.Render.Tests;

public class SlotWarningRenderTests
{
    [AvaloniaTheory]
    [InlineData(false, SpritePreference.ForceSprites, false)]
    [InlineData(true, SpritePreference.ForceSprites, false)]
    [InlineData(false, SpritePreference.ForceMugshots, false)]
    [InlineData(true, SpritePreference.ForceMugshots, false)]
    [InlineData(false, SpritePreference.ForceArtwork, false)]
    [InlineData(true, SpritePreference.ForceArtwork, false)]
    [InlineData(false, SpritePreference.ForceSprites, true)]
    [InlineData(true, SpritePreference.ForceSprites, true)]
    public async Task BoxAndBothPartyViewsShowOnlyTheUsualWarning(bool dark, SpritePreference preference, bool haXMode)
    {
        var previous = global::Avalonia.Application.Current!.RequestedThemeVariant;
        var settings = new AppSettings();
        settings.Sprite.SpritePreference = preference;
        var save = new SAV6XY();
        var pokemon = new[]
        {
            new PK6 { Species = (ushort)Species.Budew, Nickname = "Budew", PID = 0x12345678 },
            new PK6 { Species = (ushort)Species.Magikarp, Nickname = "Magikarp", PID = 0x12345678 },
        };
        for (var i = 0; i < pokemon.Length; i++)
        {
            save.SetBoxSlotAtIndex(pokemon[i], 0, 28 + i, EntityImportSettings.None);
            save.SetPartySlotAtIndex(pokemon[i], i, EntityImportSettings.None);
        }
        var renderer = new AvaloniaSpriteRenderer(settings); renderer.Initialize(save);
        var boxes = new BoxViewerViewModel(save, renderer, haXMode: haXMode);
        var party = new PartyViewerViewModel(save, renderer, haXMode: haXMode);
        Assert.Equal(haXMode, boxes.Slots[28].IsLegal);
        Assert.Equal(haXMode, party.Slots[0].IsLegal);
        for (var i = 0; i < pokemon.Length; i++)
        {
            // Box sprites now match the preview exactly: the lower-left illegal bitmap is absent.
            Assert.Equal(renderer.GetSprite(pokemon[i]), boxes.Slots[28 + i].Sprite);
            using var plain = SKBitmap.Decode(renderer.GetSprite(pokemon[i])!);
            using var partySprite = SKBitmap.Decode(party.Slots[i].Sprite!);
            Assert.Equal(Pixels(plain, SKRectI.Create(0, 44, 12, 12)), Pixels(partySprite, SKRectI.Create(0, 44, 12, 12)));
            Assert.Contains(Pixels(partySprite, SKRectI.Create(14, 44, 12, 12)), p => p.Alpha > 0); // Party position remains.
            if (!haXMode)
            {
                Assert.Contains(LocalizedStrings.Instance["SlotState_Illegal"], boxes.Slots[28 + i].AccessibleName);
                Assert.Contains(LocalizedStrings.Instance["SlotState_Illegal"], party.Slots[i].AccessibleName);
            }
        }

        try
        {
            global::Avalonia.Application.Current.RequestedThemeVariant = dark ? ThemeVariant.Dark : ThemeVariant.Light;
            foreach (var (name, view, width, height) in new (string, Control, int, int)[]
            {
                ("box", new BoxViewer { DataContext = boxes }, 490, 400),
                ("party-strip", new PartyStrip { DataContext = party }, 490, 90),
                ("party-tool", new PartyViewer { DataContext = party }, 490, 400),
            })
            {
                var window = new Window { Content = view, Width = width, Height = height };
                try
                {
                    // Party metadata is shared by strip/tool, so restore it after the preceding frame comparison.
                    foreach (var slot in party.Slots.Where(s => !s.IsEmpty)) slot.IsLegal = haXMode;
                    window.Show(); await PKHeX.Testing.HeadlessRenderSettling.Settle(window);
                    var occupied = view.GetVisualDescendants().OfType<Button>().Where(b => b.Tag is SlotData { IsEmpty: false } or PartySlotData { IsEmpty: false }).ToArray();
                    Assert.Equal(2, occupied.Length);
                    var warnings = occupied.SelectMany(b => b.GetVisualDescendants().OfType<Path>()).Where(p => p.IsEffectivelyVisible).ToArray();
                    Assert.Equal(haXMode ? 0 : 2, warnings.Length);
                    // The compact strip uses an unstretched 16x14 geometry in a 9x8 layout slot.
                    // Test the actual drawn geometry, including that existing visual overflow.
                    var warningBounds = warnings.Select(p => new Rect(p.TranslatePoint(p.Data!.Bounds.TopLeft, window)!.Value, p.Data.Bounds.Size)).ToArray();
                    using var marked = Render(window);
                    SaveFrame(marked, $"single-warning-{name}-{preference}-{(dark ? "dark" : "light")}-{(haXMode ? "hax" : "normal")}");
                    foreach (var slot in boxes.Slots.Where(s => !s.IsEmpty)) slot.IsLegal = true;
                    foreach (var slot in party.Slots.Where(s => !s.IsEmpty)) slot.IsLegal = true;
                    await PKHeX.Testing.HeadlessRenderSettling.Settle(window);
                    using var clear = Render(window);
                    var differences = ChangedPixels(marked, clear).ToArray();
                    if (haXMode) Assert.Empty(differences);
                    else
                    {
                        Assert.NotEmpty(differences);
                        // Removing the view warning changes only its two upper-right triangles.
                        Assert.All(differences, point => Assert.Contains(warningBounds, bounds => bounds.Inflate(1).Contains(point)));
                        foreach (var bounds in warningBounds) Assert.Contains(differences, bounds.Contains);
                    }
                }
                finally { window.Close(); }
            }
        }
        finally { global::Avalonia.Application.Current.RequestedThemeVariant = previous; }
    }

    private static SKColor[] Pixels(SKBitmap bitmap, SKRectI rect) => Enumerable.Range(rect.Top, rect.Height)
        .SelectMany(y => Enumerable.Range(rect.Left, rect.Width).Select(x => bitmap.GetPixel(x, y))).ToArray();

    private static IEnumerable<Point> ChangedPixels(SKBitmap first, SKBitmap second)
    {
        Assert.Equal((first.Width, first.Height), (second.Width, second.Height));
        for (var y = 0; y < first.Height; y++)
        for (var x = 0; x < first.Width; x++)
            if (first.GetPixel(x, y) != second.GetPixel(x, y)) yield return new Point(x, y);
    }

    private static SKBitmap Render(Window window)
    {
        using var frame = new RenderTargetBitmap(new PixelSize((int)window.ClientSize.Width, (int)window.ClientSize.Height));
        frame.Render(window);
        using var bytes = new MemoryStream(); frame.Save(bytes, PngBitmapEncoderOptions.Default);
        return SKBitmap.Decode(bytes.ToArray());
    }

    private static void SaveFrame(SKBitmap frame, string name)
    {
        if (Environment.GetEnvironmentVariable("PKHEX_HEADLESS_CAPTURE") != "1" || Environment.GetEnvironmentVariable("PKHEX_HEADLESS_CAPTURE_DIR") is not { } path) return;
        Directory.CreateDirectory(path);
        using var image = SKImage.FromBitmap(frame); using var data = image.Encode(SKEncodedImageFormat.Png, 100);
        File.WriteAllBytes(System.IO.Path.Combine(path, name + ".png"), data.ToArray());
    }
}
