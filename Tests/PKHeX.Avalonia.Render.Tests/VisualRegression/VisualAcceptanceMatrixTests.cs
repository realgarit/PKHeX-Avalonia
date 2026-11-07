using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Styling;
using Avalonia.VisualTree;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using PKHeX.Application.Abstractions;
using PKHeX.Application.Services;
using PKHeX.Avalonia.Services;
using PKHeX.Avalonia.Views;
using PKHeX.Core;
using PKHeX.Presentation.Models;
using PKHeX.Presentation.ViewModels;
using PKHeX.Presentation.Localization;

namespace PKHeX.Avalonia.Render.Tests.VisualRegression;

/// <summary>
/// The visual acceptance matrix (docs/visual-regression.md): the compact 900x600 shell with the
/// Pokémon editor sections Main/Stats/Met/Moves/OT, the Box and Party workspaces, and two
/// representative dialogs, each in Dark, Light and HighContrast, compared against reviewed baselines.
/// The box is populated from the public Legends: Z-A fixture with legal, illegal, shiny, Alpha,
/// held-item, selected, hovered, keyboard-focused and empty slots, all in memory (the fixture file is
/// never written).
/// </summary>
public class VisualAcceptanceMatrixTests
{
    public static TheoryData<string> Themes => ["dark", "light", "highcontrast"];

    private static ThemeVariant Variant(string theme) => theme switch
    {
        "light" => ThemeVariant.Light,
        "highcontrast" => AppThemeVariants.HighContrast,
        _ => ThemeVariant.Dark,
    };

    [AvaloniaTheory]
    [MemberData(nameof(Themes))]
    public async Task ShellEditorBoxAndPartyMatchBaselines(string theme)
    {
        var previous = global::Avalonia.Application.Current!.RequestedThemeVariant;
        using var host = new ShellHost();
        var failures = new List<string>();
        try
        {
            host.ApplyTheme(theme);
            var (save, states) = MatrixSave.Create();
            host.Gateway.OpenLoadedSave(save, "visual-matrix.main");
            var vm = host.ViewModel;
            Assert.True(vm.HasSave);

            var window = host.Show();
            await PKHeX.Testing.HeadlessRenderSettling.Settle(window);
            var box = vm.BoxViewer!;
            // Semantic states must really be present, or the baseline would silently lose them.
            Assert.True(box.Slots[states.ShinyAlpha].IsShiny);
            Assert.Contains(LocalizedStrings.Instance["SlotState_Alpha"], box.Slots[states.ShinyAlpha].SemanticSummary);
            Assert.True(box.Slots[states.ShinyAlpha].IsLegal);
            Assert.False(box.Slots[states.Illegal].IsLegal);
            Assert.NotEqual(0, box.Slots[states.HeldItem].HeldItem);
            Assert.True(box.Slots[states.Empty].IsEmpty);

            box.SelectedIndex = states.ShinyAlpha;
            var editor = vm.CurrentPokemonEditor!;
            editor.LoadPKM(save.GetBoxSlotAtIndex(0, states.ShinyAlpha));
            Assert.True(editor.IsLegal, editor.LegalityReport);
            await PKHeX.Testing.HeadlessRenderSettling.Settle(window);
            Assert.True(box.Slots[states.ShinyAlpha].IsSelected);

            // Interaction states: one slot hovered, another keyboard-focused (focus-visible ring).
            var slots = window.GetVisualDescendants().OfType<Button>().Where(b => b.Tag is SlotData { Box: 0 }).ToArray();
            Assert.Equal(save.BoxSlotCount, slots.Length);
            ((IPseudoClasses)slots[states.Hovered].Classes).Set(":pointerover", true);
            Assert.True(slots[states.Focused].Focus(NavigationMethod.Tab));
            await PKHeX.Testing.HeadlessRenderSettling.Settle(window);
            Assert.True(box.Slots[states.ShinyAlpha].IsSelected);

            string[] sections = ["main", "stats", "met", "moves", "ot"];
            for (var section = 0; section < sections.Length; section++)
            {
                editor.SelectedEditorSection = section;
                await PKHeX.Testing.HeadlessRenderSettling.Settle(window);
                using var frame = VisualBaseline.Render(window);
                Assert.Equal((900, 600), (frame.Width, frame.Height));
                if (VisualBaseline.Check($"shell-box-editor-{sections[section]}-{theme}", frame) is { } failure)
                    failures.Add(failure);
            }
            editor.SelectedEditorSection = 0;

            // Party workspace with the shiny Alpha selected in the box above.
            vm.SelectWorkspaceTabCommand.Execute(MainWindowViewModel.PartyWorkspaceTabIndex);
            await PKHeX.Testing.HeadlessRenderSettling.Settle(window);
            Assert.Contains(window.GetVisualDescendants().OfType<PartyViewer>(), p => p.IsEffectivelyVisible);
            using (var frame = VisualBaseline.Render(window))
                if (VisualBaseline.Check($"shell-party-{theme}", frame) is { } failure)
                    failures.Add(failure);
        }
        finally
        {
            global::Avalonia.Application.Current.RequestedThemeVariant = previous;
        }
        VisualBaseline.AssertAll(failures);
    }

    [AvaloniaTheory]
    [MemberData(nameof(Themes))]
    public async Task RepresentativeDialogsMatchBaselines(string theme)
    {
        var previous = global::Avalonia.Application.Current!.RequestedThemeVariant;
        using var host = new ShellHost();
        var failures = new List<string>();
        Window? window = null;
        try
        {
            host.ApplyTheme(theme);
            var services = host.Services;

            // Settings: the main preferences dialog. Linux-only sections are pinned off so the frame
            // does not depend on the OS running the test.
            var settings = new SettingsViewModel(services.GetRequiredService<AppSettings>(), services.GetRequiredService<ISettingsStore>(),
                services.GetRequiredService<IThemeService>(), services.GetRequiredService<IUiDensityService>(),
                services.GetRequiredService<LanguageService>(), services.GetRequiredService<UpdateCheckCoordinator>(), isLinux: false);
            window = new Window { Width = 420, Height = 520, Content = new SettingsView { DataContext = settings } };
            window.Show();
            await PKHeX.Testing.HeadlessRenderSettling.Settle(window);
            using (var frame = VisualBaseline.Render(window))
                if (VisualBaseline.Check($"dialog-settings-{theme}", frame) is { } failure)
                    failures.Add(failure);
            window.Close();

            // Box Report: a data-grid tool window over the populated matrix save.
            var (save, _) = MatrixSave.Create();
            var report = new BoxReportViewModel(save, Mock.Of<IDialogService>(), new AppSettings(), Mock.Of<ISettingsStore>());
            window = new Window { Width = 900, Height = 600, Content = new BoxReportView { DataContext = report } };
            window.Show();
            await PKHeX.Testing.HeadlessRenderSettling.Settle(window);
            using (var frame = VisualBaseline.Render(window))
                if (VisualBaseline.Check($"dialog-box-report-{theme}", frame) is { } failure)
                    failures.Add(failure);
        }
        finally
        {
            window?.Close();
            global::Avalonia.Application.Current.RequestedThemeVariant = previous;
        }
        VisualBaseline.AssertAll(failures);
    }

    /// <summary>The production composition root with the two surface-bound services replaced.</summary>
    private sealed class ShellHost : IDisposable
    {
        private readonly string _root = Path.Combine(Path.GetTempPath(), "pkhex-visual-" + Guid.NewGuid().ToString("N"));
        private readonly ServiceProvider _services;
        private MainWindow? _window;

        public ShellHost()
        {
            var paths = new Mock<IAppPaths>();
            paths.SetupGet(x => x.ConfigDirectory).Returns(Path.Combine(_root, "config"));
            paths.SetupGet(x => x.DataDirectory).Returns(Path.Combine(_root, "data"));
            paths.SetupGet(x => x.ConfigFilePath).Returns(Path.Combine(_root, "config.json"));
            paths.SetupGet(x => x.LegacyConfigFilePath).Returns(Path.Combine(_root, "legacy.json"));
            _services = (ServiceProvider)App.BuildServiceProvider(paths.Object, Mock.Of<ISettingsStore>(), new AppSettings(), s =>
            {
                s.AddSingleton(Mock.Of<IDialogService>());
                s.AddSingleton(Mock.Of<IWindowService>());
            });
            ViewModel = _services.GetRequiredService<MainWindowViewModel>();
            Gateway = _services.GetRequiredService<ISaveFileGateway>();
        }

        public IServiceProvider Services => _services;
        public MainWindowViewModel ViewModel { get; }
        public ISaveFileGateway Gateway { get; }

        /// <summary>
        /// Selects the theme through the production <see cref="IThemeService"/> so the shell's theme
        /// picker shows it. HighContrast is no longer user-selectable (the service normalizes it to Dark),
        /// so its retained palette is applied directly as the app theme variant on top.
        /// </summary>
        public void ApplyTheme(string theme)
        {
            _services.GetRequiredService<IThemeService>().ApplyTheme(theme == "light" ? AppTheme.Light : AppTheme.Dark);
            global::Avalonia.Application.Current!.RequestedThemeVariant = Variant(theme);
            ViewModel.RefreshThemeSelection();
        }

        public MainWindow Show()
        {
            _window = new MainWindow { DataContext = ViewModel, Width = 900, Height = 600 };
            _window.Show();
            return _window;
        }

        public void Dispose()
        {
            try
            {
                Gateway.CloseSave();
                if (_window is not null)
                {
                    _window.DataContext = null;
                    _window.Close();
                    _window.Content = null;
                }
            }
            finally
            {
                _services.Dispose();
                try { Directory.Delete(_root, true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
            }
        }
    }
}

/// <summary>Slot indexes (box 1) of each semantic state in <see cref="MatrixSave"/>.</summary>
internal sealed record MatrixStates(int ShinyAlpha, int Illegal, int HeldItem, int Empty, int Hovered, int Focused);

/// <summary>Builds the populated in-memory Legends: Z-A save used by the acceptance matrix.</summary>
internal static class MatrixSave
{
    public static (SAV9ZA Save, MatrixStates States) Create()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Tests/savefiles/gen9a_legendsza.main")))
            directory = directory.Parent;
        Assert.NotNull(directory);
        var save = Assert.IsType<SAV9ZA>(SaveUtil.GetSaveFile(File.ReadAllBytes(Path.Combine(directory.FullName, "Tests/savefiles/gen9a_legendsza.main"))));
        save.CurrentBox = 0;

        var occupied = Enumerable.Range(0, save.BoxSlotCount).Where(i => save.GetBoxSlotAtIndex(0, i).Species != 0).ToArray();
        // The fixture's existing Alpha Machamp, made shiny (still legal, as in the README capture).
        var alpha = occupied.First(i => save.GetBoxSlotAtIndex(0, i) is PA9 { Species: 68, IsAlpha: true });
        var shiny = save.GetBoxSlotAtIndex(0, alpha);
        shiny.SetShiny();
        save.SetBoxSlotAtIndex(shiny, 0, alpha);

        var others = occupied.Where(i => i != alpha).ToArray();
        Assert.True(others.Length >= 5, "The Z-A fixture needs at least six Pokémon in box 1.");

        // Illegal: a level below the met level cannot happen in game.
        var illegal = others[0];
        var bad = save.GetBoxSlotAtIndex(0, illegal);
        bad.CurrentLevel = 1;
        bad.MetLevel = 50;
        save.SetBoxSlotAtIndex(bad, 0, illegal);

        var held = others[1];
        var holder = save.GetBoxSlotAtIndex(0, held);
        holder.HeldItem = 155; // Oran Berry
        save.SetBoxSlotAtIndex(holder, 0, held);

        // Empty: clear the last occupied slot so an empty tile sits between populated ones.
        var empty = others[^1];
        save.SetBoxSlotAtIndex(save.BlankPKM, 0, empty);

        return (save, new MatrixStates(alpha, illegal, held, empty, others[2], others[3]));
    }
}
