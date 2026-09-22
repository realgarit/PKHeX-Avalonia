using Moq;
using PKHeX.Application;
using PKHeX.Application.Abstractions;
using PKHeX.Avalonia.Tests.Fixtures;
using PKHeX.Core;
using PKHeX.Presentation.ViewModels;

namespace PKHeX.Avalonia.Tests;

public class FolderListTests
{
    [Fact]
    public void FilterMatchesDisplayedSaveColumnsAndCanBeCleared()
    {
        var path = Path.Combine(SaveFileFixture.FindSaveFilesPath()!, "gen9_violet.main");
        var save = Assert.IsType<SAV9SV>(SaveFileFixture.LoadSave(path));
        var vm = new FolderListViewModel(
            Mock.Of<ISaveFileGateway>(),
            new AppSettings(),
            Mock.Of<IDialogService>(),
            loadOnConstruct: false);
        vm.RecentSaves.Add(new SaveFilePreviewViewModel(save, path));
        vm.BackupSaves.Add(new SaveFilePreviewViewModel(save, path.Replace("violet", "backup", StringComparison.OrdinalIgnoreCase)));

        vm.FilterText = "violet";
        Assert.Single(vm.FilteredRecentSaves);
        Assert.Empty(vm.FilteredBackupSaves);

        vm.FilterText = string.Empty;
        Assert.Single(vm.FilteredRecentSaves);
        Assert.Single(vm.FilteredBackupSaves);
    }

    [Fact]
    public void PreviewPopulatesReliablePlayTimeAndPathValues()
    {
        var path = Path.Combine(SaveFileFixture.FindSaveFilesPath()!, "gen9_violet.main");
        var save = Assert.IsType<SAV9SV>(SaveFileFixture.LoadSave(path));

        var preview = new SaveFilePreviewViewModel(save, path);

        Assert.Equal(path, preview.FilePath);
        Assert.Equal(Path.GetFileName(path), preview.FileName);
        Assert.Matches("^\\d{2}:\\d{2}:\\d{2}$", preview.PlayTime);
    }
}
