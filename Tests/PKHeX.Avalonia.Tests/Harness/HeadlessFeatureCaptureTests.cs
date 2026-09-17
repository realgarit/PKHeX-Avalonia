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

        app.ViewModel.ActiveWorkspace = MainWorkspace.Reports;
        app.Pump();
        CaptureFreshShellState(app, "shell-reports.png", "Reports workspace");

        app.ViewModel.IsToolLauncherOpen = true;
        app.Pump();
        CaptureFreshShellState(app, "shell-launcher.png", "Tool launcher");
        app.ViewModel.IsToolLauncherOpen = false;
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
