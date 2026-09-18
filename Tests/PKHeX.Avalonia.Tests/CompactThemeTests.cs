using Avalonia;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.Headless.XUnit;
using Microsoft.Extensions.DependencyInjection;
using PKHeX.Application.Abstractions;
using PKHeX.Avalonia.Services;
using PKHeX.Avalonia.Tests.Harness;

namespace PKHeX.Avalonia.Tests;

/// <summary>
/// Verifies that the compact palette is exposed through Avalonia's theme-aware resource lookup
/// and that switching the production theme service changes the requested variant for open windows.
/// The source-level coverage in ResponsiveShellTests protects the exact design tokens; this test
/// protects the runtime resource path those tokens use.
/// </summary>
public sealed class CompactThemeTests
{
    private static readonly string[] RequiredBrushes =
    [
        "CompactAccentBrush",
        "CompactOnAccentBrush",
        "CompactAccentTextBrush",
        "CompactSelectionBrush",
        "CompactSelectionBorderBrush",
        "CompactFocusBrush",
    ];

    [AvaloniaFact]
    public void CompactBrushesResolveForBothThemeVariants()
    {
        using var app = new HeadlessAppFixture();
        var styles = global::Avalonia.Application.Current!.Styles;

        foreach (var key in RequiredBrushes)
        {
            Assert.True(styles.TryGetResource(key, ThemeVariant.Light, out var light),
                $"Missing light compact resource '{key}'.");
            Assert.IsType<SolidColorBrush>(light);
            Assert.True(styles.TryGetResource(key, ThemeVariant.Dark, out var dark),
                $"Missing dark compact resource '{key}'.");
            Assert.IsType<SolidColorBrush>(dark);
        }
    }

    [AvaloniaFact]
    public void ThemeServiceSwitchesCompactPaletteForOpenWindow()
    {
        using var app = new HeadlessAppFixture();
        var theme = app.Services.GetRequiredService<IThemeService>();

        theme.ApplyTheme(AppTheme.Light);
        app.Pump();
        Assert.Equal(ThemeVariant.Light, global::Avalonia.Application.Current!.RequestedThemeVariant);
        Assert.True(global::Avalonia.Application.Current!.Styles.TryGetResource(
            "CompactAccentBrush", ThemeVariant.Light, out var lightAccent));

        theme.ApplyTheme(AppTheme.Dark);
        app.Pump();
        Assert.Equal(ThemeVariant.Dark, global::Avalonia.Application.Current.RequestedThemeVariant);
        Assert.True(global::Avalonia.Application.Current.Styles.TryGetResource(
            "CompactAccentBrush", ThemeVariant.Dark, out var darkAccent));

        Assert.NotEqual(((SolidColorBrush)lightAccent!).Color, ((SolidColorBrush)darkAccent!).Color);
    }
}
