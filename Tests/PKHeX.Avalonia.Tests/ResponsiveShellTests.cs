using Avalonia.Controls;
using Avalonia;
using Avalonia.Automation;
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
        var controlSystem = ReadSourceFile("Styles", "ControlSystem.axaml");

        Assert.Contains("Width=\"900\" Height=\"600\"", mainWindow);
        Assert.Contains("MinWidth=\"900\" MinHeight=\"600\"", mainWindow);
        Assert.Contains("Width=\"{DynamicResource CompactShellEditorWidth}\" MinWidth=\"300\" MaxWidth=\"360\"", mainWindow);
        Assert.Contains("x:Name=\"EditorPane\"", mainWindow);
        Assert.Contains("x:Name=\"WorkspacePane\"", mainWindow);
        Assert.Contains("MinHeight=\"49\"", mainWindow);
        Assert.DoesNotContain("Classes=\"workspace-rail\"", mainWindow);
        Assert.DoesNotContain("Classes=\"app-header\"", mainWindow);
        Assert.Contains("Classes=\"reports-surface\"", mainWindow);
        Assert.Contains("Classes=\"reports-layout\"", mainWindow);
        Assert.Contains("<UniformGrid Columns=\"3\" />", mainWindow);
        Assert.Contains("SelectedIndex=\"{Binding SelectedWorkspaceIndex, Mode=TwoWay}\"", mainWindow);
        Assert.Contains("Classes=\"pane-splitter shell-divider\"", mainWindow);
        Assert.Contains("Classes=\"workspace-tabs\"", mainWindow);
        Assert.Contains("ItemsSource=\"{Binding AvailableToolMenuGroups}\"", mainWindow);
        Assert.Contains("MenuItem.ItemContainerTheme", mainWindow);
        Assert.Contains("BasedOn=\"{StaticResource {x:Type MenuItem}}\"", mainWindow);
        Assert.DoesNotContain("MenuItem.ItemTemplate", mainWindow);
        Assert.Contains("Classes=\"topbar-control\"", mainWindow);
        Assert.Contains("ColumnDefinitions=\"*,Auto\"", mainWindow);
        Assert.DoesNotContain("Assets/Icons/icon.png\" Width=\"22\" Height=\"22\"", mainWindow);
        Assert.DoesNotContain("<MenuItem Header=\"{loc:Loc Menu_Pokemon}\"", mainWindow);
        Assert.DoesNotContain("<MenuItem Header=\"{loc:Loc Menu_Gen1}\"", mainWindow);
        Assert.DoesNotContain("ThemeAccentBrush\"", mainWindow);
        Assert.Equal(7, System.Text.RegularExpressions.Regex.Matches(mainWindow, "Classes=\"workspace-tab\"").Count);

        Assert.Contains("TabControl.editor-tabs TabItem:selected", theme);
        Assert.Contains("TabControl.editor-tabs TabItem:selected /template/ Border#PART_SelectedPipe", theme);
        Assert.Contains("TabControl.workspace-tabs TabItem.workspace-tab:selected /template/ Border#PART_SelectedPipe", theme);
        Assert.Contains("BorderThickness\" Value=\"0\"", theme);
        Assert.Contains("TabControl.workspace-tabs TabItem.workspace-tab:selected", theme);
        Assert.Contains("TabControl.editor-tabs TabItem:focus-visible", theme);
        Assert.Contains("TabControl.workspace-tabs TabItem.workspace-tab:focus-visible", theme);
        Assert.Contains("MenuItem:pointerover /template/ Border#PART_LayoutRoot", theme);
        Assert.Contains("MenuItem:selected /template/ Border#PART_LayoutRoot", theme);
        Assert.Contains("MenuItem:selected:pointerover /template/ Border#PART_LayoutRoot", theme);
        Assert.Contains("<Color x:Key=\"ThemeControlFocusGlow\">#33F1A1A9</Color>", theme);
        Assert.Contains("<Color x:Key=\"ThemeControlFocusGlow\">#22913F4A</Color>", theme);
        Assert.Contains("<Color x:Key=\"ThemeControlFocusGlow\">#66FF4500</Color>", theme);
        Assert.Contains("<SolidColorBrush x:Key=\"ComboBoxBackgroundUnfocused\"", theme);
        Assert.Contains("<SolidColorBrush x:Key=\"ComboBoxBackgroundBorderBrushUnfocused\"", theme);
        Assert.Contains("ComboBox:focus-visible /template/ Border#HighlightBackground", controlSystem);
        Assert.Contains("<Setter Property=\"IsVisible\" Value=\"False\" />", controlSystem);
        var selectedRailStyle = System.Text.RegularExpressions.Regex.Match(
            theme,
            "<Style Selector=\"ToggleButton\\.workspace-nav-item:checked\">(?<body>.*?)</Style>",
            System.Text.RegularExpressions.RegexOptions.Singleline);
        Assert.True(selectedRailStyle.Success);
        Assert.Contains("Background\" Value=\"Transparent\"", selectedRailStyle.Groups["body"].Value);
        Assert.Contains("BorderThickness\" Value=\"1\"", selectedRailStyle.Groups["body"].Value);
        Assert.DoesNotContain("ThemeBackgroundElevatedBrush", selectedRailStyle.Groups["body"].Value);
        Assert.Contains("ToggleButton.workspace-nav-item:checked /template/ ContentPresenter#PART_ContentPresenter", theme);
        Assert.Contains("Border.pokedex-species-list ListBoxItem:selected /template/ ContentPresenter#PART_ContentPresenter", theme);
        Assert.Contains("Button.auxiliary-list-item /template/ Border#PART_Border", theme);
        var selectedEditorStyle = System.Text.RegularExpressions.Regex.Match(
            theme,
            "<Style Selector=\"TabControl\\.editor-tabs TabItem:selected\">(?<body>.*?)</Style>",
            System.Text.RegularExpressions.RegexOptions.Singleline);
        Assert.True(selectedEditorStyle.Success);
        Assert.DoesNotContain("ThemeAccentGlowBrush", selectedEditorStyle.Groups["body"].Value);
        Assert.Contains("Background\" Value=\"Transparent\"", selectedEditorStyle.Groups["body"].Value);

        // Save identity is intentionally a single piece of application chrome: the top header owns
        // the filename, while the left rail and bottom status bar carry only task/status context.
        Assert.Single(System.Text.RegularExpressions.Regex.Matches(mainWindow, "ToolTip.Tip=\"\\{Binding CurrentSaveFileName\\}\""));
        Assert.DoesNotContain("rail-save-card", mainWindow);
        Assert.DoesNotContain("rail-status", mainWindow);
        var statusBar = System.Text.RegularExpressions.Regex.Match(
            mainWindow,
            "<Border DockPanel.Dock=\"Bottom\"(?<body>.*?)</Border>",
            System.Text.RegularExpressions.RegexOptions.Singleline);
        Assert.True(statusBar.Success);
        Assert.DoesNotContain("CurrentSaveFileName", statusBar.Groups["body"].Value);
    }

    [AvaloniaFact]
    public void SaveAndWindowMenus_OpenWithoutThrowing()
    {
        using var app = new HeadlessAppFixture();
        app.LoadSaveInstance(new SAV6XY());

        foreach (var header in new[] { LocalizedStrings.Instance["Menu_Save"], LocalizedStrings.Instance["Menu_Window"] })
        {
            var menu = app.Window.GetVisualDescendants()
                .OfType<MenuItem>()
                .Single(item => Equals(item.Header, header));

            menu.IsSubMenuOpen = true;
            app.Pump();
            menu.IsSubMenuOpen = false;
            app.Pump();
        }
    }

    [AvaloniaFact]
    public void TopBarSettingsAndThemeShareHeightAndTypography()
    {
        using var app = new HeadlessAppFixture();
        app.LoadSaveInstance(new SAV6XY());

        var settings = app.Window.GetVisualDescendants()
            .OfType<Button>()
            .Single(button => Equals(button.Content, LocalizedStrings.Instance["Settings_Title"]));
        var theme = app.Window.GetVisualDescendants()
            .OfType<ComboBox>()
            .Single(combo => AutomationProperties.GetName(combo) == LocalizedStrings.Instance["Settings_Theme"]);

        Assert.Equal(settings.Bounds.Height, theme.Bounds.Height);
        Assert.Equal(settings.FontSize, theme.FontSize);
    }

    [AvaloniaFact]
    public void ToolsMenu_ClosesPreviousDynamicSubmenuWhenSiblingOpens()
    {
        using var app = new HeadlessAppFixture();
        app.LoadSaveInstance(new SAV6XY());
        var tools = app.Window.GetVisualDescendants().OfType<MenuItem>()
            .Single(item => Equals(item.Header, LocalizedStrings.Instance["Menu_Tools"]));
        tools.IsSubMenuOpen = true;
        app.Pump();

        var items = app.Window.GetVisualDescendants().OfType<MenuItem>()
            .Where(item => item.Header is "Data" or "Pokémon" or "WORKSPACES")
            .ToArray();
        var data = items.Single(item => Equals(item.Header, "Data"));
        var pokemon = items.Single(item => Equals(item.Header, "Pokémon"));
        data.IsSubMenuOpen = true;
        app.Pump();
        pokemon.IsSubMenuOpen = true;
        app.Pump();
        Assert.False(data.IsSubMenuOpen);
        Assert.True(pokemon.IsSubMenuOpen);

        var window = app.Window.GetVisualDescendants().OfType<MenuItem>()
            .Single(item => Equals(item.Header, LocalizedStrings.Instance["Menu_Window"]));
        window.IsSubMenuOpen = true;
        app.Pump();
        Assert.False(tools.IsSubMenuOpen);
        Assert.True(window.IsSubMenuOpen);
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
            var expectedCanvas = name == "Dark" ? "191B22" : "F7F7F9";
            var expectedPane = name == "Dark" ? "22252E" : "FFFFFF";
            var expectedInput = name == "Dark" ? "292D37" : "FBFBFC";
            var expectedAccent = name == "Dark" ? "B05763" : "AD4D53";
            var expectedOnAccent = "FFFFFF";
            var expectedAccentText = name == "Dark" ? "D98B95" : "913F4A";
            var expectedSelection = name == "Dark" ? "403039" : "F5E8EA";
            var expectedSelectionBorder = name == "Dark" ? "C4717B" : "B66A73";
            var expectedFocus = name == "Dark" ? "F1A1A9" : "913F4A";

            Assert.Equal(expectedCanvas, GetThemeHex(body, "ThemeBackgroundBase"));
            Assert.Equal(expectedPane, GetThemeHex(body, "ThemeBackgroundCard"));
            Assert.Equal(expectedInput, GetThemeHex(body, "ThemeControlBackground"));
            Assert.Equal(expectedAccent, GetBrushHex(body, "CompactAccentBrush"));
            Assert.Equal(expectedOnAccent, GetBrushHex(body, "CompactOnAccentBrush"));
            Assert.Equal(expectedAccentText, GetBrushHex(body, "CompactAccentTextBrush"));
            Assert.Equal(expectedSelection, GetBrushHex(body, "CompactSelectionBrush"));
            Assert.Equal(expectedSelectionBorder, GetBrushHex(body, "CompactSelectionBorderBrush"));
            Assert.Equal(expectedFocus, GetBrushHex(body, "CompactFocusBrush"));

            Assert.True(ContrastRatio(GetThemeHex(body, "ThemeTextPrimary"), expectedCanvas) >= 4.5,
                $"{name} primary text must meet normal-text contrast on the canvas.");
            Assert.True(ContrastRatio(GetThemeHex(body, "ThemeTextMuted"), expectedPane) >= 4.5,
                $"{name} muted text must meet normal-text contrast on a pane.");
            Assert.True(ContrastRatio(expectedOnAccent, expectedAccent) >= 4.5,
                $"{name} action text must meet normal-text contrast on its action background.");
            Assert.True(ContrastRatio(expectedAccentText, expectedPane) >= 4.5,
                $"{name} accent text must meet normal-text contrast on a pane.");
            Assert.True(ContrastRatio(expectedFocus, expectedInput) >= 3,
                $"{name} focus boundary must meet non-text contrast against an input.");
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
            Assert.DoesNotContain("Foreground=\"Gray\"", view);
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
        Assert.InRange(editor!.Bounds.Width, 300, 360);
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
            Assert.InRange(card.Bounds.Width, 220, 360);
            Assert.InRange(card.Bounds.Height, 70, 90);
            Assert.Equal(VerticalAlignment.Center, card.VerticalContentAlignment);
        });
    }

    [Fact]
    public void AppearanceAccent_IsStaticAndAchromaticAcrossViewStyles()
    {
        var theme = ReadSourceFile("Styles", "Theme.axaml");
        var controls = ReadSourceFile("Styles", "ControlSystem.axaml");

        Assert.Contains("CompactAccentBrush", theme);
        Assert.Contains("CompactOnAccentBrush", theme);
        Assert.Contains("CompactAccentTextBrush", theme);
        Assert.Contains("CompactSelectionBrush", theme);
        Assert.Contains("CompactSelectionBorderBrush", theme);
        Assert.Contains("CompactFocusBrush", theme);
        Assert.DoesNotContain("StaticResource PkhexNeutral", theme);
        Assert.DoesNotContain("StaticResource PkhexNeutral", controls);
        Assert.Contains("Button.compact-primary", theme);
        Assert.Contains("Button.compact-secondary", theme);
        Assert.Contains("TabControl.compact-editor-tabs", theme);
        Assert.Contains("Button.compact-slot", theme);
        Assert.Contains("Window.compact-settings", theme);
        Assert.Contains("Border.compact-party-strip", theme);
    }

    [Fact]
    public void AuxiliaryEditors_UseFlatSectionsAndOneActionSurface()
    {
        var views = new[]
        {
            "BatchEditor.axaml",
            "EventFlagsEditor.axaml",
            "MysteryGiftEditor.axaml",
            "Misc3Editor.axaml",
            "Misc4Editor.axaml",
            "Misc9Editor.axaml",
        };

        foreach (var view in views)
        {
            var source = ReadSourceFile("Views", view);
            Assert.Contains("Classes=\"editor-section\"", source);
            Assert.DoesNotContain("Classes=\"section-card\"", source);
            Assert.DoesNotContain("<!-- Bottom Buttons -->", source);
            Assert.Contains("Classes=\"save-editor-header\"", source);
        }

        Assert.Contains("Classes=\"auxiliary-list-item\"", ReadSourceFile("Views", "MysteryGiftEditor.axaml"));
    }

    [Fact]
    public void DatabaseTools_UseFullWidthFilterResultsAndNativeGridSurfaces()
    {
        var gridViews = new[]
        {
            "PKMDatabaseView.axaml",
            "MysteryGiftDatabaseView.axaml",
            "BoxReportView.axaml",
            "LegalityAuditView.axaml",
        };

        foreach (var view in gridViews)
        {
            var source = ReadSourceFile("Views", view);
            Assert.Contains("Classes=\"tool-surface\"", source);
            Assert.DoesNotContain("Classes=\"section-card\"", source);
            Assert.Contains("database-grid", source);
        }

        var encounterDatabase = ReadSourceFile("Views", "EncounterDatabaseView.axaml");
        Assert.Contains("Classes=\"tool-surface\"", encounterDatabase);
        Assert.Contains("Classes=\"database-results-surface\"", encounterDatabase);
        Assert.Contains("Classes=\"tool-result-card\"", encounterDatabase);
        Assert.DoesNotContain("Classes=\"section-card\"", encounterDatabase);

        var pkmDatabase = ReadSourceFile("Views", "PKMDatabaseView.axaml");
        Assert.DoesNotContain("Width=\"900\" Height=\"600\"", pkmDatabase);
        Assert.Contains("Classes=\"database-filter-pane\"", pkmDatabase);
        Assert.Contains("Classes=\"database-results-pane\"", pkmDatabase);
        Assert.Contains("Width=\"80\"", pkmDatabase);
    }

    [Fact]
    public void PokedexEditors_UseFlatDetailSurfacesAndTopActions()
    {
        var views = new[]
        {
            "Pokedex4Editor.axaml",
            "Pokedex5Editor.axaml",
            "Pokedex6Editor.axaml",
            "Pokedex7Editor.axaml",
            "Pokedex7bEditor.axaml",
            "Pokedex8Editor.axaml",
            "Pokedex8bEditor.axaml",
            "PokedexGen9Editor.axaml",
            "PokedexLAEditor.axaml",
            "PokedexSimpleEditor.axaml",
        };

        foreach (var view in views)
        {
            var source = ReadSourceFile("Views", view);
            Assert.DoesNotContain("section-card", source);
            Assert.DoesNotContain("Bottom Buttons", source);
            Assert.Contains("pokedex-species-list", source);
            Assert.DoesNotContain("ComboBoxItem>{loc:Loc", source);
        }

        Assert.DoesNotContain("RowDefinitions=\"*,Auto\"", ReadSourceFile("Views", "PokedexGen9Editor.axaml"));
        Assert.Contains("Classes=\"save-editor-header\"", ReadSourceFile("Views", "PokedexGen9Editor.axaml"));
        Assert.Contains("Classes=\"save-editor-header\"", ReadSourceFile("Views", "Pokedex7bEditor.axaml"));
    }

    [Fact]
    public void LegacyMiscEditors_UseFlatSectionsAndConsistentTabRails()
    {
        var views = new[]
        {
            "Misc2Editor.axaml",
            "Misc5Editor.axaml",
            "Misc7Editor.axaml",
            "Misc7bEditor.axaml",
            "Misc8Editor.axaml",
            "Misc8bEditor.axaml",
        };

        foreach (var view in views)
        {
            var source = ReadSourceFile("Views", view);
            Assert.DoesNotContain("section-card", source);
            Assert.DoesNotContain("Bottom Buttons", source);
            Assert.Contains("Classes=\"editor-section\"", source);
            Assert.Contains("Classes=\"save-editor-header\"", source);
        }

        foreach (var view in new[] { "Misc5Editor.axaml", "Misc7Editor.axaml" })
        {
            var source = ReadSourceFile("Views", view);
            Assert.DoesNotContain("<TabItem Header=", source);
            Assert.Contains("Classes=\"editor-tabs\"", source);
        }

        var misc7 = ReadSourceFile("Views", "Misc7Editor.axaml");
        Assert.Contains("Grid.Row=\"2\" Grid.Column=\"0\" Text=\"{loc:Loc Misc7Editor_Single}", misc7);
        Assert.DoesNotContain("Grid.Row=\"1\" Grid.Column=\"0\" Text=\"{loc:Loc Misc7Editor_Single}", misc7);
    }

    [Fact]
    public void ComplexEditors_UseSharedToolHeadersFlatSectionsAndNativeGrids()
    {
        var views = new[]
        {
            "PokeathlonEditor.axaml",
            "JoinAvenueEditor.axaml",
            "GlobalLink5Editor.axaml",
            "MedalEditorView.axaml",
            "FashionEditorView.axaml",
            "DonutEditor.axaml",
        };

        foreach (var view in views)
        {
            var source = ReadSourceFile("Views", view);
            Assert.DoesNotContain("section-card", source);
            Assert.Contains("editor-section-padded", source);
        }

        foreach (var view in new[] { "PokeathlonEditor.axaml", "JoinAvenueEditor.axaml", "GlobalLink5Editor.axaml", "MedalEditorView.axaml" })
        {
            var source = ReadSourceFile("Views", view);
            Assert.Contains("Classes=\"editor-tabs\"", source);
            Assert.Contains("Classes=\"editor-tab\"", source);
            Assert.Contains("Classes=\"save-grid\"", source);
            Assert.Contains("Classes=\"tool-header-stack\"", source);
        }

        Assert.Contains("Classes=\"save-editor-header\"", ReadSourceFile("Views", "FashionEditorView.axaml"));
        Assert.Contains("Classes=\"tool-surface\"", ReadSourceFile("Views", "PokeathlonEditor.axaml"));
    }

    [Fact]
    public void RemainingDialogViews_UseFlatPaddedSectionsAndNativeListSelection()
    {
        var views = new[]
        {
            "ApricornEditor.axaml",
            "BoxLayoutEditor.axaml",
            "BoxManipView.axaml",
            "Capture7GGEditor.axaml",
            "ChatterEditor.axaml",
            "DaycareEditorView.axaml",
            "FestivalPlazaEditor.axaml",
            "Geonet4Editor.axaml",
            "HallOfFame1Editor.axaml",
            "HallOfFame3EditorView.axaml",
            "HallOfFame7Editor.axaml",
            "HallOfFameEditor.axaml",
            "HoneyTreeEditor.axaml",
            "OPowerEditor.axaml",
            "PartyViewer.axaml",
            "PokeBlockEditor.axaml",
            "PokeGear4Editor.axaml",
            "PokebeanEditor.axaml",
            "PoketchEditorView.axaml",
            "RTC3Editor.axaml",
            "RTCEditor.axaml",
            "Raid9Editor.axaml",
            "RaidEditor.axaml",
            "RaidSevenStar9Editor.axaml",
            "RecordsEditorView.axaml",
            "RibbonEditor.axaml",
            "Roamer3Editor.axaml",
            "Roamer6Editor.axaml",
            "RoamerEditor.axaml",
            "SaveHandlerTroubleshooter.axaml",
            "SecretBaseEditor.axaml",
            "TrainerCard8EditorView.axaml",
            "UndergroundEditor.axaml",
            "UnityTower5Editor.axaml",
            "UpdateChangelogView.axaml",
            "ZygardeCellEditor.axaml",
        };

        foreach (var view in views)
        {
            var source = ReadSourceFile("Views", view);
            Assert.DoesNotContain("section-card", source);
            Assert.Contains("editor-section-padded", source);
        }

        var settings = ReadSourceFile("Views", "SettingsView.axaml");
        Assert.Contains("RowDefinitions=\"70,*,54\"", settings);
        Assert.Contains("Command=\"{Binding SaveCommand}\"", settings);

        var theme = ReadSourceFile("Styles", "Theme.axaml");
        Assert.Contains("ListBoxItem:selected /template/ ContentPresenter#PART_ContentPresenter", theme);
        Assert.Contains("Background\" Value=\"Transparent\"", theme);
        Assert.Contains("BorderBrush\" Value=\"{DynamicResource CompactAccentTextBrush}\"", theme);
    }

    [Fact]
    public void LegacyUtilityViews_UseSharedNeutralSurfacesAndNativeGridChrome()
    {
        var views = new[]
        {
            "AccessorEditor.axaml",
            "BoxListEditor.axaml",
            "DatabaseEditor.axaml",
            "EncountersEditor.axaml",
            "EventWorkEditor.axaml",
            "FolderList.axaml",
            "GearBREditor.axaml",
            "KChart.axaml",
            "Link6Editor.axaml",
            "Misc8aEditor.axaml",
            "MoveShopEditor.axaml",
            "SealStickers8bEditor.axaml",
            "SecretBase3Editor.axaml",
            "SecretBase6Editor.axaml",
            "SimpleTrainerEditor.axaml",
            "TechRecordEditor.axaml",
            "Underground8bEditor.axaml",
        };

        foreach (var view in views)
        {
            var source = ReadSourceFile("Views", view);
            Assert.Contains("view-container", source);
            Assert.DoesNotContain("BorderBrush=\"Gray\"", source);
            Assert.DoesNotContain("GridLinesVisibility=\"All\"", source);
        }

        foreach (var view in new[] { "FolderList.axaml", "KChart.axaml", "MoveShopEditor.axaml", "SealStickers8bEditor.axaml", "TechRecordEditor.axaml", "Underground8bEditor.axaml" })
            Assert.Contains("Classes=\"save-grid\"", ReadSourceFile("Views", view));

        foreach (var view in new[] { "MoveShopEditor.axaml", "SealStickers8bEditor.axaml", "TechRecordEditor.axaml", "Underground8bEditor.axaml" })
            Assert.Contains("Common_Save", ReadSourceFile("Views", view));

        foreach (var view in new[] { "EventWorkEditor.axaml", "GearBREditor.axaml", "Link6Editor.axaml", "Misc8aEditor.axaml", "SecretBase6Editor.axaml", "SimpleTrainerEditor.axaml" })
            Assert.Contains("editor-section-padded", ReadSourceFile("Views", view));

        Assert.Contains("database-filter-pane", ReadSourceFile("Views", "SecretBase3Editor.axaml"));
    }

    [Fact]
    public void RemainingUtilityDialogs_UseFlatNativeSurfacesAndTopActions()
    {
        var views = new[]
        {
            "BackupManager.axaml",
            "BlockEditor.axaml",
            "EventFlags2Editor.axaml",
            "EventReset1Editor.axaml",
            "Fashion9Editor.axaml",
            "GroupViewer.axaml",
            "LegalityView.axaml",
            "MailBoxEditor.axaml",
            "PokepuffEditor.axaml",
            "SaveDiffView.axaml",
            "TrashEditor.axaml",
        };

        foreach (var view in views)
        {
            var source = ReadSourceFile("Views", view);
            Assert.Contains("view-container", source);
            Assert.DoesNotContain("BorderBrush=\"Gray\"", source);
            Assert.DoesNotContain("BorderBrush=\"LightGray\"", source);
            Assert.DoesNotContain("GridLinesVisibility=\"All\"", source);
        }

        foreach (var view in new[] { "BackupManager.axaml", "BlockEditor.axaml", "EventFlags2Editor.axaml", "Fashion9Editor.axaml", "SaveDiffView.axaml" })
            Assert.Contains("Classes=\"save-grid\"", ReadSourceFile("Views", view));

        var theme = ReadSourceFile("Styles", "Theme.axaml");
        Assert.DoesNotContain("Border.section-card", theme);
        Assert.Contains("DataGrid.save-grid DataGridRow:selected /template/ Rectangle#BackgroundRectangle", theme);
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

    private static string GetThemeHex(string dictionary, string key)
    {
        var match = System.Text.RegularExpressions.Regex.Match(
            dictionary,
            $"<Color x:Key=\"{System.Text.RegularExpressions.Regex.Escape(key)}\">#(?<hex>[0-9A-Fa-f]{{6}})</Color>");
        Assert.True(match.Success, $"Theme color '{key}' was not declared.");
        return match.Groups["hex"].Value.ToUpperInvariant();
    }

    private static string GetBrushHex(string dictionary, string key)
    {
        var match = System.Text.RegularExpressions.Regex.Match(
            dictionary,
            $"<SolidColorBrush x:Key=\"{System.Text.RegularExpressions.Regex.Escape(key)}\" Color=\"#(?<hex>[0-9A-Fa-f]{{6}})\" />");
        Assert.True(match.Success, $"Theme brush '{key}' was not declared.");
        return match.Groups["hex"].Value.ToUpperInvariant();
    }

    private static double ContrastRatio(string foreground, string background)
    {
        var foregroundLuminance = RelativeLuminance(foreground);
        var backgroundLuminance = RelativeLuminance(background);
        var lighter = Math.Max(foregroundLuminance, backgroundLuminance);
        var darker = Math.Min(foregroundLuminance, backgroundLuminance);
        return (lighter + 0.05) / (darker + 0.05);
    }

    private static double RelativeLuminance(string hex)
    {
        var red = Convert.ToInt32(hex[..2], 16) / 255d;
        var green = Convert.ToInt32(hex[2..4], 16) / 255d;
        var blue = Convert.ToInt32(hex[4..6], 16) / 255d;
        static double Linearize(double value) => value <= 0.03928
            ? value / 12.92
            : Math.Pow((value + 0.055) / 1.055, 2.4);
        return 0.2126 * Linearize(red) + 0.7152 * Linearize(green) + 0.0722 * Linearize(blue);
    }
}
