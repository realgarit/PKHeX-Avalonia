using System;
using System.IO;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using PKHeX.Application.Abstractions;
using PKHeX.Avalonia.Controls;
using PKHeX.Avalonia.Views;
using PKHeX.Avalonia.Tests.Fixtures;
using PKHeX.Core;
using PKHeX.Infrastructure.GiftRecords;
using PKHeX.Presentation.Localization;
using PKHeX.Presentation.ViewModels;
using Xunit;
using Xunit.Abstractions;

namespace PKHeX.Avalonia.Tests.Harness;

/// <summary>
/// Opt-in visual-evidence captures of feature states, mirroring the headless capture pattern in
/// <see cref="HeadlessGiftRecordTests"/>. These write a PNG of a real editor view and are skipped
/// unless the process was started with <c>PKHEX_HEADLESS_CAPTURE=1</c> and the Skia headless app
/// builder (frames are only meaningful when drawing is enabled; see Harness/README.md).
/// </summary>
public sealed class HeadlessFeatureCaptureTests(ITestOutputHelper output)
{
    [AvaloniaFact]
    public void CapturePokeRadar_Misc4Editor_WhenEnabled_WritesPng()
    {
        if (SkipWhenCaptureDisabled())
            return;

        // Poke Radar is a Gen 4 Platinum key item toggled from the Misc editor's checkbox.
        var sav = new SAV4Pt();
        var vm = new Misc4EditorViewModel(sav);
        vm.PokeRadar = true;
        vm.SaveCommand.Execute(null);

        var view = new Misc4Editor { DataContext = vm };
        var window = new Window { Content = view, Width = 520, Height = 400 };
        window.Show();
        PumpToStableLayout(window);

        var path = Path.Combine(CaptureDirectory(), "poke-radar.png");
        var saved = CaptureWindow(window, path);
        if (saved is null)
        {
            output.WriteLine("Skipped: headless drawing mode produced no frame.");
            return;
        }

        Assert.Equal(path, saved);
        Assert.True(File.Exists(path));
        Assert.True(new FileInfo(path).Length > 0);
        output.WriteLine($"Saved Pt Poke Radar editor screenshot to {path}");
    }

    [AvaloniaFact]
    public void CaptureHyperTrainingAndPokerus_PokemonEditor_WhenEnabled_WritesPng()
    {
        if (SkipWhenCaptureDisabled())
            return;

        // A level-100 Gen 9 PKM so hyper training is available (Gen 9 unlocks at level 50), with the
        // ATK hyper-training flag and Pokerus infection set — both should render as checked.
        var sav = new SAV9SV();
        var pk = new PK9 { Species = (ushort)Species.Sprigatito, CurrentLevel = 100 };
        var (vm, _, _) = TestHelpers.CreateTestViewModel(pk, sav);
        Assert.True(vm.CanHyperTrain);
        vm.HyperTrainedATK = true;
        vm.IsPokerusInfected = true;

        var view = new PokemonEditor { DataContext = vm };
        // Match the minimum width of the editor pane in MainWindow so this capture is a meaningful
        // regression artifact for the compact headers and Hyper Training column.
        var window = new Window { Content = view, Width = 360, Height = 620 };
        window.Show();
        PumpToStableLayout(window);

        // The editor opens on the Main tab, which shows neither feature: the hyper-training
        // checkboxes are on the Stats tab (index 1) and the Pokerus checkboxes on the OT/Misc tab
        // (index 4). Select each tab in turn and capture one PNG per feature so the checked state
        // is actually visible in the rendered frame.
        var tabs = view.GetVisualDescendants().OfType<TabControl>().Single();

        tabs.SelectedIndex = 1; // Stats
        PumpToStableLayout(window);
        if (CaptureOrSkip(window, "hyper-training.png", "hyper training") is null)
            return;

        tabs.SelectedIndex = 4; // OT/Misc
        PumpToStableLayout(window);
        CaptureOrSkip(window, "pokerus.png", "Pokerus");
    }

    [AvaloniaFact]
    public void CaptureNativeControls_PokemonEditor_WhenEnabled_WritesPng()
    {
        if (SkipWhenCaptureDisabled())
            return;

        var sav = new SAV9SV();
        var pk = new PK9 { Species = (ushort)Species.Pikachu, CurrentLevel = 55 };
        var (vm, _, _) = TestHelpers.CreateTestViewModel(pk, sav);
        var view = new PokemonEditor { DataContext = vm };
        var window = new Window { Content = view, Width = 620, Height = 720 };
        window.Show();

        try
        {
            PumpToStableLayout(window);

            // Open a real native ComboBox so the artifact proves the composed field and popup
            // surfaces, not only their closed-state colors. Select the gender list by its actual
            // ComboItem content rather than relying on the order of the editor's hidden fields.
            var gender = view.GetVisualDescendants()
                .OfType<ComboBox>()
                .First(combo => combo.Items.Cast<object>()
                    .OfType<ComboItem>()
                    .Any(item => item.Text == "Male"));
            gender.IsDropDownOpen = true;
            PumpToStableLayout(window);

            CaptureOrSkip(window, "native-controls-pokemon-editor.png", "native control system");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void CapturePkmDatabaseScanningState_WhenEnabled_WritesPng()
    {
        if (SkipWhenCaptureDisabled())
            return;

        var sav = new SAV9SV();
        var vm = new PKMDatabaseViewModel(
            sav,
            new Mock<ISpriteRenderer>().Object,
            new Mock<IDialogService>().Object)
        {
            IsSearching = true,
            SearchProgress = 42,
        };

        var view = new PKMDatabaseView { DataContext = vm };
        var window = new Window { Content = view, Width = 960, Height = 620 };
        window.Show();
        PumpToStableLayout(window);

        CaptureOrSkip(window, "pkm-database-scanning.png", "PKM Database scanning state");
    }

    [AvaloniaFact]
    public void CaptureAuxiliaryEditorStates_WhenEnabled_WritesPng()
    {
        if (SkipWhenCaptureDisabled())
            return;

        var dialog = new Mock<IDialogService>().Object;
        CaptureAuxiliaryView(
            new EventFlagsEditor { DataContext = new EventFlagsEditorViewModel(new SAV3E()) },
            "event-flags-editor.png",
            960,
            620,
            "Event Flags editor");
        CaptureAuxiliaryView(
            new MysteryGiftEditor
            {
                DataContext = new MysteryGiftEditorViewModel(new SAV9SV(), dialog, new GiftRecordProvider()),
            },
            "mystery-gift-editor.png",
            960,
            620,
            "Mystery Gift editor");
        CaptureAuxiliaryView(
            new BatchEditor { DataContext = new BatchEditorViewModel(new SAV3E(), dialog) },
            "batch-editor.png",
            980,
            720,
            "Batch editor");
        CaptureAuxiliaryView(
            new Misc3Editor { DataContext = new Misc3EditorViewModel(new SAV3E()) },
            "misc3-editor.png",
            800,
            680,
            "Gen 3 Misc editor");
        CaptureAuxiliaryView(
            new Misc4Editor { DataContext = new Misc4EditorViewModel(new SAV4Pt()) },
            "misc4-editor.png",
            800,
            680,
            "Gen 4 Misc editor");

        using var host = new HeadlessAppFixture();
        var saveDirectory = SaveFileFixture.FindSaveFilesPath();
        Assert.NotNull(saveDirectory);
        host.LoadSave(Path.Combine(saveDirectory!, "gen9_scarlet.main"));
        var save = host.Save as SAV9SV
            ?? throw new InvalidOperationException("The Gen 9 capture save could not be loaded.");
        CaptureAuxiliaryView(
            new Misc9Editor
            {
                DataContext = new Misc9EditorViewModel(save),
            },
            "misc9-editor.png",
            800,
            680,
            "Gen 9 Misc editor");
    }

    [AvaloniaFact]
    public void CaptureLegacyMiscEditorStates_WhenEnabled_WritesPng()
    {
        if (SkipWhenCaptureDisabled())
            return;

        CaptureAuxiliaryView(
            new Misc2Editor
            {
                DataContext = new Misc2EditorViewModel(
                    LoadCaptureSave<SAV2>("gen2_crystal.sav")),
            },
            "misc2-editor.png",
            800,
            620,
            "Gen 2 Misc editor");
        CaptureAuxiliaryView(
            new Misc5Editor
            {
                DataContext = new Misc5EditorViewModel(
                    LoadCaptureSave<SAV5>("gen5_black.sav")),
            },
            "misc5-editor.png",
            920,
            700,
            "Gen 5 Misc editor");
        CaptureAuxiliaryView(
            new Misc7Editor
            {
                DataContext = new Misc7EditorViewModel(
                    LoadCaptureSave<SAV7>("gen7_sun.main")),
            },
            "misc7-editor.png",
            920,
            700,
            "Gen 7 Misc editor");
        CaptureAuxiliaryView(
            new Misc7bEditor
            {
                DataContext = new Misc7bEditorViewModel(new SAV7b()),
            },
            "misc7b-editor.png",
            800,
            620,
            "LGPE Misc editor");
        CaptureAuxiliaryView(
            new Misc8Editor
            {
                DataContext = new Misc8EditorViewModel(new SAV8SWSH()),
            },
            "misc8-editor.png",
            800,
            620,
            "Gen 8 Misc editor");
        CaptureAuxiliaryView(
            new Misc8bEditor
            {
                DataContext = new Misc8bEditorViewModel(new SAV8BS()),
            },
            "misc8b-editor.png",
            900,
            700,
            "BDSP Misc editor");
    }

    [AvaloniaFact]
    public void CaptureComplexEditorStates_WhenEnabled_WritesPng()
    {
        if (SkipWhenCaptureDisabled())
            return;

        var dialog = new Mock<IDialogService>().Object;
        var spriteRenderer = new Mock<ISpriteRenderer>().Object;
        CaptureAuxiliaryView(
            new PokeathlonEditor
            {
                DataContext = new PokeathlonEditorViewModel(LoadCaptureSave<SAV4HGSS>("gen4_heartgold.sav"), spriteRenderer),
            },
            "pokeathlon-editor.png",
            980,
            760,
            "Pokéathlon editor");
        CaptureAuxiliaryView(
            new JoinAvenueEditor
            {
                DataContext = new JoinAvenueEditorViewModel(LoadCaptureSave<SAV5B2W2>("gen5_white2.sav"), spriteRenderer, dialog),
            },
            "join-avenue-editor.png",
            980,
            760,
            "Join Avenue editor");
        CaptureAuxiliaryView(
            new GlobalLink5Editor
            {
                DataContext = new GlobalLink5EditorViewModel(LoadCaptureSave<SAV5>("gen5_white2.sav"), spriteRenderer),
            },
            "global-link-editor.png",
            760,
            720,
            "Global Link editor");
        CaptureAuxiliaryView(
            new MedalEditorView
            {
                DataContext = new MedalEditorViewModel(LoadCaptureSave<SaveFile>("gen5_white2.sav"), dialog),
            },
            "medal-editor.png",
            980,
            720,
            "Medal editor");
        CaptureAuxiliaryView(
            new FashionEditorView
            {
                DataContext = new FashionEditorViewModel(new SAV8SWSH()),
            },
            "fashion-editor.png",
            760,
            620,
            "Fashion editor");
        CaptureAuxiliaryView(
            new DonutEditor
            {
                DataContext = new DonutEditorViewModel(LoadCaptureSave<SaveFile>("gen9a_legendsza.main")),
            },
            "donut-editor.png",
            820,
            700,
            "Donut editor");
    }

    [AvaloniaFact]
    public void CaptureRemainingDialogStates_WhenEnabled_WritesPng()
    {
        if (SkipWhenCaptureDisabled())
            return;

        var spriteRenderer = new Mock<ISpriteRenderer>().Object;
        CaptureAuxiliaryView(
            new OPowerEditor { DataContext = new OPowerEditorViewModel(new SAV6XY()) },
            "o-power-editor.png",
            820,
            620,
            "O-Power editor");
        CaptureAuxiliaryView(
            new PokebeanEditor { DataContext = new PokebeanEditorViewModel(new SAV7SM()) },
            "pokebean-editor.png",
            820,
            620,
            "Poké Beans editor");
        CaptureAuxiliaryView(
            new PoketchEditorView { DataContext = new PoketchEditorViewModel(new SAV4Pt()) },
            "poketch-editor.png",
            820,
            620,
            "Pokétch editor");
        CaptureAuxiliaryView(
            new RaidEditor { DataContext = new RaidEditorViewModel(new SAV8SWSH()) },
            "raid-editor.png",
            820,
            620,
            "Raid editor");
        CaptureAuxiliaryView(
            new Raid9Editor { DataContext = new Raid9EditorViewModel(LoadCaptureSave<SAV9SV>("gen9_scarlet.main")) },
            "raid9-editor.png",
            820,
            620,
            "Gen 9 Raid editor");
        CaptureAuxiliaryView(
            new SecretBaseEditor
            {
                DataContext = new SecretBaseEditorViewModel(new SAV6XY(), spriteRenderer),
            },
            "secret-base-editor.png",
            900,
            680,
            "Secret Base editor");
    }

    [AvaloniaFact]
    public void CaptureLegacyUtilityStates_WhenEnabled_WritesPng()
    {
        if (SkipWhenCaptureDisabled())
            return;

        CaptureAuxiliaryView(
            new AccessorEditor { DataContext = new AccessorEditorViewModel(new SAV6XY()) },
            "accessor-editor.png",
            900,
            620,
            "Accessor editor");
        CaptureAuxiliaryView(
            new BoxListEditor { DataContext = new BoxListEditorViewModel(new SAV6XY()) },
            "box-list-editor.png",
            720,
            560,
            "Box list editor");
        CaptureAuxiliaryView(
            new EventWorkEditor { DataContext = new EventWorkEditorViewModel(new SAV7b()) },
            "event-work-editor.png",
            820,
            620,
            "Event work editor");
        CaptureAuxiliaryView(
            new Misc8aEditor { DataContext = new Misc8aEditorViewModel(LoadCaptureSave<SAV8LA>("gen8a_legendsarceus.main")) },
            "misc8a-editor.png",
            820,
            620,
            "Legends: Arceus Misc editor");
        CaptureAuxiliaryView(
            new SecretBase3Editor { DataContext = new SecretBase3EditorViewModel(new SAV3E()) },
            "secret-base3-editor.png",
            820,
            620,
            "Gen 3 Secret Base editor");
        CaptureAuxiliaryView(
            new SealStickers8bEditor { DataContext = new SealStickers8bEditorViewModel(new SAV8BS()) },
            "seal-stickers8b-editor.png",
            820,
            620,
            "BDSP Seal Stickers editor");
        CaptureAuxiliaryView(
            new Underground8bEditor { DataContext = new Underground8bEditorViewModel(new SAV8BS()) },
            "underground8b-editor.png",
            900,
            620,
            "BDSP Underground editor");
    }

    [AvaloniaFact]
    public void CaptureDatabaseToolStates_WhenEnabled_WritesPng()
    {
        if (SkipWhenCaptureDisabled())
            return;

        using var host = new HeadlessAppFixture();
        var saveDirectory = SaveFileFixture.FindSaveFilesPath();
        Assert.NotNull(saveDirectory);
        host.LoadSave(Path.Combine(saveDirectory!, "gen9_scarlet.main"));
        var save = host.Save ?? throw new InvalidOperationException("The database capture save could not be loaded.");
        var spriteRenderer = host.Services.GetRequiredService<ISpriteRenderer>();

        CaptureAuxiliaryView(
            new PKMDatabaseView
            {
                DataContext = new PKMDatabaseViewModel(save, spriteRenderer, host.Dialogs),
            },
            "pkm-database-editor.png",
            1100,
            700,
            "PKM Database");
        CaptureAuxiliaryView(
            new EncounterDatabaseView
            {
                DataContext = new EncounterDatabaseViewModel(save, spriteRenderer, host.Dialogs, _ => { }),
            },
            "encounter-database-editor.png",
            900,
            650,
            "Encounter Database");
        CaptureAuxiliaryView(
            new BoxReportView { DataContext = new BoxReportViewModel(save, host.Dialogs) },
            "box-report-editor.png",
            1100,
            600,
            "Box Data Report");
        CaptureAuxiliaryView(
            new LegalityAuditView { DataContext = new LegalityAuditViewModel(save, host.Dialogs) },
            "legality-audit-editor.png",
            1100,
            600,
            "Legality Audit");
        CaptureAuxiliaryView(
            new MysteryGiftDatabaseView
            {
                DataContext = new MysteryGiftDatabaseViewModel(save, spriteRenderer, host.Dialogs),
            },
            "mystery-gift-database-editor.png",
            1100,
            700,
            "Mystery Gift Database");
    }

    [AvaloniaFact]
    public void CapturePokedexEditorStates_WhenEnabled_WritesPng()
    {
        if (SkipWhenCaptureDisabled())
            return;

        CaptureAuxiliaryView(
            new Pokedex6Editor { DataContext = new Pokedex6EditorViewModel(new SAV6XY()) },
            "pokedex6-editor.png",
            1000,
            680,
            "Gen 6 Pokédex editor");
        CaptureAuxiliaryView(
            new Pokedex7bEditor { DataContext = new Pokedex7bEditorViewModel(new SAV7b()) },
            "pokedex7b-editor.png",
            920,
            650,
            "LGPE Pokédex editor");

        using var host = new HeadlessAppFixture();
        var saveDirectory = SaveFileFixture.FindSaveFilesPath();
        Assert.NotNull(saveDirectory);
        host.LoadSave(Path.Combine(saveDirectory!, "gen9_scarlet.main"));
        var save = host.Save as SAV9SV
            ?? throw new InvalidOperationException("The Gen 9 Pokédex capture save could not be loaded.");
        CaptureAuxiliaryView(
            new PokedexGen9Editor { DataContext = new PokedexGen9EditorViewModel(save) },
            "pokedex-gen9-editor.png",
            900,
            620,
            "Gen 9 Pokédex editor");
        CaptureAuxiliaryView(
            new PokedexLAEditor { DataContext = new PokedexLAEditorViewModel(BlankSaveFile.Get(GameVersion.PLA)) },
            "pokedex-la-editor.png",
            1000,
            680,
            "Legends Pokédex editor");
    }

    [AvaloniaFact]
    public void CaptureDensityModes_MainWindow_WhenEnabled_WritesPng()
    {
        if (SkipWhenCaptureDisabled())
            return;

        using var app = new HeadlessAppFixture();
        app.Window.Width = 1024;
        app.Window.Height = 720;
        var saveDirectory = SaveFileFixture.FindSaveFilesPath();
        Assert.NotNull(saveDirectory);
        var density = app.Services.GetRequiredService<IUiDensityService>();
        // The declarative application resources already carry the Compact compatibility default.
        // Capture that initial frame without a same-value resource notification; later mode changes
        // are captured after the explicit repaint barrier in CaptureDensityMode.
        app.LoadSave(Path.Combine(saveDirectory!, "gen9a_legendsza.main"));

        try
        {
            CaptureCurrentDensity(app, "mainwindow-density-compact.png");
            CaptureDensityMode(app, density, AppDensity.Comfortable, "mainwindow-density-comfortable.png");
        }
        finally
        {
            // Do not leave the process-wide resource dictionary in the capture-only mode for any
            // later opt-in captures running in the same test process.
            density.ApplyDensity(AppDensity.Compact);
        }
    }

    [AvaloniaFact]
    public void CaptureTaskAwareShellStates_WhenEnabled_WritesPng()
    {
        if (SkipWhenCaptureDisabled())
            return;

        using var app = new HeadlessAppFixture();
        app.Window.Width = 1024;
        app.Window.Height = 720;
        var saveDirectory = SaveFileFixture.FindSaveFilesPath();
        Assert.NotNull(saveDirectory);
        app.LoadSave(Path.Combine(saveDirectory!, "gen9a_legendsza.main"));
        app.ClickSlot(0, 0);
        var boxView = app.Find<BoxViewer>();
        Assert.NotNull(boxView);
        app.Focus(boxView!);
        app.PressKey(PhysicalKey.Enter);
        app.Pump();

        // Use a fresh top-level surface for every shell state so each visual artifact is a complete
        // repaint, including the initial Pokémon workspace after the save-loaded tree is realized.
        app.Pump();
        CaptureFreshShellState(app, "shell-pokemon.png", "Pokémon workspace");

        app.ViewModel.SelectedWorkspaceIndex = 1;
        app.Pump();
        CaptureFreshShellState(app, "shell-party.png", "Party workspace");
        app.ViewModel.SelectedWorkspaceIndex = 0;
        app.Pump();

        app.ViewModel.ActiveWorkspace = MainWorkspace.Save;
        app.Pump();
        CaptureFreshShellState(app, "shell-save.png", "Save workspace");

        app.ViewModel.SelectedWorkspaceIndex = 3;
        app.Pump();
        CaptureFreshShellState(app, "shell-inventory.png", "Inventory workspace");

        app.ViewModel.ActiveWorkspace = MainWorkspace.Reports;
        app.Pump();
        CaptureFreshShellState(app, "shell-reports.png", "Reports workspace");

        app.ViewModel.IsToolLauncherOpen = true;
        app.Pump();
        CaptureFreshShellState(app, "shell-launcher.png", "Tool launcher");
        app.ViewModel.IsToolLauncherOpen = false;
    }

    [AvaloniaFact]
    public void CaptureToolsMenu_WhenEnabled_WritesPng()
    {
        if (SkipWhenCaptureDisabled())
            return;

        using var app = new HeadlessAppFixture();
        app.Window.Width = 1024;
        app.Window.Height = 720;
        app.LoadSaveInstance(new SAV6XY());

        var toolsMenu = app.Window.GetVisualDescendants()
            .OfType<MenuItem>()
            .Single(menu => Equals(menu.Header, LocalizedStrings.Instance["Menu_Tools"]));
        toolsMenu.IsSubMenuOpen = true;
        app.Pump();

        CaptureShellState(app.Window, "tools-menu.png", "capability-driven Tools menu");
        toolsMenu.IsSubMenuOpen = false;
    }

    [AvaloniaFact]
    public void CaptureThemeVariants_WhenEnabled_WritesPng()
    {
        if (SkipWhenCaptureDisabled())
            return;

        using var app = new HeadlessAppFixture();
        app.Window.Width = 1024;
        app.Window.Height = 720;
        var saveDirectory = SaveFileFixture.FindSaveFilesPath();
        Assert.NotNull(saveDirectory);
        app.LoadSave(Path.Combine(saveDirectory!, "gen9a_legendsza.main"));

        var theme = app.Services.GetRequiredService<IThemeService>();
        try
        {
            foreach (var (variant, fileName) in new[]
                     {
                         (AppTheme.Dark, "shell-theme-dark.png"),
                         (AppTheme.Light, "shell-theme-light.png"),
                     })
            {
                theme.ApplyTheme(variant);
                app.Pump();
                CaptureFreshShellState(app, fileName, $"{variant} theme");
            }
        }
        finally
        {
            theme.ApplyTheme(AppTheme.Dark);
        }
    }

    private bool SkipWhenCaptureDisabled()
    {
        if (Environment.GetEnvironmentVariable("PKHEX_HEADLESS_CAPTURE") == "1")
            return false;
        output.WriteLine("Skipped: set PKHEX_HEADLESS_CAPTURE=1 with the Skia headless app builder.");
        return true;
    }

    private static string CaptureDirectory() =>
        Environment.GetEnvironmentVariable("PKHEX_HEADLESS_CAPTURE_DIR")
        ?? Path.Combine(Path.GetTempPath(), "pkhex-headless-frames");

    private static void PumpToStableLayout(Window window)
    {
        for (var i = 0; i < 10; i++)
        {
            Dispatcher.UIThread.RunJobs();
            window.UpdateLayout();
            // Commit the visual tree into the server-side composition scene.
            AvaloniaHeadlessPlatform.ForceRenderTimerTick();
        }
    }

    private string? CaptureOrSkip(Window window, string fileName, string featureLabel)
    {
        var path = Path.Combine(CaptureDirectory(), fileName);
        var saved = CaptureWindow(window, path);
        if (saved is null)
        {
            output.WriteLine("Skipped: headless drawing mode produced no frame.");
            return null;
        }

        Assert.Equal(path, saved);
        Assert.True(File.Exists(path));
        Assert.True(new FileInfo(path).Length > 0);
        output.WriteLine($"Saved Pokemon editor ({featureLabel}) screenshot to {path}");
        return saved;
    }

    private void CaptureDensityMode(HeadlessAppFixture app, IUiDensityService density, AppDensity mode, string fileName)
    {
        var changed = density.CurrentDensity != mode;
        if (changed)
            density.ApplyDensity(mode);
        var captureWindow = changed
            ? new MainWindow
            {
                DataContext = app.ViewModel,
                Width = app.Window.Width,
                Height = app.Window.Height,
            }
            : app.Window;

        try
        {
            if (changed)
                captureWindow.Show();
            app.Pump();
            // A fresh top-level surface gives Skia a complete repaint after a DynamicResource
            // replacement. This avoids treating a valid runtime reflow as a partial screenshot.
            if (changed)
                PumpToStableLayout(captureWindow);

            var path = Path.Combine(CaptureDirectory(), fileName);
            var saved = CaptureWindow(captureWindow, path);
            if (saved is null)
            {
                output.WriteLine("Skipped: headless drawing mode produced no frame.");
                return;
            }

            Assert.Equal(path, saved);
            Assert.True(File.Exists(path));
            Assert.True(new FileInfo(path).Length > 0);
            output.WriteLine($"Saved {mode} density screenshot to {path}");
        }
        finally
        {
            if (changed)
                captureWindow.Close();
        }
    }

    private void CaptureCurrentDensity(HeadlessAppFixture app, string fileName)
    {
        app.Pump();

        var path = Path.Combine(CaptureDirectory(), fileName);
        var saved = CaptureWindow(app.Window, path);
        if (saved is null)
        {
            output.WriteLine("Skipped: headless drawing mode produced no frame.");
            return;
        }

        Assert.Equal(path, saved);
        Assert.True(File.Exists(path));
        Assert.True(new FileInfo(path).Length > 0);
        output.WriteLine($"Saved initial Compact density screenshot to {path}");
    }

    private void CaptureAuxiliaryView(Control view, string fileName, double width, double height, string stateLabel)
    {
        var window = new Window { Content = view, Width = width, Height = height };
        window.Show();
        try
        {
            PumpToStableLayout(window);
            var path = Path.Combine(CaptureDirectory(), fileName);
            var saved = CaptureWindow(window, path);
            if (saved is null)
            {
                output.WriteLine("Skipped: headless drawing mode produced no frame.");
                return;
            }

            Assert.Equal(path, saved);
            Assert.True(File.Exists(path));
            Assert.True(new FileInfo(path).Length > 0);
            output.WriteLine($"Saved {stateLabel} screenshot to {path}");
        }
        finally
        {
            window.Close();
        }
    }

    private static TSave LoadCaptureSave<TSave>(string fileName)
        where TSave : SaveFile
    {
        var saveDirectory = SaveFileFixture.FindSaveFilesPath()
            ?? throw new InvalidOperationException("The capture save directory could not be found.");
        return SaveFileFixture.LoadSave(Path.Combine(saveDirectory, fileName)) as TSave
            ?? throw new InvalidOperationException($"The capture save {fileName} could not be loaded as {typeof(TSave).Name}.");
    }

    private void CaptureFreshShellState(HeadlessAppFixture app, string fileName, string stateLabel)
    {
        var window = new MainWindow
        {
            DataContext = app.ViewModel,
            Width = app.Window.Width,
            Height = app.Window.Height,
        };
        window.Show();
        try
        {
            PumpToStableLayout(window);
            CaptureShellState(window, fileName, stateLabel);
        }
        finally
        {
            window.Close();
        }
    }

    private void CaptureShellState(Window window, string fileName, string stateLabel)
    {
        var path = Path.Combine(CaptureDirectory(), fileName);
        var saved = CaptureWindow(window, path);
        if (saved is null)
        {
            output.WriteLine("Skipped: headless drawing mode produced no frame.");
            return;
        }

        Assert.Equal(path, saved);
        Assert.True(File.Exists(path));
        Assert.True(new FileInfo(path).Length > 0);
        output.WriteLine($"Saved {stateLabel} screenshot to {path}");
    }

    private static string? CaptureWindow(Window window, string pngPath)
    {
        WriteableBitmap? frame;
        try
        {
            // Throws NotSupportedException under the default headless drawing mode (no real pixels);
            // only succeeds when the assembly's app builder enables Skia + UseHeadlessDrawing = false.
            frame = window.GetLastRenderedFrame();
        }
        catch (NotSupportedException)
        {
            return null;
        }
        if (frame is null)
            return null;
        Directory.CreateDirectory(Path.GetDirectoryName(pngPath)!);
        using var fs = File.Create(pngPath);
        frame.Save(fs);
        return pngPath;
    }
}
