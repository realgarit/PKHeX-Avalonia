using System;
using System.IO;
using System.Threading.Tasks;
using Avalonia.Headless.XUnit;
using PKHeX.Avalonia.Tests.Harness;
using PKHeX.Core;

namespace PKHeX.Avalonia.Tests;

public sealed class MainWindowFileCommandTests
{
    [AvaloniaFact]
    public async Task DumpBoxesCommand_WritesFromTheLoadedSaveAndCompletesOffTheCommandStack()
    {
        var root = Directory.CreateTempSubdirectory("pkhex-dump-command-");
        try
        {
            var save = BlankSaveFile.Get(GameVersion.SL);
            var pokemon = save.BlankPKM;
            pokemon.Species = 25;
            pokemon.RefreshChecksum();
            save.SetBoxSlotAtIndex(pokemon, 0, 0);

            using var app = new HeadlessAppFixture();
            app.LoadSaveInstance(save);
            app.Dialogs.OpenFolderResult = root.FullName;

            await app.ViewModel.DumpBoxesCommand.ExecuteAsync(null);
            app.Pump();

            Assert.False(app.ViewModel.IsBoxTransferRunning);
            Assert.NotEmpty(Directory.GetFiles(root.FullName));
            Assert.Empty(app.Dialogs.Errors);
            Assert.Contains("Dumped", Assert.Single(app.Dialogs.Infos).Message, StringComparison.Ordinal);
        }
        finally
        {
            try { root.Delete(recursive: true); }
            catch { /* best effort */ }
        }
    }

    [AvaloniaFact]
    public async Task LoadBoxesCommand_ImportsOnAWorkingCopyThenAppliesOnTheUiThread()
    {
        var root = Directory.CreateTempSubdirectory("pkhex-load-command-");
        try
        {
            var save = BlankSaveFile.Get(GameVersion.SL);
            var source = save.BlankPKM;
            source.Species = 25;
            source.RefreshChecksum();
            File.WriteAllBytes(Path.Combine(root.FullName, "pikachu.pk9"), source.Data.ToArray());

            using var app = new HeadlessAppFixture();
            app.LoadSaveInstance(save);
            app.Dialogs.OpenFolderResult = root.FullName;

            await app.ViewModel.LoadBoxesCommand.ExecuteAsync(null);
            app.Pump();

            Assert.False(app.ViewModel.IsBoxTransferRunning);
            Assert.Equal((ushort)25, save.GetBoxSlotAtIndex(0, 0).Species);
            Assert.True(save.State.Edited);
            Assert.Empty(app.Dialogs.Errors);
            Assert.Contains("Loaded", Assert.Single(app.Dialogs.Infos).Message, StringComparison.Ordinal);
        }
        finally
        {
            try { root.Delete(recursive: true); }
            catch { /* best effort */ }
        }
    }

    [AvaloniaFact]
    public async Task SaveFileChangedRaisedOffUiThread_MarshalsEditorBuildToUiThread()
    {
        using var app = new HeadlessAppFixture();
        var save = BlankSaveFile.Get(GameVersion.SL);

        // Native file pickers are allowed to resume their continuation off the UI thread. The
        // production gateway therefore may raise SaveFileChanged from a worker too.
        await Task.Run(() => app.Gateway.OpenLoadedSave(save, "worker.main"));

        app.PumpUntil(
            () => app.ViewModel.HasSave && app.ViewModel.BoxViewer is not null,
            because: "save-change notification to be applied on the Avalonia UI thread");

        Assert.Same(save, app.Save);
        Assert.Empty(app.Dialogs.Errors);
    }
}
