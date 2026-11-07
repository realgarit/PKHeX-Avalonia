using System.Globalization;
using System.Text;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Headless.XUnit;
using Avalonia.Media.Imaging;
using Avalonia.VisualTree;
using Moq;
using PKHeX.Application.Abstractions;
using PKHeX.Application.Services;
using PKHeX.Avalonia.Services;
using PKHeX.Core;
using PKHeX.Presentation.Localization;
using PKHeX.Presentation.ViewModels;

namespace PKHeX.Avalonia.Render.Tests;

/// <summary>
/// Issue #249: auxiliary editors open compact, keep their labels unclipped and keep their primary
/// actions on screen at 100, 125, 150 and 200 % display scaling, in English and with longer German
/// labels. Display scaling is reproduced the way <see cref="WindowService"/> sees it: a 1920 x 1080
/// desktop (1040 DIP work area at 100 %) divided by the scale factor.
/// </summary>
public class CompactDialogScalingRenderTests
{
    public static IEnumerable<object[]> Cases =>
        from dialog in new[] { "opower", "roamer6", "link6", "underground8b", "medal5" }
        from scale in new[] { 1.0, 1.25, 1.5, 2.0 }
        from language in new[] { "en", "de" }
        select new object[] { dialog, scale, language };

    // Compact first-frame budgets. They are far below the 670 x 850 (O-Power), 981 x 700 (Pokémon Link)
    // and 1402 x 852 (Underground, Medal Rally) windows that the issue measured.
    private static (double Width, double Height) Budget(string dialog) => dialog switch
    {
        "opower" => (600, 500),
        "roamer6" => (440, 320),
        "link6" => (640, 540),
        "underground8b" => (700, 500),
        "medal5" => (920, 660),
        _ => throw new ArgumentOutOfRangeException(nameof(dialog)),
    };

    private static object Build(string dialog) => dialog switch
    {
        "opower" => new OPowerEditorViewModel(Load<SAV6XY>("gen6_x.main")),
        "roamer6" => new Roamer6EditorViewModel(Load<SAV6XY>("gen6_x.main")),
        "link6" => new Link6EditorViewModel(Load<SAV6XY>("gen6_x.main"), Mock.Of<IDialogService>()),
        "underground8b" => new Underground8bEditorViewModel(Load<SAV8BS>("gen8b_brilliantdiamond.bin")),
        "medal5" => new MedalEditorViewModel(Load<SAV5B2W2>("gen5_white2.sav")),
        _ => throw new ArgumentOutOfRangeException(nameof(dialog)),
    };

    [AvaloniaTheory]
    [MemberData(nameof(Cases))]
    public async Task DialogFitsAndKeepsTextAndActionsVisible(string dialog, double scale, string language)
    {
        var previous = LocalizedStrings.Instance.CurrentLanguage; var dataLanguage = GameInfo.CurrentLanguage;
        var strings = GameInfo.Strings; var filtered = GameInfo.FilteredSources;
        var culture = CultureInfo.CurrentCulture; var uiCulture = CultureInfo.CurrentUICulture;
        var defaultCulture = CultureInfo.DefaultThreadCurrentCulture; var defaultUiCulture = CultureInfo.DefaultThreadCurrentUICulture;
        Window? window = null;
        var diagnostics = new StringBuilder();
        try
        {
            new LanguageService().SetLanguage(language); LocalizedStrings.Instance.SetLanguage(language);
            var available = WindowService.ComputeAvailableToolSize(1920, 1040, scale);
            window = new Window
            {
                Content = ViewLocator.Build(Build(dialog)),
                MaxWidth = Math.Max(620, available.Width),
                MaxHeight = Math.Max(420, available.Height),
            };
            WindowService.SetMeasuredInitialBounds(window);
            WindowService.ConstrainToolBounds(window);
            diagnostics.AppendLine($"{dialog} {language} {scale:P0}: {window.Width} x {window.Height} (max {window.MaxWidth} x {window.MaxHeight})");

            var budget = Budget(dialog);
            Assert.True(window.Width <= Math.Min(budget.Width, window.MaxWidth) && window.Height <= Math.Min(budget.Height, window.MaxHeight), diagnostics.ToString());

            window.Show();
            await PKHeX.Testing.HeadlessRenderSettling.Settle(window);
            var client = new Rect(window.ClientSize);
            using (var frame = new RenderTargetBitmap(new PixelSize((int)client.Width, (int)client.Height)))
            {
                frame.Render(window);
                if (Environment.GetEnvironmentVariable("PKHEX_HEADLESS_CAPTURE") == "1" && Environment.GetEnvironmentVariable("PKHEX_HEADLESS_CAPTURE_DIR") is { } path)
                {
                    Directory.CreateDirectory(path);
                    frame.Save(Path.Combine(path, $"compact-{dialog}-{language}-{scale * 100:0}.png"), PngBitmapEncoderOptions.Default);
                }
            }

            AssertNoClippedText(window, diagnostics);

            // Primary actions (outside scrolling content) are fully inside the client area.
            foreach (var button in window.GetVisualDescendants().OfType<Button>().Where(b => b.IsEffectivelyVisible
                         && b.FindAncestorOfType<ScrollContentPresenter>() is null && b.FindAncestorOfType<DataGrid>() is null))
            {
                var bounds = button.Bounds.TranslateToWindow(button, window);
                Assert.True(client.Contains(bounds), $"{dialog}: action '{button.Content}' at {bounds} is outside {client}.\n{diagnostics}");
            }
        }
        finally
        {
            window?.Close(); LocalizedStrings.Instance.SetLanguage(previous); GameInfo.CurrentLanguage = dataLanguage;
            GameInfo.Strings = strings; GameInfo.FilteredSources = filtered;
            CultureInfo.CurrentCulture = culture; CultureInfo.CurrentUICulture = uiCulture;
            CultureInfo.DefaultThreadCurrentCulture = defaultCulture; CultureInfo.DefaultThreadCurrentUICulture = defaultUiCulture;
        }
    }

    /// <summary>
    /// Every visible label shows its whole text: no ellipsis, and no clipping ancestor cuts it
    /// horizontally. Rows a scroll viewer has moved out of view are skipped; scrolling is not clipping.
    /// </summary>
    internal static void AssertNoClippedText(Window window, StringBuilder diagnostics)
    {
        foreach (var text in window.GetVisualDescendants().OfType<TextBlock>())
        {
            if (!text.IsEffectivelyVisible || string.IsNullOrWhiteSpace(text.Text) || text.Bounds.Width <= 0)
                continue;
            if (text.FindAncestorOfType<ToolTip>() is not null || text.FindAncestorOfType<ComboBox>() is not null || text.FindAncestorOfType<NumericUpDown>() is not null)
                continue;

            var layout = text.TextLayout;
            var name = $"\"{text.Text}\"";
            Assert.False(layout.TextLines.Any(line => line.HasCollapsed), $"{name} is trimmed.\n{diagnostics}");

            var viewport = text.FindAncestorOfType<ScrollContentPresenter>();
            if (viewport is not null)
            {
                var origin = text.TranslatePoint(default, viewport)!.Value;
                if (origin.Y < 0 || origin.Y + text.Bounds.Height > viewport.Bounds.Height || origin.X >= viewport.Bounds.Width)
                    continue;
            }

            var right = Math.Min(layout.WidthIncludingTrailingWhitespace, text.Bounds.Width + 0.5);
            var natural = layout.TextLines.Max(line => line.WidthIncludingTrailingWhitespace);
            Assert.True(natural <= text.Bounds.Width + 0.75, $"{name} needs {natural:0.#} but has {text.Bounds.Width:0.#}.\n{diagnostics}");
            var clips = text.GetVisualAncestors().Where(visual => visual.ClipToBounds).ToArray();
            // Recycled grid rows are parked far outside their panel; they are not on screen at all.
            if (clips.Any(clip => text.TranslatePoint(default, clip)!.Value.X >= clip.Bounds.Width))
                continue;
            foreach (var clip in clips)
            {
                var left = text.TranslatePoint(default, clip)!.Value.X;
                var end = text.TranslatePoint(new Point(right, 0), clip)!.Value.X;
                Assert.True(left >= -0.5 && end <= clip.Bounds.Width + 0.5,
                    $"{name} [{left:0.#}, {end:0.#}] is cut by {clip.GetType().Name} [0, {clip.Bounds.Width:0.#}].\n{diagnostics}");
            }
        }
    }

    private static T Load<T>(string name) where T : SaveFile
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Tests/savefiles", name))) directory = directory.Parent;
        Assert.NotNull(directory);
        return Assert.IsType<T>(SaveUtil.GetSaveFile(File.ReadAllBytes(Path.Combine(directory.FullName, "Tests/savefiles", name))));
    }
}

internal static class RectExtensions
{
    public static Rect TranslateToWindow(this Rect bounds, Visual visual, Window window)
    {
        var origin = visual.TranslatePoint(default, window) ?? default;
        return new Rect(origin, bounds.Size);
    }
}
