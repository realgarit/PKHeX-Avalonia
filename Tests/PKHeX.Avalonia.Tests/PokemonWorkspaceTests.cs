using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Moq;
using PKHeX.Application.Abstractions;
using PKHeX.Application.UseCases;
using PKHeX.Avalonia.Views;
using PKHeX.Core;
using PKHeX.Presentation.ViewModels;

namespace PKHeX.Avalonia.Tests;

/// <summary>
/// Standalone Pokémon workspace (issue #316): edit and export individual Pokémon files against an
/// in-memory blank save for a selected target game, without loading or writing a real save.
/// </summary>
public sealed class PokemonWorkspaceTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), $"pkhex-pokemon-workspace-{Guid.NewGuid():N}");

    public PokemonWorkspaceTests() => Directory.CreateDirectory(_dir);

    public void Dispose()
    {
        try { Directory.Delete(_dir, recursive: true); }
        catch { /* best-effort temp cleanup */ }
    }

    public static TheoryData<GameVersion> AllTargets()
    {
        var data = new TheoryData<GameVersion>();
        foreach (var version in BlankWorkspaceUseCase.SupportedTargets)
            data.Add(version);
        return data;
    }

    [Theory]
    [MemberData(nameof(AllTargets))]
    public void EverySupportedTargetCreatesACleanInMemoryBlankSave(GameVersion version)
    {
        var sav = new BlankWorkspaceUseCase().Execute(version);

        // Saves that cannot tell paired games apart store their group instead.
        var expected = version switch
        {
            GameVersion.D or GameVersion.P => GameVersion.DP,
            GameVersion.CXD => GameVersion.XD,
            _ => version,
        };
        Assert.Equal(expected, sav.Version);
        Assert.False(sav.State.Edited);
        Assert.True(string.IsNullOrEmpty(sav.Metadata.FilePath));
        Assert.Equal(version.SaveFileType.Context, sav.Context);
        Assert.NotNull(sav.BlankPKM);
    }

    [Fact]
    public void TargetListCoversEveryBlankSaveFormat()
    {
        var targets = BlankWorkspaceUseCase.SupportedTargets;

        // Every game with a save format, including side games, and none without one.
        foreach (var version in new[] { GameVersion.RD, GameVersion.C, GameVersion.E, GameVersion.FR, GameVersion.CXD,
                     GameVersion.Pt, GameVersion.HG, GameVersion.BATREV, GameVersion.W2, GameVersion.OR, GameVersion.UM,
                     GameVersion.GP, GameVersion.SH, GameVersion.BD, GameVersion.PLA, GameVersion.VL, GameVersion.ZA })
            Assert.Contains(version, targets);
        Assert.DoesNotContain(GameVersion.GO, targets);
        Assert.DoesNotContain(GameVersion.FRX, targets);
        Assert.All(targets, v => Assert.NotEqual(SaveFileType.None, v.SaveFileType));

        var picker = new BlankWorkspaceTargetViewModel(GameVersion.BATREV);
        Assert.Equal(targets.Count, picker.Targets.Count);
        Assert.Equal(GameVersion.BATREV, picker.SelectedTarget?.Version);
        Assert.Contains("BK4", picker.Targets.Single(t => t.Version == GameVersion.BATREV).Detail);
        Assert.Contains("XK3", picker.Targets.Single(t => t.Version == GameVersion.CXD).Detail);
        Assert.Contains("PK9", picker.Targets.Single(t => t.Version == GameVersion.VL).Detail);
    }

    [Fact]
    public void SuggestedTargetFollowsTheFileFormat()
    {
        Assert.Equal(GameVersion.VL, BlankWorkspaceUseCase.SuggestTarget(new PK9()));
        Assert.Equal(GameVersion.FR, BlankWorkspaceUseCase.SuggestTarget(new PK3 { Version = GameVersion.FR }));
        // A PK8 that came from Sword/Shield keeps its game; a transferred one falls back to the context.
        Assert.Equal(GameVersion.SW, BlankWorkspaceUseCase.SuggestTarget(new PK8 { Version = GameVersion.SW }));
        Assert.Equal(GameVersion.SH, BlankWorkspaceUseCase.SuggestTarget(new PK8 { Version = GameVersion.US }));
    }

    [Fact]
    public async Task NewWorkspace_UsesTheSelectedTargetAndBlocksSaveWrites()
    {
        var harness = new Harness(choose: GameVersion.SL);

        await harness.Vm.NewPokemonWorkspaceCommand.ExecuteAsync(null);

        var sav = Assert.IsType<SAV9SV>(harness.Gateway.CurrentSave);
        Assert.Equal(GameVersion.SL, sav.Version);
        Assert.Null(harness.Gateway.CurrentPath);
        Assert.True(harness.Vm.IsBlankWorkspace);
        Assert.NotNull(harness.Vm.CurrentPokemonEditor);
        Assert.False(harness.Vm.SaveFileCommand.CanExecute(null));
        Assert.False(harness.Vm.SaveFileAsCommand.CanExecute(null));
        Assert.True(harness.Vm.SavePokemonAsCommand.CanExecute(null));
        Assert.Contains(GameVersion.SL.ToString(), harness.Vm.StatusText);
        Assert.Equal(0, harness.Gateway.SaveCalls);
    }

    [Fact]
    public async Task NewWorkspace_CancelledPicker_LeavesTheAppEmpty()
    {
        var harness = new Harness(choose: null);

        await harness.Vm.NewPokemonWorkspaceCommand.ExecuteAsync(null);

        Assert.Null(harness.Gateway.CurrentSave);
        Assert.False(harness.Vm.IsBlankWorkspace);
    }

    [Fact]
    public async Task OpenPokemonFile_WithoutASave_SuggestsItsGameAndLoadsItWithLegality()
    {
        var path = WriteEntity(CreatePk9(25), "pikachu.pk9");
        var harness = new Harness(choose: GameVersion.VL, filePath: path);

        await harness.Vm.OpenPokemonFileCommand.ExecuteAsync(null);

        // The file came from Scarlet, so the picker preselects Scarlet.
        Assert.Equal(GameVersion.SL, harness.SuggestedTarget);
        Assert.True(harness.Vm.IsBlankWorkspace);
        var editor = Assert.IsType<PokemonEditorViewModel>(harness.Vm.CurrentPokemonEditor);
        var pk = editor.PreparePKM();
        Assert.IsType<PK9>(pk);
        Assert.Equal((ushort)25, pk.Species);
        Assert.True(editor.ShowLegalityStatus);
        Assert.False(string.IsNullOrWhiteSpace(editor.LegalityReport));
    }

    [Fact]
    public async Task OpenPokemonFile_ConvertsToTheSelectedTargetFormat()
    {
        var pk3 = new PK3 { Species = 25, Version = GameVersion.E, TID16 = 12345, SID16 = 54321, OriginalTrainerName = "ASH", CurrentLevel = 10, Language = (int)LanguageID.English };
        pk3.Nickname = SpeciesName.GetSpeciesNameGeneration(25, pk3.Language, 3);
        pk3.RefreshChecksum();
        var path = WriteEntity(pk3, "pikachu.pk3");
        // The file suggests Emerald; the user picks Platinum instead (Pal Park transfer format).
        var harness = new Harness(choose: GameVersion.Pt, filePath: path);

        await harness.Vm.OpenPokemonFileCommand.ExecuteAsync(null);

        Assert.Equal(GameVersion.E, harness.SuggestedTarget);
        Assert.IsType<SAV4Pt>(harness.Gateway.CurrentSave);
        var pk = harness.Vm.CurrentPokemonEditor!.PreparePKM();
        Assert.IsType<PK4>(pk);
        Assert.Equal((ushort)25, pk.Species);
        Assert.Equal("ASH", pk.OriginalTrainerName);
    }

    [Fact]
    public async Task EditedPokemon_ExportsTheExactEntityBytes()
    {
        var path = WriteEntity(CreatePk9(25), "pikachu.pk9");
        var exportPath = Path.Combine(_dir, "edited.pk9");
        var harness = new Harness(choose: GameVersion.VL, filePath: path, savePath: exportPath);
        await harness.Vm.OpenPokemonFileCommand.ExecuteAsync(null);
        var editor = harness.Vm.CurrentPokemonEditor!;

        editor.HeldItem = 13;
        editor.Nickname = "Sparky";
        editor.IsNicknamed = true;
        await harness.Vm.SavePokemonAsCommand.ExecuteAsync(null);

        var expected = new ExportEntityToFileUseCase().Execute(editor.PreparePKM())!.Value;
        var written = File.ReadAllBytes(exportPath);
        Assert.Equal(expected.Data, written);
        Assert.EndsWith(".pk9", harness.SuggestedFileName);
        harness.Dialogs.Verify(d => d.SaveFileAsync(It.IsAny<string>(), It.IsAny<string?>(),
            It.Is<string[]?>(f => f != null && f.Contains("*.pk9"))), Times.Once);

        var reloaded = Assert.IsType<PK9>(EntityFormat.GetFromBytes(written));
        Assert.Equal(13, reloaded.HeldItem);
        Assert.Equal("Sparky", reloaded.Nickname);
        // The blank context itself was never written.
        Assert.Equal(0, harness.Gateway.SaveCalls);
    }

    [Fact]
    public async Task SavePokemonAs_WithEmptyEditor_ShowsAnErrorInsteadOfWriting()
    {
        var harness = new Harness(choose: GameVersion.VL);
        await harness.Vm.NewPokemonWorkspaceCommand.ExecuteAsync(null);

        await harness.Vm.SavePokemonAsCommand.ExecuteAsync(null);

        harness.Dialogs.Verify(d => d.ShowErrorAsync(It.IsAny<string>(), It.IsAny<string>()), Times.Once);
        harness.Dialogs.Verify(d => d.SaveFileAsync(It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<string[]?>()), Times.Never);
    }

    [Fact]
    public async Task DroppedPokemonFile_WithoutASave_StartsAWorkspace()
    {
        var path = WriteEntity(CreatePk9(133), "eevee.pk9");
        var harness = new Harness(choose: GameVersion.SL);

        await harness.Vm.HandleWindowFileDropAsync([path]);

        Assert.True(harness.Vm.IsBlankWorkspace);
        Assert.Equal((ushort)133, harness.Vm.CurrentPokemonEditor!.PreparePKM().Species);
    }

    [Fact]
    public async Task EditedBlankWorkspace_ClosesWithoutTheUnsavedChangesPrompt()
    {
        var harness = new Harness(choose: GameVersion.SL);
        harness.Settings.EditorBehavior.WarnClosingModified = true;
        await harness.Vm.NewPokemonWorkspaceCommand.ExecuteAsync(null);
        harness.Gateway.CurrentSave!.State.Edited = true;

        Assert.True(await harness.Vm.CanLeaveCurrentSaveAsync());
        await harness.Vm.CloseFileCommand.ExecuteAsync(null);

        Assert.Null(harness.Gateway.CurrentSave);
        Assert.False(harness.Vm.IsBlankWorkspace);
        harness.Dialogs.Verify(d => d.ShowUnsavedChangesAsync(It.IsAny<string>(), It.IsAny<string>(),
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task RealSave_AfterAWorkspace_KeepsTheNormalSaveWorkflow()
    {
        var harness = new Harness(choose: GameVersion.SL);
        harness.Settings.EditorBehavior.WarnClosingModified = true;
        await harness.Vm.NewPokemonWorkspaceCommand.ExecuteAsync(null);

        // Opening a real save replaces the workspace; the save commands and the guard apply again.
        var savePath = Path.Combine(_dir, "main.sav");
        File.WriteAllBytes(savePath, BlankSaveFile.Get(GameVersion.B2).Write().ToArray());
        Assert.True(await harness.Gateway.LoadSaveFileAsync(savePath));

        Assert.False(harness.Vm.IsBlankWorkspace);
        Assert.True(harness.Vm.SaveFileCommand.CanExecute(null));
        Assert.True(harness.Vm.SaveFileAsCommand.CanExecute(null));
        Assert.Equal(savePath, harness.Gateway.CurrentPath);

        await harness.Vm.SaveFileCommand.ExecuteAsync(null);
        Assert.Equal(1, harness.Gateway.SaveCalls);

        harness.Gateway.CurrentSave!.State.Edited = true;
        harness.Dialogs.Setup(d => d.ShowUnsavedChangesAsync(It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync(UnsavedChangesChoice.Discard);
        await harness.Vm.CloseFileCommand.ExecuteAsync(null);

        Assert.Null(harness.Gateway.CurrentSave);
        harness.Dialogs.Verify(d => d.ShowUnsavedChangesAsync(It.IsAny<string>(), It.IsAny<string>(),
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()), Times.Once);

        // ...and a real save can be reopened afterwards.
        Assert.True(await harness.Gateway.LoadSaveFileAsync(savePath));
        Assert.True(harness.Vm.HasSave);
        Assert.False(harness.Vm.IsBlankWorkspace);
    }

    [Fact]
    public async Task OpenPokemonFile_WithARealSave_LoadsIntoItsEditorWithoutAPicker()
    {
        var harness = new Harness(choose: GameVersion.ZA, filePath: WriteEntity(CreatePk9(25), "pikachu.pk9"));
        harness.Gateway.OpenLoadedSave(new SAV9SV(), "main.sav");

        await harness.Vm.OpenPokemonFileCommand.ExecuteAsync(null);

        Assert.Null(harness.SuggestedTarget);
        Assert.False(harness.Vm.IsBlankWorkspace);
        Assert.Equal((ushort)25, harness.Vm.CurrentPokemonEditor!.PreparePKM().Species);
    }

    [AvaloniaFact]
    public void TargetPickerView_ShowsTheListAndActions()
    {
        var vm = new BlankWorkspaceTargetViewModel(GameVersion.ZA, "File: test.pa9");
        var view = new BlankWorkspaceTargetView { DataContext = vm };
        var window = new Window { Content = view, Width = 440, Height = 520 };
        window.Show();

        try
        {
            Dispatcher.UIThread.RunJobs();
            window.UpdateLayout();
            AvaloniaHeadlessPlatform.ForceRenderTimerTick();
            Dispatcher.UIThread.RunJobs();

            var list = view.GetVisualDescendants().OfType<ListBox>().Single();
            Assert.True(list.Bounds.Height > 100, $"Target list is only {list.Bounds.Height}px tall.");
            Assert.Same(vm.SelectedTarget, list.SelectedItem);

            var buttons = view.GetVisualDescendants().OfType<Button>()
                .Where(b => b.IsEffectivelyVisible && b.Command is not null).ToArray();
            Assert.Equal(2, buttons.Length);
            foreach (var button in buttons)
            {
                var bottom = button.TranslatePoint(new Point(0, button.Bounds.Height), view)!.Value.Y;
                Assert.True(bottom <= view.Bounds.Height, "Picker actions are clipped.");
            }

            bool closed = false;
            vm.CloseRequested = () => closed = true;
            vm.ConfirmCommand.Execute(null);
            Assert.True(closed);
            Assert.Equal(GameVersion.ZA, vm.Result);
        }
        finally
        {
            window.Content = null;
            window.Close();
        }
    }

    private static PK9 CreatePk9(ushort species)
    {
        var sav = new SAV9SV();
        var pk = (PK9)sav.BlankPKM;
        pk.Species = species;
        pk.CurrentLevel = 5;
        pk.Version = GameVersion.SL;
        pk.RefreshChecksum();
        return pk;
    }

    private string WriteEntity(PKM pk, string name)
    {
        var path = Path.Combine(_dir, name);
        var data = new byte[pk.SIZE_STORED];
        pk.WriteDecryptedDataStored(data);
        File.WriteAllBytes(path, data);
        return path;
    }

    private sealed class Harness
    {
        public FakeGateway Gateway { get; } = new();
        public Mock<IDialogService> Dialogs { get; } = new();
        public AppSettings Settings { get; } = new();
        public MainWindowViewModel Vm { get; }
        public GameVersion? SuggestedTarget { get; private set; }
        public string? SuggestedFileName { get; private set; }

        public Harness(GameVersion? choose, string? filePath = null, string? savePath = null)
        {
            Dialogs.Setup(d => d.OpenFileAsync(It.IsAny<string>(), It.IsAny<string[]?>())).ReturnsAsync(filePath);
            Dialogs.Setup(d => d.SaveFileAsync(It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<string[]?>()))
                .Callback<string, string?, string[]?>((_, name, _) => SuggestedFileName = name)
                .ReturnsAsync(savePath);

            var windows = new Mock<IWindowService>();
            windows.Setup(w => w.ShowDialogAsync(It.IsAny<object>(), It.IsAny<string>()))
                .Returns<object, string>((vm, _) =>
                {
                    if (vm is BlankWorkspaceTargetViewModel picker)
                    {
                        SuggestedTarget = picker.SelectedTarget?.Version;
                        if (choose is { } version)
                        {
                            picker.SelectedTarget = picker.Targets.Single(t => t.Version == version);
                            picker.ConfirmCommand.Execute(null);
                        }
                        else
                        {
                            picker.CancelCommand.Execute(null);
                        }
                    }
                    return Task.CompletedTask;
                });

            Vm = new MainWindowViewModel(
                Gateway,
                Dialogs.Object,
                windows.Object,
                new Mock<ISpriteRenderer>().Object,
                new Mock<ISlotService>().Object,
                new Mock<IClipboardService>().Object,
                new Mock<IQrCodeService>().Object,
                UpdateTestDoubles.Coordinator(),
                new Mock<ISaveBackupService>().Object,
                Settings,
                new FakeSettingsStore(),
                new Mock<IThemeService>().Object,
                new Mock<IUiDensityService>().Object,
                new UndoRedoService(),
                new LanguageService(),
                new Mock<IAutoLegalityService>().Object,
                new Mock<PKHeX.Application.Abstractions.LiveHex.ILiveHexService>().Object,
                new Mock<ILivingDexService>().Object,
                new Mock<PKHeX.Application.Abstractions.GiftRecords.IGiftRecordProvider>().Object);
        }
    }

    /// <summary>Gateway double that really parses saves from disk but never writes one.</summary>
    private sealed class FakeGateway : ISaveFileGateway
    {
        public SaveFile? CurrentSave { get; private set; }
        public bool HasSave => CurrentSave is not null;
        public string? CurrentPath { get; private set; }
        public int SaveCalls { get; private set; }

        public event Action<SaveFile?>? SaveFileChanged;

        public Task<bool> LoadSaveFileAsync(string path)
        {
            if (FileUtil.GetSupportedFile(path) is not SaveFile sav)
                return Task.FromResult(false);
            CurrentSave = sav;
            CurrentPath = path;
            SaveFileChanged?.Invoke(sav);
            return Task.FromResult(true);
        }

        public void OpenLoadedSave(SaveFile sav, string? path = null)
        {
            CurrentSave = sav;
            CurrentPath = path;
            SaveFileChanged?.Invoke(sav);
        }

        public Task<bool> SaveFileAsync(string? path = null)
        {
            SaveCalls++;
            if (CurrentSave is not null)
                CurrentSave.State.Edited = false;
            return Task.FromResult(CurrentPath is not null || path is not null);
        }

        public void CloseSave()
        {
            CurrentSave = null;
            CurrentPath = null;
            SaveFileChanged?.Invoke(null);
        }
    }
}
