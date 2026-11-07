using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Styling;
using Avalonia.VisualTree;
using PKHeX.Application.Services;
using PKHeX.Avalonia.Services;
using PKHeX.Avalonia.Views;
using PKHeX.Core;
using PKHeX.Presentation.ViewModels;
using SkiaSharp;

namespace PKHeX.Avalonia.Render.Tests;

/// <summary>Real-Skia frames of the slot hover card (issue #244) in both themes.</summary>
public class SlotPreviewCardRenderTests
{
    [AvaloniaTheory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task LegalAndIllegalCardsRenderWithDistinctSemanticStyling(bool dark)
    {
        var previous = global::Avalonia.Application.Current!.RequestedThemeVariant;
        try
        {
            global::Avalonia.Application.Current.RequestedThemeVariant = dark ? ThemeVariant.Dark : ThemeVariant.Light;
            var legal = await RenderCard(LegalPreview(), $"slot-preview-legal-{(dark ? "dark" : "light")}");
            var illegal = await RenderCard(IllegalPreview(), $"slot-preview-illegal-{(dark ? "dark" : "light")}");

            foreach (var frame in new[] { legal, illegal })
            {
                // Real content: many distinct colors (sprite, ball, type icons, text), not a blank surface.
                Assert.True(frame.Bitmap.Pixels.Select(p => (uint)p).Distinct().Count() > 50);
                Assert.True(frame.CardSize.Width <= 320 && frame.CardSize.Height > 120, $"{frame.CardSize}");
            }

            // Legality state uses different semantic brushes; the pill always also carries text.
            Assert.NotEqual(legal.PillBackground, illegal.PillBackground);
            Assert.NotEqual(legal.PillBorder, illegal.PillBorder);
            Assert.NotEqual(Colors.Transparent, legal.PillBorder);
            Assert.True(illegal.IllegalMoveRows > 0);
            Assert.Equal(0, legal.IllegalMoveRows);
            Assert.Equal(illegal.ErrorBrush, illegal.IllegalMoveForeground);

            // The card surface follows the theme.
            var surface = legal.Bitmap.GetPixel(4, (int)legal.CardSize.Height / 2);
            var luminance = 0.2126 * surface.Red + 0.7152 * surface.Green + 0.0722 * surface.Blue;
            if (dark) Assert.True(luminance < 110, $"dark surface {surface}");
            else Assert.True(luminance > 150, $"light surface {surface}");
            legal.Bitmap.Dispose();
            illegal.Bitmap.Dispose();
        }
        finally { global::Avalonia.Application.Current.RequestedThemeVariant = previous; }
    }

    private sealed record Frame(SKBitmap Bitmap, Size CardSize, Color PillBackground, Color PillBorder, int IllegalMoveRows, Color? IllegalMoveForeground, Color ErrorBrush);

    private static async Task<Frame> RenderCard(SlotPreviewViewModel preview, string name)
    {
        var card = new SlotPreviewCard { DataContext = preview };
        var window = new Window { Content = card, SizeToContent = SizeToContent.WidthAndHeight, Background = Brushes.Transparent };
        try
        {
            window.Show();
            await PKHeX.Testing.HeadlessRenderSettling.Settle(window);
            var pill = card.GetVisualDescendants().OfType<Border>().Single(b => b.Name == "LegalityPill");
            var illegalMoves = card.GetVisualDescendants().OfType<TextBlock>().Where(t => t.Classes.Contains("move-name") && t.Classes.Contains("illegal")).ToArray();
            Assert.True(card.TryFindResource("ThemeErrorBrush", card.ActualThemeVariant, out var errorResource));
            var error = ((ISolidColorBrush)errorResource!).Color;
            using var target = new RenderTargetBitmap(new PixelSize((int)Math.Ceiling(card.Bounds.Width), (int)Math.Ceiling(card.Bounds.Height)));
            target.Render(card);
            using var bytes = new MemoryStream();
            target.Save(bytes, PngBitmapEncoderOptions.Default);
            var bitmap = SKBitmap.Decode(bytes.ToArray());
            SaveFrame(bitmap, name);
            return new Frame(bitmap, card.Bounds.Size,
                ((ISolidColorBrush)pill.Background!).Color,
                ((ISolidColorBrush)pill.BorderBrush!).Color,
                illegalMoves.Length,
                (illegalMoves.FirstOrDefault()?.Foreground as ISolidColorBrush)?.Color,
                error);
        }
        finally { window.Close(); }
    }

    private static SlotPreviewViewModel LegalPreview()
    {
        var root = new DirectoryInfo(AppContext.BaseDirectory);
        while (root is not null && !File.Exists(Path.Combine(root.FullName, "PKHeX.sln"))) root = root.Parent;
        Assert.NotNull(root);
        var pk = new PK9(File.ReadAllBytes(Path.Combine(root.FullName, "Tests", "PKHeX.Core.Tests", "Legality", "Legal", "Generation 9", "FormArg", "0983 - Kingambit - 6BE42E9A9EF4 0 asKing.pk9")));
        var save = new SAV9SV { Version = pk.Version, OT = pk.OriginalTrainerName, ID32 = pk.ID32, Gender = pk.OriginalTrainerGender, Language = pk.Language };
        var preview = SlotPreviewViewModel.Create(pk, save, StorageSlotType.Box, Renderer(save), haXMode: false, includeEncounter: true);
        Assert.NotNull(preview);
        Assert.True(preview.IsLegal, preview.LegalityHint);
        return preview;
    }

    private static SlotPreviewViewModel IllegalPreview()
    {
        var save = new SAV6XY();
        var pk = new PK6 { Species = (ushort)Species.Budew, Nickname = "Budew", PID = 0x12345678, CurrentLevel = 5, Ball = (byte)Ball.Poke, HeldItem = 1, Move1 = (ushort)Move.Absorb, Move2 = (ushort)Move.HydroPump };
        var preview = SlotPreviewViewModel.Create(pk, save, StorageSlotType.Box, Renderer(save), haXMode: false, includeEncounter: true);
        Assert.NotNull(preview);
        Assert.True(preview.IsIllegal);
        return preview;
    }

    private static AvaloniaSpriteRenderer Renderer(SaveFile save)
    {
        var renderer = new AvaloniaSpriteRenderer(new AppSettings());
        renderer.Initialize(save);
        return renderer;
    }

    private static void SaveFrame(SKBitmap frame, string name)
    {
        if (Environment.GetEnvironmentVariable("PKHEX_HEADLESS_CAPTURE") != "1" || Environment.GetEnvironmentVariable("PKHEX_HEADLESS_CAPTURE_DIR") is not { } path) return;
        Directory.CreateDirectory(path);
        using var image = SKImage.FromBitmap(frame); using var data = image.Encode(SKEncodedImageFormat.Png, 100);
        File.WriteAllBytes(Path.Combine(path, name + ".png"), data.ToArray());
    }
}
