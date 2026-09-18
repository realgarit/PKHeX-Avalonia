using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Microsoft.Extensions.DependencyInjection;
using PKHeX.Application.Abstractions;
using PKHeX.Avalonia.Tests.Harness;
using PKHeX.Avalonia.Views;
using PKHeX.Core;

namespace PKHeX.Avalonia.Tests;

/// <summary>
/// Regression coverage for the compact editor composition. The production fields remain owned by
/// PokemonEditorViewModel; these checks cover the compact navigation and the fact that real controls
/// still receive usable bounds at the target editor width.
/// </summary>
public sealed class CompactPokemonEditorTests
{
    [AvaloniaFact]
    public void HaXModeDoesNotDisplayAnAffirmativeLegalPill()
    {
        var (vm, _, _) = TestHelpers.CreateTestViewModel(new PK9 { Species = 282 }, new SAV9SV(), haXMode: true);
        var view = new PokemonEditor { DataContext = vm };
        var window = Show(view, 306, 480);
        Assert.True(vm.IsHaXMode);
        Assert.True(vm.IsLegal); // Existing suppression flag, not proof of legality.
        Assert.False(view.FindControl<Button>("LegalityPill")!.IsVisible);
        window.Close();
    }

    [AvaloniaFact]
    public void CompactNavigation_UsesPrimaryButtonsAndLocalizedMoreMenu()
    {
        var save = new SAV9SV();
        var (vm, _, _) = TestHelpers.CreateTestViewModel(new PK9 { Species = (ushort)Species.Pikachu }, save);
        var view = new PokemonEditor { DataContext = vm };
        var window = Show(view, 306, 600);

        var tabs = view.GetVisualDescendants().OfType<TabControl>().Single();
        var primary = view.GetVisualDescendants()
            .OfType<Button>()
            .Where(button => button.Classes.Contains("editor-nav"))
            .ToArray();
        var more = view.FindControl<Button>("MoreSectionsButton");

        Assert.Equal(5, primary.Length);
        Assert.NotNull(more);
        Assert.False(string.IsNullOrWhiteSpace(more!.Content?.ToString()));
        Assert.Equal("Top", tabs.TabStripPlacement.ToString());
        Assert.DoesNotContain(tabs.GetVisualDescendants().OfType<ItemsPresenter>(), presenter => presenter.IsEffectivelyVisible);

        tabs.SelectedIndex = 3;
        Pump(window);
        Assert.Contains("selected", primary[3].Classes);
        Assert.DoesNotContain("selected", more.Classes);

        window.Close();
    }

    [AvaloniaFact]
    public void CompactNavigation_MoreMenuKeepsAdvancedSectionsReachable()
    {
        var save = new SAV6XY();
        var (vm, _, _) = TestHelpers.CreateTestViewModel(new PK6 { Species = (ushort)Species.Bulbasaur }, save);
        var view = new PokemonEditor { DataContext = vm };
        var window = Show(view, 306, 600);
        var tabs = view.GetVisualDescendants().OfType<TabControl>().Single();
        var more = view.FindControl<Button>("MoreSectionsButton");

        Assert.NotNull(more);
        Assert.True(vm.HasContestStats);
        Assert.True(vm.HasMemories);
        Assert.True(vm.HasRibbons);
        var flyout = Assert.IsType<MenuFlyout>(more!.Flyout);
        var entries = flyout.Items.OfType<MenuItem>().ToArray();
        Assert.Equal(3, entries.Length);
        Assert.Equal(["5", "6", "7"], entries.Select(item => item.Tag?.ToString() ?? string.Empty).ToArray());

        tabs.SelectedIndex = 5;
        Pump(window);
        Assert.Equal(5, tabs.SelectedIndex);
        Assert.Contains("selected", more.Classes);

        tabs.SelectedIndex = 6;
        Pump(window);
        Assert.Equal(6, tabs.SelectedIndex);
        Assert.Contains("selected", more.Classes);

        tabs.SelectedIndex = 7;
        Pump(window);
        Assert.Equal(7, tabs.SelectedIndex);
        Assert.Contains("selected", more.Classes);

        window.Close();
    }

    [AvaloniaFact]
    public void CompactEditor_MainFieldsRemainUsableAtTargetWidth()
    {
        var save = new SAV9SV();
        var (vm, _, _) = TestHelpers.CreateTestViewModel(new PK9
        {
            Species = (ushort)Species.Pikachu,
            Nickname = "Compact",
            CurrentLevel = 50,
        }, save);
        var view = new PokemonEditor { DataContext = vm };
        var window = Show(view, 306, 600);

        var species = view.GetVisualDescendants().OfType<PKHeX.Avalonia.Controls.FilterableComboBox>()
            .Single(field => field.AutomationPropertiesName() == "Species");
        var heldItem = view.GetVisualDescendants().OfType<PKHeX.Avalonia.Controls.FilterableComboBox>()
            .Single(field => field.AutomationPropertiesName() == "Held Item");
        var nickname = view.FindControl<TextBox>("NicknameField");

        Assert.True(species.Bounds.Width >= 80, $"Species field collapsed to {species.Bounds.Width} DIPs.");
        Assert.True(heldItem.Bounds.Width >= 80, $"Held Item field collapsed to {heldItem.Bounds.Width} DIPs.");
        Assert.NotNull(nickname);
        Assert.True(nickname!.Bounds.Width >= 80, $"Nickname field collapsed to {nickname.Bounds.Width} DIPs.");

        window.Close();
    }

    [AvaloniaFact]
    public void CaptureCompactEditorThemes_WhenEnabled_WritesPng()
    {
        if (Environment.GetEnvironmentVariable("PKHEX_HEADLESS_CAPTURE") != "1")
            return;

        using var app = new HeadlessAppFixture();
        var theme = app.Services.GetRequiredService<IThemeService>();
        var save = new SAV9SV();
        var (vm, _, _) = TestHelpers.CreateTestViewModel(new PK9 { Species = (ushort)Species.Gardevoir, CurrentLevel = 72 }, save);
        var directory = Environment.GetEnvironmentVariable("PKHEX_HEADLESS_CAPTURE_DIR")
            ?? Path.Combine(Path.GetTempPath(), "pkhex-headless-frames");
        Directory.CreateDirectory(directory);

        try
        {
            foreach (var (variant, name) in new[]
                     {
                         (AppTheme.Light, "pokemon-editor-306x600-light.png"),
                         (AppTheme.Dark, "pokemon-editor-306x600-dark.png"),
                     })
            {
                theme.ApplyTheme(variant);
                app.Pump();
                var view = new PokemonEditor { DataContext = vm };
                var window = new Window { Content = view, Width = 306, Height = 600 };
                window.Show();
                Pump(window);
                AvaloniaHeadlessPlatform.ForceRenderTimerTick();

                var frame = window.GetLastRenderedFrame();
                Assert.NotNull(frame);
                var path = Path.Combine(directory, name);
                using (var stream = File.Create(path))
                    frame!.Save(stream);
                Assert.True(new FileInfo(path).Length > 0, $"Capture was empty: {path}");
                window.Close();
            }
        }
        finally
        {
            theme.ApplyTheme(AppTheme.Dark);
        }
    }

    private static Window Show(Control content, double width, double height)
    {
        var window = new Window { Content = content, Width = width, Height = height };
        window.Show();
        Pump(window);
        return window;
    }

    private static void Pump(Window window)
    {
        Dispatcher.UIThread.RunJobs();
        window.UpdateLayout();
        Dispatcher.UIThread.RunJobs();
    }
}

internal static class CompactPokemonEditorTestExtensions
{
    public static string? AutomationPropertiesName(this Control control) =>
        global::Avalonia.Automation.AutomationProperties.GetName(control);
}
