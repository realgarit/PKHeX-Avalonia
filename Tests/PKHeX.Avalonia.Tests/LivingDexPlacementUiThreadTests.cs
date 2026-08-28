using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Microsoft.Extensions.DependencyInjection;
using PKHeX.Application.UseCases;
using PKHeX.Avalonia.Tests.Harness;
using PKHeX.Core;
using PKHeX.Presentation.ViewModels;
using Xunit;

namespace PKHeX.Avalonia.Tests;

/// <summary>
/// Regression coverage for issue #262 - "Living Dex placement failing with an invalid-thread error".
///
/// <para>
/// The shipped bug: <c>LivingDexGeneratorViewModel.GenerateAsync</c> ran <b>placement</b> (not just
/// generation) inside <c>Task.Run</c>. Placement mutates the save and closes an
/// <see cref="UndoRedoService"/> batch, whose <c>StateChanged</c> event reaches
/// <c>MainWindowViewModel</c>'s <c>UndoCommand.NotifyCanExecuteChanged()</c>. The Undo/Redo
/// <c>MenuItem</c>s in <c>MainWindow.axaml</c> are direct XAML children, so they are attached to the
/// logical tree - and subscribed to <c>CanExecuteChanged</c> - from the moment the window loads, with
/// no menu ever opened. Raising that event off the UI thread makes Avalonia's
/// <c>Dispatcher.VerifyAccess()</c> throw <c>InvalidOperationException: Call from invalid thread</c>.
/// </para>
///
/// <para>
/// Why the nine existing <see cref="LivingDexTests"/> missed it: every one of them calls
/// <see cref="LivingDexPlacementUseCase.TryPlace"/> synchronously against a bare
/// <see cref="UndoRedoService"/> that has <b>no subscribers</b>, so no thread boundary and no UI-bound
/// listener is ever involved. These tests instead drive the real command through
/// <see cref="HeadlessAppFixture"/> - the real composition root, the real <c>MainWindow</c>, and
/// therefore the real <c>MenuItem</c> command subscribers - with only <see cref="ILivingDexService"/>
/// stubbed so the run is fast.
/// </para>
/// </summary>
public class LivingDexPlacementUiThreadTests
{
    /// <summary>
    /// Stands in for the real (minutes-long) generator. Also records whether it was invoked off the UI
    /// thread, so a "fix" that simply made the whole flow synchronous would fail the assertion.
    /// </summary>
    private sealed class StubLivingDexService(IReadOnlyList<PKM> pokemon) : ILivingDexService
    {
        public bool RanOffUiThread { get; private set; }

        public LivingDexGenerationResult Generate(
            SaveFile sav,
            LivingDexOptions options,
            IProgress<LivingDexGenerationProgress>? progress = null,
            CancellationToken cancellationToken = default,
            int? maxSpeciesId = null)
        {
            RanOffUiThread = !Dispatcher.UIThread.CheckAccess();
            progress?.Report(new LivingDexGenerationProgress(pokemon.Count, pokemon.Count));
            return LivingDexGenerationResult.Ok(pokemon, []);
        }
    }

    /// <summary>A cheap, format-correct entity for <paramref name="sav"/>. Placement performs no legality work.</summary>
    private static PKM MakeEntity(SaveFile sav, ushort species)
    {
        var pk = sav.BlankPKM;
        pk.Species = species;
        return pk;
    }

    private static LivingDexGeneratorViewModel OpenGenerator(HeadlessAppFixture app)
    {
        app.ViewModel.OpenLivingDexGeneratorCommand.Execute(null);
        app.Pump();
        return app.Windows.ShownTools.Select(t => t.ViewModel).OfType<LivingDexGeneratorViewModel>().Single();
    }

    // -------------------------------------------------------------------
    // The reported failure: generation succeeds, placement dies on the thread boundary
    // -------------------------------------------------------------------

    [AvaloniaFact]
    public async Task GenerateCommand_PlacesTheDex_WithoutAnInvalidThreadError()
    {
        var sav = BlankSaveFile.Get(GameVersion.SW);
        var pokemon = new List<PKM> { MakeEntity(sav, 25), MakeEntity(sav, 52), MakeEntity(sav, 133) };
        var service = new StubLivingDexService(pokemon);

        using var app = new HeadlessAppFixture(svc => svc.AddSingleton<ILivingDexService>(service));
        app.LoadSaveInstance(sav);

        var vm = OpenGenerator(app);
        var refreshes = 0;
        vm.BoxesUpdated += () => refreshes++;

        await vm.GenerateCommand.ExecuteAsync(null);
        app.Pump();

        Assert.DoesNotContain("invalid thread", vm.StatusMessage, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Unexpected error", vm.StatusMessage, StringComparison.Ordinal);
        Assert.Contains("Placed 3", vm.StatusMessage, StringComparison.Ordinal);

        // The Pokemon actually reached the boxes...
        for (var i = 0; i < pokemon.Count; i++)
            Assert.Equal(pokemon[i].Species, sav.GetBoxSlotAtIndex(0, i).Species);

        // ...the host was told exactly once to refresh, so the box view is not left stale...
        Assert.Equal(1, refreshes);

        // ...the whole fill is one undoable operation...
        var undoRedo = app.Services.GetRequiredService<UndoRedoService>();
        Assert.True(undoRedo.CanUndo);

        // ...and generation itself is still offloaded (the expensive part must not move to the UI thread).
        Assert.True(service.RanOffUiThread, "Living Dex generation must keep running off the UI thread.");
    }

    // -------------------------------------------------------------------
    // The second half of #262: a mid-placement failure left the save silently mutated
    // -------------------------------------------------------------------

    [AvaloniaFact]
    public async Task GenerateCommand_WhenPlacementFailsPartway_RefreshesTheBoxesAndSaysSo()
    {
        var sav = BlankSaveFile.Get(GameVersion.SW);
        // The third entity is the wrong PKM format for this save, so SaveFile.SetBoxSlot throws *after*
        // the first two slots have already been written - the exact partial-write shape #262 hid.
        var written = new List<PKM> { MakeEntity(sav, 25), MakeEntity(sav, 52) };
        var pokemon = new List<PKM>(written) { new PK9 { Species = 133 } };
        var service = new StubLivingDexService(pokemon);

        using var app = new HeadlessAppFixture(svc => svc.AddSingleton<ILivingDexService>(service));
        app.LoadSaveInstance(sav);

        var vm = OpenGenerator(app);
        var refreshes = 0;
        vm.BoxesUpdated += () => refreshes++;

        await vm.GenerateCommand.ExecuteAsync(null);
        app.Pump();

        // The boxes really were modified, so the UI must be refreshed rather than left showing empties.
        Assert.Equal(written[0].Species, sav.GetBoxSlotAtIndex(0, 0).Species);
        Assert.Equal(written[1].Species, sav.GetBoxSlotAtIndex(0, 1).Species);
        Assert.Equal(1, refreshes);

        // The message must admit the save was touched instead of implying nothing happened.
        Assert.Contains("partway", vm.StatusMessage, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Undo", vm.StatusMessage, StringComparison.OrdinalIgnoreCase);

        // The partial writes stay undoable: the batch is closed even though the loop threw.
        var undoRedo = app.Services.GetRequiredService<UndoRedoService>();
        Assert.True(undoRedo.CanUndo);
        undoRedo.Undo();
        Assert.Equal(0, sav.GetBoxSlotAtIndex(0, 0).Species);
        Assert.Equal(0, sav.GetBoxSlotAtIndex(0, 1).Species);
    }

    // -------------------------------------------------------------------
    // Defense in depth: an off-thread StateChanged must never crash the shell again
    // -------------------------------------------------------------------

    [AvaloniaFact]
    public async Task UndoRedoStateChanged_RaisedOffTheUiThread_DoesNotCrashTheShell()
    {
        using var app = new HeadlessAppFixture();
        var sav = BlankSaveFile.Get(GameVersion.SW);
        app.LoadSaveInstance(sav);

        var undoRedo = app.Services.GetRequiredService<UndoRedoService>();

        // AddChange -> SetChangeCount -> StateChanged -> MainWindowViewModel's Undo/RedoCommand
        // NotifyCanExecuteChanged -> the real MenuItem command subscribers.
        var thrown = await Record.ExceptionAsync(
            () => Task.Run(() => undoRedo.AddChange(new SlotInfoBox(0, 0, sav))));

        Assert.Null(thrown);
        app.Pump();
        Assert.True(undoRedo.CanUndo);
    }

    // -------------------------------------------------------------------
    // Dirty-state bookkeeping
    // -------------------------------------------------------------------

    [Fact]
    public void TryPlace_MarksTheSaveEdited()
    {
        var sav = BlankSaveFile.Get(GameVersion.SW);
        Assert.False(sav.State.Edited);

        var result = new LivingDexPlacementUseCase().TryPlace(sav, [MakeEntity(sav, 25)], startBox: 0);

        Assert.Equal(LivingDexPlacementStatus.Success, result.Status);
        Assert.True(sav.State.Edited);
    }

    [Fact]
    public void TryPlace_WhenRefused_LeavesTheSaveUnedited()
    {
        var sav = BlankSaveFile.Get(GameVersion.SW);
        sav.SetBoxSlotAtIndex(MakeEntity(sav, 25), 0, 1); // break the contiguous run at slot 1
        sav.State.Edited = false;

        var result = new LivingDexPlacementUseCase()
            .TryPlace(sav, [MakeEntity(sav, 52), MakeEntity(sav, 133)], startBox: 0);

        Assert.Equal(LivingDexPlacementStatus.InsufficientSpace, result.Status);
        Assert.False(sav.State.Edited);
    }
}
