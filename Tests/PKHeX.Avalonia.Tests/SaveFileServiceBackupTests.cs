using System;
using System.IO;
using System.Threading.Tasks;
using Moq;
using PKHeX.Core;

namespace PKHeX.Avalonia.Tests;

/// <summary>
/// Covers the automatic backup-on-write behaviour required by issue #135: saving over an existing
/// file snapshots the pre-overwrite bytes before the new data is written, gated by the existing
/// <c>Backup.BAKEnabled</c> setting.
/// </summary>
public sealed class SaveFileServiceBackupTests : IDisposable
{
    private readonly string _tempDir;

    public SaveFileServiceBackupTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "pkhex-savefileservice-tests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        try { Directory.Delete(_tempDir, recursive: true); }
        catch { /* best effort */ }
    }

    [Fact]
    public async Task SaveFileAsync_OverwritingExistingFile_CreatesTimestampedBackup()
    {
        var sav = SaveFileFactory.CreateBlankSave(GameVersion.W2);
        var path = Path.Combine(_tempDir, "game.sav");
        await File.WriteAllBytesAsync(path, sav.Write().ToArray());

        var backupService = new SaveBackupService(new FakeAppPaths());
        var settings = new AppSettings(); // Backup.BAKEnabled defaults to true (Core default)
        var service = new SaveFileService(backupService, settings);

        Assert.True(await service.LoadSaveFileAsync(path));
        service.CurrentSave!.Money = 555;
        Assert.True(await service.SaveFileAsync());

        var identity = SaveIdentity.Compute(path);
        var backups = backupService.ListBackups(identity);

        Assert.Single(backups.Entries);
        // The backup must hold the *pre-overwrite* bytes, not the newly written ones.
        var backedUpSav = (SaveFile)FileUtil.GetSupportedFile(backupService.ReadBackup(backups.Entries[0]), "game.sav")!;
        Assert.NotEqual(555u, backedUpSav.Money);
    }

    [Fact]
    public async Task SaveFileAsync_BackupDisabled_DoesNotCreateBackup()
    {
        var sav = SaveFileFactory.CreateBlankSave(GameVersion.W2);
        var path = Path.Combine(_tempDir, "game.sav");
        await File.WriteAllBytesAsync(path, sav.Write().ToArray());

        var backupService = new SaveBackupService(new FakeAppPaths());
        var settings = new AppSettings();
        settings.Backup.BAKEnabled = false;
        var service = new SaveFileService(backupService, settings);

        Assert.True(await service.LoadSaveFileAsync(path));
        Assert.True(await service.SaveFileAsync());

        var identity = SaveIdentity.Compute(path);
        Assert.Empty(backupService.ListBackups(identity).Entries);
    }

    [Fact]
    public async Task SaveFileAsync_ToNewPathWithNoExistingFile_DoesNotCreateBackup()
    {
        var sav = SaveFileFactory.CreateBlankSave(GameVersion.W2);
        var existingPath = Path.Combine(_tempDir, "game.sav");
        await File.WriteAllBytesAsync(existingPath, sav.Write().ToArray());

        var backupService = new SaveBackupService(new FakeAppPaths());
        var settings = new AppSettings();
        var service = new SaveFileService(backupService, settings);
        Assert.True(await service.LoadSaveFileAsync(existingPath));

        var newPath = Path.Combine(_tempDir, "brand-new.sav");
        Assert.True(await service.SaveFileAsync(newPath));

        var identity = SaveIdentity.Compute(newPath);
        Assert.Empty(backupService.ListBackups(identity).Entries);
    }

    [Fact]
    public async Task SaveFileAsync_RepeatedSaves_PrunesAccordingToRetentionSetting()
    {
        var sav = SaveFileFactory.CreateBlankSave(GameVersion.W2);
        var path = Path.Combine(_tempDir, "game.sav");
        await File.WriteAllBytesAsync(path, sav.Write().ToArray());

        var backupService = new SaveBackupService(new FakeAppPaths());
        var settings = new AppSettings();
        settings.SaveBackup.MaxBackupsPerSave = 2;
        var service = new SaveFileService(backupService, settings);
        Assert.True(await service.LoadSaveFileAsync(path));

        for (int i = 0; i < 4; i++)
        {
            service.CurrentSave!.Money = (uint)(i + 1);
            Assert.True(await service.SaveFileAsync());
        }

        var identity = SaveIdentity.Compute(path);
        Assert.Equal(2, backupService.ListBackups(identity).Entries.Count);
    }

    [Fact]
    public async Task SaveFileAsync_SnapshotsTheOriginalSaveBeforeAConcurrentLoad()
    {
        var original = SaveFileFactory.CreateBlankSave(GameVersion.W2);
        original.Money = 111;
        var replacement = SaveFileFactory.CreateBlankSave(GameVersion.W2);
        replacement.Money = 222;
        var originalPath = Path.Combine(_tempDir, "original.sav");
        var replacementPath = Path.Combine(_tempDir, "replacement.sav");
        await File.WriteAllBytesAsync(originalPath, original.Write().ToArray());
        await File.WriteAllBytesAsync(replacementPath, replacement.Write().ToArray());

        var backupStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseBackup = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var backupService = new Mock<ISaveBackupService>();
        backupService
            .Setup(s => s.CreateBackup(It.IsAny<string>(), It.IsAny<byte[]>(), It.IsAny<int>()))
            .Callback(() =>
            {
                backupStarted.TrySetResult();
                releaseBackup.Task.GetAwaiter().GetResult();
            })
            .Returns((SaveBackupEntry?)null);
        var service = new SaveFileService(backupService.Object, new AppSettings());

        Assert.True(await service.LoadSaveFileAsync(originalPath));
        service.CurrentSave!.Money = 333;
        var saveTask = service.SaveFileAsync();
        await backupStarted.Task;

        var loadTask = service.LoadSaveFileAsync(replacementPath);
        Assert.False(loadTask.IsCompleted);

        releaseBackup.SetResult();
        Assert.True(await saveTask);
        Assert.True(await loadTask);

        var writtenOriginal = Assert.IsAssignableFrom<SAV5>(FileUtil.GetSupportedFile(originalPath));
        Assert.Equal(333u, writtenOriginal.Money);
        Assert.Equal(222u, service.CurrentSave!.Money);
        Assert.Equal(replacementPath, service.CurrentPath);
    }
}
