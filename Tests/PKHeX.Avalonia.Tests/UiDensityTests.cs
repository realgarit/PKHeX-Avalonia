using System.Text.Json;
using System.Text.RegularExpressions;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Microsoft.Extensions.DependencyInjection;
using PKHeX.Avalonia.Services;
using PKHeX.Avalonia.Tests.Harness;
using PKHeX.Avalonia.Views;
using PKHeX.Application.Abstractions;
using PKHeX.Application.Services;
using PKHeX.Core;
using PKHeX.Presentation.ViewModels;

namespace PKHeX.Avalonia.Tests;

public class UiDensityTests
{
    [Fact]
    public void Theme_UsesCompactSharedStyleValues()
    {
        var theme = ReadSourceFile("Styles", "Theme.axaml");

        AssertStyleSetter(theme, "Border.card", "Padding", "{DynamicResource UiDensityCardPadding}");
        AssertStyleSetter(theme, "Border.card-elevated", "Padding", "{DynamicResource UiDensityCardPadding}");
        AssertStyleSetter(theme, "Button.accent-gradient", "Padding", "{DynamicResource UiDensityPrimaryButtonPadding}");
        AssertStyleSetter(theme, "Border.badge-success", "Padding", "{DynamicResource UiDensityBadgePadding}");
        AssertStyleSetter(theme, "Border.badge-error", "Padding", "{DynamicResource UiDensityBadgePadding}");
        AssertStyleSetter(theme, "Border.editor-section-padded", "Padding", "8,0,8,8");
        AssertStyleSetter(theme, "Border.view-container", "Padding", "{DynamicResource UiDensityViewPadding}");
        AssertStyleSetter(theme, "NumericUpDown.form-field", "MinHeight", "{DynamicResource UiDensityControlHeight}");
        AssertStyleSetter(theme, "NumericUpDown.form-field", "Padding", "{DynamicResource UiDensityFormFieldPadding}");
        AssertStyleSetter(theme, "DataGridRow", "MinHeight", "{DynamicResource UiDensityDataGridRowHeight}");
        AssertStyleSetter(theme, "DataGridCell", "Padding", "{DynamicResource UiDensityDataGridCellPadding}");
        Assert.Contains("Button.compact-primary", theme);
        Assert.Contains("Button.compact-secondary", theme);
        Assert.Contains("TabControl.compact-editor-tabs", theme);
        Assert.Contains("Button.compact-slot", theme);
        Assert.Contains("Window.compact-settings", theme);
        Assert.Contains("Border.compact-party-strip", theme);
    }

    [Fact]
    public void ControlSystem_UsesNativeFluentControlsAndSemanticInteractionStates()
    {
        var controls = ReadSourceFile("Styles", "ControlSystem.axaml");

        Assert.Contains("<Style Selector=\"ComboBox\">", controls);
        Assert.Contains("ComboBox /template/ Border#PopupBorder", controls);
        Assert.Contains("ComboBoxItem:selected /template/ ContentPresenter", controls);
        Assert.Contains("<Style Selector=\"CalendarDatePicker\">", controls);
        Assert.Contains("<Style Selector=\"NumericUpDown\">", controls);
        Assert.Contains("NumericUpDown:not(:disabled) /template/ TextBox#PART_TextBox", controls);
        Assert.Contains("<Style Selector=\"ToggleSwitch\">", controls);
        Assert.Contains("ThemeControlBorderFocusBrush", controls);
        Assert.DoesNotContain("ThemeControlMidBrush", controls);
    }

    [Fact]
    public void MainWindow_UsesCompactEditorColumnAndStatusBar()
    {
        var mainWindow = ReadSourceFile("Views", "MainWindow.axaml");
        var statusBar = Regex.Match(
            mainWindow,
            "<Border\\s+DockPanel.Dock=\"Bottom\"[^>]*>",
            RegexOptions.Singleline);

        Assert.Contains("Width=\"360\" MinWidth=\"320\" MaxWidth=\"440\"", mainWindow);
        Assert.DoesNotContain("ColumnDefinitions=\"520,*\"", mainWindow);
        Assert.True(statusBar.Success, "Status bar Border was not found.");
        Assert.Contains("Padding=\"{DynamicResource UiDensityStatusBarPadding}\"", statusBar.Value);
    }

    [Fact]
    public void PokemonEditor_UsesACompactHyperTrainingColumn()
    {
        var pokemonEditor = ReadSourceFile("Views", "PokemonEditor.axaml");

        Assert.Contains("ColumnDefinitions=\"40,40,42,42,30,26\"", pokemonEditor);
        Assert.Contains("x:Name=\"StatsHeaderHyperTraining\"", pokemonEditor);
        Assert.Contains("Text=\"{loc:Loc PokemonEditor_ColHyperTrainingShort}\"", pokemonEditor);
        Assert.Contains("Classes=\"compact stats-number\"", pokemonEditor);
        Assert.DoesNotContain("Content=\"{loc:Loc StatsHyperTrained}\"", pokemonEditor);
    }

    [Fact]
    public void PokemonEditor_UsesReadableDatePickers()
    {
        var pokemonEditor = ReadSourceFile("Views", "PokemonEditor.axaml");

        Assert.Equal(2, Regex.Matches(pokemonEditor, "SelectedDateFormat=\"Long\"").Count);
        Assert.Equal(2, Regex.Matches(pokemonEditor, "Watermark=\"{loc:Loc PokemonEditor_SelectDate}\"").Count);
    }

    [Fact]
    public void PokemonEditor_PokerusFieldsShareAFullWidthRow()
    {
        var pokemonEditor = ReadSourceFile("Views", "PokemonEditor.axaml");

        Assert.Contains("Grid.Row=\"1\" Grid.Column=\"0\" Grid.ColumnSpan=\"3\"", pokemonEditor);
        Assert.Contains("ColumnDefinitions=\"*,8,*\"", pokemonEditor);
    }

    [Fact]
    public void PokemonEditor_PidRerollGlyphIsExplicitlyCentered()
    {
        var pokemonEditor = ReadSourceFile("Views", "PokemonEditor.axaml");

        Assert.Contains("<Border Width=\"18\" Height=\"18\"", pokemonEditor);
        Assert.Equal(5, Regex.Matches(pokemonEditor, "<Ellipse Canvas.Left=").Count);
        Assert.Contains("HorizontalContentAlignment=\"Center\" VerticalContentAlignment=\"Center\"", pokemonEditor);
    }

    [Fact]
    public void PokemonEditor_ExposesHandlingTrainerFields()
    {
        var pokemonEditor = ReadSourceFile("Views", "PokemonEditor.axaml");

        Assert.Contains("IsVisible=\"{Binding CanEditHandlingTrainer}\"", pokemonEditor);
        Assert.Contains("Text=\"{Binding HandlingTrainerName}\"", pokemonEditor);
        Assert.Contains("SelectedValue=\"{Binding CurrentHandler}\"", pokemonEditor);
    }

    [Fact]
    public void PokemonEditor_UsesCompactContentSpacingAndMargin()
    {
        var pokemonEditor = ReadSourceFile("Views", "PokemonEditor.axaml");
        var topLevelContentStacks = Regex.Matches(
            pokemonEditor,
            "<ScrollViewer HorizontalScrollBarVisibility=\"Disabled\" VerticalScrollBarVisibility=\"Auto\">\\s*<StackPanel\\b[^>]*>");

        Assert.Equal(7, topLevelContentStacks.Count);
        Assert.All(topLevelContentStacks, stack =>
        {
            Assert.Contains("Spacing=\"8\"", stack.Value);
            Assert.Contains("Margin=\"6,6,6,12\"", stack.Value);
        });
    }

    [Fact]
    public void PokemonEditor_TabsUseOneQuietSelectedRule()
    {
        var theme = ReadSourceFile("Styles", "Theme.axaml");
        var selected = Regex.Match(
            theme,
            "<Style Selector=\"TabControl\\.editor-tabs TabItem:selected\">(?<body>.*?)</Style>",
            RegexOptions.Singleline);

        Assert.True(selected.Success);
        Assert.Contains("Background\" Value=\"Transparent\"", selected.Groups["body"].Value);
        Assert.Contains("BorderThickness\" Value=\"0\"", selected.Groups["body"].Value);
    }

    [Theory]
    [InlineData(AppDensity.Compact)]
    [InlineData(AppDensity.Comfortable)]
    public void AppSettings_RoundTripsDensityPreference_ThroughJson(AppDensity density)
    {
        var settings = new AppSettings { Density = new AppSettings.DensitySettings { Selected = density } };

        var restored = JsonSerializer.Deserialize<AppSettings>(JsonSerializer.Serialize(settings));

        Assert.NotNull(restored);
        Assert.Equal(density, restored!.Density.Selected);
    }

    [Fact]
    public void AppSettings_DefaultsToCompactDensity()
    {
        Assert.Equal(AppDensity.Compact, new AppSettings().Density.Selected);
    }

    [Fact]
    public void UiDensityService_ApplyDensity_UpdatesPreferenceAndPersists()
    {
        var settings = new AppSettings();
        var store = new FakeSettingsStore();
        var service = new UiDensityService(settings, store);

        service.ApplyDensity(AppDensity.Comfortable);

        Assert.Equal(AppDensity.Comfortable, service.CurrentDensity);
        Assert.Equal(AppDensity.Comfortable, settings.Density.Selected);
        Assert.Same(settings, store.Saved);
    }

    [AvaloniaFact]
    public void UiDensityService_ExposesStableCompactShellContract()
    {
        using var app = new HeadlessAppFixture();
        var density = app.Services.GetRequiredService<IUiDensityService>();
        var resources = global::Avalonia.Application.Current!.Resources;

        density.ApplyDensity(AppDensity.Compact);
        app.Pump();
        Assert.Equal(30d, resources["UiDensityControlHeight"]);
        Assert.Equal(306d, resources["CompactShellEditorWidth"]);
        Assert.Equal(35d, resources["CompactShellMenuHeight"]);
        Assert.Equal(49d, resources["CompactShellContextHeight"]);
        Assert.Equal(27d, resources["CompactShellStatusHeight"]);
        Assert.Equal(76d, resources["CompactPartyStripHeight"]);
        Assert.Equal(new Thickness(16), resources["CompactPanePadding"]);
        Assert.Equal(new Thickness(0, 0, 5, 5), resources["CompactSlotGap"]);

        density.ApplyDensity(AppDensity.Comfortable);
        app.Pump();
        Assert.Equal(36d, resources["UiDensityControlHeight"]);
        Assert.Equal(306d, resources["CompactShellEditorWidth"]);
        Assert.Equal(76d, resources["CompactPartyStripHeight"]);
    }

    [AvaloniaFact]
    public void UiDensityService_UpdatesLoadedReferenceViewAtRuntime()
    {
        using var app = new HeadlessAppFixture();
        app.LoadSaveInstance(new SAV9SV());

        var boxView = app.Find<BoxViewer>();
        Assert.NotNull(boxView);
        var viewContainer = boxView!.GetVisualDescendants()
            .OfType<Border>()
            .Single(border => border.Classes.Contains("view-container"));
        var editorTitle = app.Window.GetVisualDescendants()
            .OfType<TextBlock>()
            .Single(textBlock => textBlock.Text == "Empty Slot");
        var service = app.Services.GetRequiredService<IUiDensityService>();

        service.ApplyDensity(AppDensity.Compact);
        app.Pump();
        Assert.Equal(new Thickness(8), viewContainer.Padding);
        Assert.True(editorTitle.Bounds.Width > 0, "The editor header title must remain realized in Compact mode.");

        service.ApplyDensity(AppDensity.Comfortable);
        app.Pump();
        Assert.Equal(new Thickness(16), viewContainer.Padding);
        Assert.True(editorTitle.Bounds.Width > 0, "The editor header title must remain realized in Comfortable mode.");

        service.ApplyDensity(AppDensity.Compact);
    }

    [AvaloniaFact]
    public void SettingsView_ExposesAndAppliesDensityChoice()
    {
        var settings = new AppSettings();
        var store = new FakeSettingsStore();
        var density = new UiDensityService(settings, store);
        var vm = new SettingsViewModel(
            settings,
            store,
            new ThemeService(settings, store),
            density,
            new LanguageService(),
            UpdateTestDoubles.Coordinator());
        var view = new SettingsView { DataContext = vm };
        var window = new Window { Content = view, Width = 520, Height = 720 };
        window.Show();

        Dispatcher.UIThread.RunJobs();
        window.UpdateLayout();
        var combo = view.GetVisualDescendants().OfType<ComboBox>().Single(combo => combo.SelectedItem is AppDensity);
        Assert.Equal(AppDensity.Compact, combo.SelectedItem);

        combo.SelectedItem = AppDensity.Comfortable;
        Dispatcher.UIThread.RunJobs();

        Assert.Equal(AppDensity.Comfortable, vm.SelectedDensity);
        Assert.Equal(AppDensity.Comfortable, density.CurrentDensity);
        Assert.Same(settings, store.Saved);
        window.Close();
    }

    private static void AssertStyleSetter(string xaml, string selector, string property, string value)
    {
        var style = Regex.Match(
            xaml,
            $"<Style\\s+Selector=\"{Regex.Escape(selector)}\">(?<body>.*?)</Style>",
            RegexOptions.Singleline);

        Assert.True(style.Success, $"Style '{selector}' was not found.");
        Assert.Contains($"<Setter Property=\"{property}\" Value=\"{value}\" />", style.Groups["body"].Value);
    }

    private static string ReadSourceFile(params string[] relativePath)
    {
        var path = Path.Combine([FindRepoRoot(), "PKHeX.Avalonia", .. relativePath]);
        Assert.True(File.Exists(path), $"Source file not found: {path}");
        return File.ReadAllText(path);
    }

    private static string FindRepoRoot()
    {
        var dir = AppDomain.CurrentDomain.BaseDirectory;
        while (!Directory.Exists(Path.Combine(dir, "PKHeX.Avalonia")))
        {
            var parent = Directory.GetParent(dir);
            if (parent == null)
                throw new DirectoryNotFoundException("Could not find repository root");
            dir = parent.FullName;
        }

        return dir;
    }
}
