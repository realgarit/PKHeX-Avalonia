using Avalonia.Controls;
using Avalonia;
using Avalonia.Headless.XUnit;
using Avalonia.Layout;
using Avalonia.VisualTree;
using PKHeX.Avalonia.Tests.Harness;
using PKHeX.Core;
using PKHeX.Presentation.Localization;
using PKHeX.Presentation.ViewModels;

namespace PKHeX.Avalonia.Tests;

public sealed class ResponsiveShellTests
{
    [Fact]
    public void MainWindow_DeclaresCompactResizableEditorAndDistinctWorkspaceTabs()
    {
        var mainWindow = ReadSourceFile("Views", "MainWindow.axaml");
        var theme = ReadSourceFile("Styles", "Theme.axaml");

        Assert.Contains("Width=\"1024\" Height=\"720\"", mainWindow);
        Assert.Contains("Width=\"360\" MinWidth=\"320\" MaxWidth=\"440\"", mainWindow);
        Assert.Contains("x:Name=\"EditorPane\"", mainWindow);
        Assert.Contains("x:Name=\"WorkspacePane\"", mainWindow);
        Assert.Contains("Classes=\"app-header\"", mainWindow);
        Assert.Contains("Classes=\"workspace-rail\"", mainWindow);
        Assert.Contains("Classes=\"reports-surface\"", mainWindow);
        Assert.Contains("Classes=\"reports-layout\"", mainWindow);
        Assert.Contains("<UniformGrid Columns=\"3\" />", mainWindow);
        Assert.Contains("SelectedIndex=\"{Binding SelectedWorkspaceIndex, Mode=TwoWay}\"", mainWindow);
        Assert.Contains("Classes=\"pane-splitter shell-divider\"", mainWindow);
        Assert.Contains("Classes=\"workspace-tabs\"", mainWindow);
        Assert.Contains("ItemsSource=\"{Binding AvailableToolMenuGroups}\"", mainWindow);
        Assert.DoesNotContain("<MenuItem Header=\"{loc:Loc Menu_Pokemon}\"", mainWindow);
        Assert.DoesNotContain("<MenuItem Header=\"{loc:Loc Menu_Gen1}\"", mainWindow);
        Assert.DoesNotContain("ThemeAccentBrush\"", mainWindow);
        Assert.Equal(7, System.Text.RegularExpressions.Regex.Matches(mainWindow, "Classes=\"workspace-tab\"").Count);

        Assert.Contains("TabControl.editor-tabs TabItem.editor-tab:selected", theme);
        Assert.Contains("TabControl.editor-tabs TabItem.editor-tab:selected /template/ Border#PART_SelectedPipe", theme);
        Assert.Contains("TabControl.workspace-tabs TabItem.workspace-tab:selected /template/ Border#PART_SelectedPipe", theme);
        Assert.Contains("BorderThickness\" Value=\"0\"", theme);
        Assert.Contains("TabControl.workspace-tabs TabItem.workspace-tab:selected", theme);
        Assert.Contains("TabControl.editor-tabs TabItem.editor-tab:focus-visible", theme);
        Assert.Contains("TabControl.workspace-tabs TabItem.workspace-tab:focus-visible", theme);
        var selectedRailStyle = System.Text.RegularExpressions.Regex.Match(
            theme,
            "<Style Selector=\"ToggleButton\\.workspace-nav-item:checked\">(?<body>.*?)</Style>",
            System.Text.RegularExpressions.RegexOptions.Singleline);
        Assert.True(selectedRailStyle.Success);
        Assert.Contains("Background\" Value=\"Transparent\"", selectedRailStyle.Groups["body"].Value);
        Assert.Contains("BorderThickness\" Value=\"1\"", selectedRailStyle.Groups["body"].Value);
        Assert.DoesNotContain("ThemeBackgroundElevatedBrush", selectedRailStyle.Groups["body"].Value);
        Assert.Contains("ToggleButton.workspace-nav-item:checked /template/ ContentPresenter#PART_ContentPresenter", theme);
        var selectedEditorStyle = System.Text.RegularExpressions.Regex.Match(
            theme,
            "<Style Selector=\"TabControl\\.editor-tabs TabItem\\.editor-tab:selected\">(?<body>.*?)</Style>",
            System.Text.RegularExpressions.RegexOptions.Singleline);
        Assert.True(selectedEditorStyle.Success);
        Assert.DoesNotContain("ThemeAccentGlowBrush", selectedEditorStyle.Groups["body"].Value);
        Assert.Contains("Background\" Value=\"Transparent\"", selectedEditorStyle.Groups["body"].Value);

        // Save identity is intentionally a single piece of application chrome: the top header owns
        // the filename, while the left rail and bottom status bar carry only task/status context.
        Assert.Single(System.Text.RegularExpressions.Regex.Matches(mainWindow, "Text=\"\\{Binding CurrentSaveFileName\\}\""));
        Assert.DoesNotContain("rail-save-card", mainWindow);
        Assert.DoesNotContain("rail-status", mainWindow);
        var statusBar = System.Text.RegularExpressions.Regex.Match(
            mainWindow,
            "<Border DockPanel.Dock=\"Bottom\"(?<body>.*?)</Border>",
            System.Text.RegularExpressions.RegexOptions.Singleline);
        Assert.True(statusBar.Success);
        Assert.DoesNotContain("CurrentSaveFileName", statusBar.Groups["body"].Value);
    }

    [Fact]
    public void VisibleThemeTokensStayNeutral()
    {
        var theme = ReadSourceFile("Styles", "Theme.axaml");
        var dictionaries = System.Text.RegularExpressions.Regex.Matches(
            theme,
            "<ResourceDictionary x:Key=\"(?<name>Dark|Light)\">(?<body>.*?)</ResourceDictionary>",
            System.Text.RegularExpressions.RegexOptions.Singleline);

        Assert.Equal(2, dictionaries.Count);
        foreach (System.Text.RegularExpressions.Match dictionary in dictionaries)
        {
            var name = dictionary.Groups["name"].Value;
            var body = dictionary.Groups["body"].Value;
            var tokens = System.Text.RegularExpressions.Regex.Matches(
                body,
                "<Color x:Key=\"(?<key>Theme(?:Background|Border|Control|Text|Accent)[^\"]*)\">#(?<hex>[0-9A-Fa-f]{6,8})</Color>");

            Assert.NotEmpty(tokens);
            foreach (System.Text.RegularExpressions.Match token in tokens)
                AssertNeutralColor(name, token.Groups["key"].Value, token.Groups["hex"].Value);

            foreach (System.Text.RegularExpressions.Match gradient in System.Text.RegularExpressions.Regex.Matches(
                         body,
                         "<LinearGradientBrush x:Key=\"Theme(?:Accent|Card|Header)Gradient\".*?</LinearGradientBrush>",
                         System.Text.RegularExpressions.RegexOptions.Singleline))
            {
                foreach (System.Text.RegularExpressions.Match color in System.Text.RegularExpressions.Regex.Matches(
                             gradient.Value,
                             "Color=\"#(?<hex>[0-9A-Fa-f]{6,8})\""))
                    AssertNeutralColor(name, "gradient", color.Groups["hex"].Value);
            }
        }
    }

    [Fact]
    public void ViewSurfacesUseSharedThemeTokensInsteadOfLegacyFluentBrushes()
    {
        var viewsDirectory = Path.Combine(FindRepoRoot(), "PKHeX.Avalonia", "Views");
        foreach (var path in Directory.EnumerateFiles(viewsDirectory, "*.axaml"))
        {
            var view = File.ReadAllText(path);
            Assert.DoesNotContain("SystemControl", view);
            Assert.DoesNotContain("ThemeBorderBrush", view);
            Assert.DoesNotContain("ThemeAccentBrush", view);
        }
    }

    [AvaloniaFact]
    public void MainWindow_At1024x720PreservesEditorAndWorkspaceWorkingWidths()
    {
        using var app = new HeadlessAppFixture();
        app.Window.Width = 1024;
        app.Window.Height = 720;
        var savePath = Path.Combine(Path.DirectorySeparatorChar.ToString(), "Saves", "pokemon-x-main");
        app.LoadSaveInstance(new SAV6XY(), savePath);
        app.Pump();

        var editor = app.FindByName<Border>("EditorPane");
        var workspace = app.FindByName<TabControl>("WorkspacePane");

        Assert.NotNull(editor);
        Assert.NotNull(workspace);
        Assert.InRange(editor!.Bounds.Width, 320, 440);
        Assert.True(workspace!.Bounds.Width >= 440, $"Workspace was only {workspace.Bounds.Width}px wide.");
        Assert.Equal("pokemon-x-main", app.ViewModel.CurrentSaveFileName);
        Assert.Equal(savePath, app.ViewModel.CurrentSavePath);
    }

    [AvaloniaFact]
    public void SaveWorkspace_ReclaimsEditorRailAtMinimumShellSize()
    {
        using var app = new HeadlessAppFixture();
        app.Window.Width = 1024;
        app.Window.Height = 720;
        app.LoadSaveInstance(new SAV6XY());

        app.ViewModel.ActiveWorkspace = MainWorkspace.Save;
        app.Pump();

        var editor = app.FindByName<Border>("EditorPane");
        var workspace = app.FindByName<TabControl>("WorkspacePane");
        Assert.NotNull(editor);
        Assert.NotNull(workspace);
        Assert.False(editor!.IsVisible);
        Assert.True(workspace!.Bounds.Width >= 760, $"Save workspace was only {workspace.Bounds.Width}px wide.");
        Assert.Equal(2, app.ViewModel.SelectedWorkspaceIndex);
    }

    [AvaloniaFact]
    public void InventoryWorkspace_UsesCompactPouchTabsAndCenteredTabContent()
    {
        using var app = new HeadlessAppFixture();
        app.LoadSaveInstance(new SAV9SV());
        app.ViewModel.ActiveWorkspace = MainWorkspace.Save;
        app.ViewModel.SelectedWorkspaceIndex = 3;
        app.Pump();

        var pouchTabs = app.Window.GetVisualDescendants()
            .OfType<TabControl>()
            .Single(control => control.Classes.Contains("pouch-tabs"));
        var visibleTabs = pouchTabs.GetVisualDescendants()
            .OfType<TabItem>()
            .Where(item => item.IsVisible)
            .ToList();

        Assert.NotEmpty(visibleTabs);
        Assert.All(visibleTabs, tab =>
        {
            Assert.Equal(13, tab.FontSize);
            Assert.Equal(VerticalAlignment.Center, tab.VerticalContentAlignment);
            Assert.InRange(tab.Bounds.Height, 30, 38);
        });
    }

    [AvaloniaFact]
    public void ReportsWorkspace_AndToolLauncherHaveRealVisibleSurfaces()
    {
        using var app = new HeadlessAppFixture();
        app.LoadSaveInstance(new SAV6XY());

        app.ViewModel.ActiveWorkspace = MainWorkspace.Reports;
        app.Pump();

        var reports = app.Window.GetVisualDescendants()
            .OfType<Border>()
            .Single(border => border.Classes.Contains("reports-surface"));
        Assert.True(reports.IsVisible);

        app.ViewModel.OpenToolLauncherCommand.Execute(null);
        app.Pump();

        var launcher = app.Window.GetVisualDescendants()
            .OfType<Border>()
            .Single(border => border.Classes.Contains("launcher-panel"));
        Assert.True(launcher.IsVisible);
        Assert.True(app.ViewModel.IsToolLauncherOpen);

        var reportCapabilities = app.ViewModel.ReportToolItems.ToList();
        Assert.True(reportCapabilities.Count == 5, $"Expected five report capabilities, found {reportCapabilities.Count}.");
        Assert.All(reportCapabilities, item =>
        {
            Assert.Contains(item, app.ViewModel.ToolLauncherItems);
            Assert.False(string.IsNullOrWhiteSpace(item.IconData));
        });

        app.ViewModel.ToolSearchText = "batch";
        var matches = app.ViewModel.FilteredToolLauncherItems.ToList();
        Assert.Single(matches);
        Assert.Equal("Batch Editor", matches[0].Title);
    }

    [AvaloniaFact]
    public void ReportsWorkspace_UsesCompactThreeColumnTilesWithAlignedActions()
    {
        using var app = new HeadlessAppFixture();
        app.Window.Width = 1024;
        app.Window.Height = 720;
        app.LoadSaveInstance(new SAV6XY());
        app.ViewModel.ActiveWorkspace = MainWorkspace.Reports;
        app.Pump();

        var cards = app.Window.GetVisualDescendants()
            .OfType<Button>()
            .Where(button => button.Classes.Contains("report-tool-card") && button.IsVisible)
            .ToList();
        var arrows = app.Window.GetVisualDescendants()
            .OfType<PathIcon>()
            .Where(icon => icon.Classes.Contains("report-tool-arrow") && icon.IsVisible)
            .ToList();

        Assert.Equal(5, cards.Count);
        Assert.Equal(5, arrows.Count);
        Assert.Equal(3, cards.Take(3)
            .Select(card => card.TranslatePoint(new Point(0, 0), app.Window)?.X)
            .Distinct()
            .Count());
        Assert.All(cards, card =>
        {
            Assert.InRange(card.Bounds.Width, 220, 310);
            Assert.InRange(card.Bounds.Height, 70, 90);
            Assert.Equal(VerticalAlignment.Center, card.VerticalContentAlignment);
        });
    }

    [Fact]
    public void AppearanceAccent_IsStaticAndAchromaticAcrossViewStyles()
    {
        var app = ReadSourceFile("App.axaml");
        Assert.Contains("PkhexNeutralAccentBrush", app);
        Assert.Contains("Color=\"#707070\"", app);

        var avaloniaDirectory = Path.Combine(FindRepoRoot(), "PKHeX.Avalonia");
        foreach (var path in Directory.EnumerateFiles(avaloniaDirectory, "*.axaml", SearchOption.AllDirectories))
        {
            var source = File.ReadAllText(path);
            Assert.DoesNotContain("DynamicResource ThemeAccentPrimaryBrush", source);
            Assert.DoesNotContain("DynamicResource ThemeAccentSecondaryBrush", source);
            Assert.DoesNotContain("DynamicResource ThemeAccentGlowBrush", source);
        }
    }

    [AvaloniaFact]
    public void ToolsMenu_UsesEveryRegisteredCapabilityOnce()
    {
        using var app = new HeadlessAppFixture();
        app.LoadSaveInstance(new SAV6XY());

        var menuEntries = app.ViewModel.ToolMenuGroups
            .SelectMany(group => group.Items)
            .ToList();

        Assert.True(menuEntries.Count == app.ViewModel.ToolLauncherItems.Count,
            $"Expected every capability to appear in one menu group, found {menuEntries.Count} of {app.ViewModel.ToolLauncherItems.Count}.");
        Assert.All(app.ViewModel.ToolLauncherItems, item => Assert.Contains(item, menuEntries));
        Assert.Contains(app.ViewModel.ToolMenuGroups, group =>
            group.IsAvailable && group.Title == LocalizedStrings.Instance["Menu_Gen6"]);
        Assert.DoesNotContain(app.ViewModel.ToolMenuGroups, group =>
            group.IsAvailable && group.Title == LocalizedStrings.Instance["Menu_Gen1"]);
    }

    [AvaloniaFact]
    public void SaveWorkspace_HidesUnsupportedZaEventAndGiftTabs()
    {
        using var app = new HeadlessAppFixture();
        app.LoadSaveInstance(new SAV9ZA());

        Assert.False(app.ViewModel.IsEventsWorkspace);
        Assert.False(app.ViewModel.IsGiftsWorkspace);

        app.ViewModel.ActiveWorkspace = MainWorkspace.Save;
        app.Pump();

        var tabs = app.Window.GetVisualDescendants().OfType<TabItem>().ToList();
        var events = tabs.Single(tab => Equals(tab.Header, LocalizedStrings.Instance["Tab_Events"]));
        var gifts = tabs.Single(tab => Equals(tab.Header, LocalizedStrings.Instance["Tab_Gifts"]));
        Assert.False(events.IsVisible);
        Assert.False(gifts.IsVisible);
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
            var parent = Directory.GetParent(dir)
                ?? throw new DirectoryNotFoundException("Could not find repository root");
            dir = parent.FullName;
        }

        return dir;
    }

    private static void AssertNeutralColor(string theme, string token, string hex)
    {
        var red = Convert.ToInt32(hex[..2], 16);
        var green = Convert.ToInt32(hex[2..4], 16);
        var blue = Convert.ToInt32(hex[4..6], 16);
        var spread = Math.Max(red, Math.Max(green, blue)) - Math.Min(red, Math.Min(green, blue));
        Assert.True(spread == 0, $"{theme} token {token} is tinted ({hex}).");
    }
}
