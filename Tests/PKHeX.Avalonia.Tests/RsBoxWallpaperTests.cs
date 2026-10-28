using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.VisualTree;
using Moq;
using PKHeX.Application.Abstractions;
using PKHeX.Application.UseCases;
using PKHeX.Avalonia.Services;
using PKHeX.Avalonia.Tests.Harness;
using PKHeX.Avalonia.Views;
using PKHeX.Core;
using PKHeX.Presentation.Localization;
using PKHeX.Presentation.ViewModels;
using PKHeX.Testing;

namespace PKHeX.Avalonia.Tests;

public sealed class RsBoxWallpaperTests
{
    [Theory]
    [InlineData(false, false, false)] [InlineData(true, false, true)]
    [InlineData(false, true, true)] [InlineData(true, true, true)]
    public void AvailabilityMatchesEnabledOrStoredPayload_WithoutChangingSource(bool enabled, bool payload, bool available)
    {
        var save = RsBoxWallpaperFixture.Create(enabled, payload);
        var before = Snapshot(save);
        Assert.Equal(available, ReadRsBoxWallpaperUseCase.IsAvailable(save));
        Assert.Equal(available, new ReadRsBoxWallpaperUseCase().Execute(save) is not null);
        AssertUnchanged(save, before);
        Assert.False(ReadRsBoxWallpaperUseCase.IsAvailable(new SAV3E()));
        Assert.False(ReadRsBoxWallpaperUseCase.IsAvailable(null));
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task FullPngRoundTripHasCorrectCmprChannelsAndCorners_AndPreservesEverySourceBuffer(bool edited)
    {
        var save = RsBoxWallpaperFixture.Create(); save.State.Edited = edited;
        var before = Snapshot(save);
        var image = new ReadRsBoxWallpaperUseCase().Execute(save)!.Image;
        Assert.Equal(568, image.Width); Assert.Equal(216, image.Height);
        AssertPixel(image, 0, 0, [0, 0, 255, 255]); // red
        AssertPixel(image, 4, 0, [255, 0, 0, 255]); // blue
        AssertPixel(image, 0, 4, [0, 255, 0, 255]); // green
        AssertPixel(image, 4, 4, [0, 0, 0, 0]); // transparent
        AssertPixel(image, 567, 215, [0, 255, 255, 255]); // far yellow corner
        var codec = new PngImageCodec();
        var directory = Directory.CreateTempSubdirectory("pkhex-rsbox-").FullName;
        try
        {
            var path = Path.Combine(directory, "full.png");
            var dialogs = Dialogs(path);
            using var vm = new RsBoxWallpaperViewModel(save, codec, dialogs.Object);
            Assert.True(vm.CanExport); Assert.True(vm.IsEnabledInSave);
            await vm.ExportCommand.ExecuteAsync(null);
            var png = await File.ReadAllBytesAsync(path, TestContext.Current.CancellationToken);
            Assert.Equal(vm.PreviewPng, png);
            var reopened = codec.DecodePng(png, 568, 216);
            Assert.Equal(ImageDecodeError.None, reopened.Error);
            Assert.Equal(image.Bgra, reopened.Image!.Bgra);
            Assert.Equal(ImageDecodeError.WrongDimensions, codec.DecodePng(png, 284, 216).Error);
            AssertUnchanged(save, before);
            Assert.Single(Directory.GetFiles(directory));
        }
        finally { Directory.Delete(directory, true); }
    }

    [Fact]
    public async Task PickerCancellationDisposalAndCommandCancellationNeverWriteFilesOrSave()
    {
        var save = RsBoxWallpaperFixture.Create(enabled: false);
        var before = Snapshot(save);
        var directory = Directory.CreateTempSubdirectory("pkhex-rsbox-cancel-").FullName;
        try
        {
            var path = Path.Combine(directory, "unchanged.png");
            byte[] sentinel = [1, 2, 3, 4]; await File.WriteAllBytesAsync(path, sentinel, TestContext.Current.CancellationToken);
            using (var cancelled = new RsBoxWallpaperViewModel(save, new PngImageCodec(), Dialogs(null).Object))
            {
                Assert.True(cancelled.HasImage); Assert.False(cancelled.IsEnabledInSave);
                Assert.Equal(LocalizedStrings.Instance["RsBoxWallpaper_Disabled"], cancelled.Status);
                await cancelled.ExportCommand.ExecuteAsync(null);
                Assert.Equal(sentinel, await File.ReadAllBytesAsync(path, TestContext.Current.CancellationToken));
            }
            foreach (var dispose in new[] { true, false })
            {
                var pending = new TaskCompletionSource<string?>();
                var dialogs = new Mock<IDialogService>();
                dialogs.Setup(d => d.SaveFileAsync(It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<string[]?>())).Returns(pending.Task);
                using var vm = new RsBoxWallpaperViewModel(save, new PngImageCodec(), dialogs.Object);
                var operation = vm.ExportCommand.ExecuteAsync(null);
                if (dispose) vm.Dispose(); else vm.ExportCancelCommand.Execute(null);
                pending.SetResult(path); await operation;
                Assert.Equal(sentinel, await File.ReadAllBytesAsync(path, TestContext.Current.CancellationToken));
                dialogs.Verify(d => d.ShowErrorAsync(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
            }
            Assert.Single(Directory.GetFiles(directory));
            AssertUnchanged(save, before);
        }
        finally { Directory.Delete(directory, true); }
    }

    [Fact]
    public async Task FileErrorsAreReportedAndCancelledAtomicExportPreservesExistingFile()
    {
        var save = RsBoxWallpaperFixture.Create(); var before = Snapshot(save);
        var directory = Directory.CreateTempSubdirectory("pkhex-rsbox-error-").FullName;
        try
        {
            var dialogs = Dialogs(directory); // A directory cannot be replaced with a PNG.
            dialogs.Setup(d => d.ShowErrorAsync(It.IsAny<string>(), It.IsAny<string>())).Returns(Task.CompletedTask);
            using var vm = new RsBoxWallpaperViewModel(save, new PngImageCodec(), dialogs.Object);
            await vm.ExportCommand.ExecuteAsync(null);
            dialogs.Verify(d => d.ShowErrorAsync(LocalizedStrings.Instance["RsBoxWallpaper_ExportFailed"], It.IsAny<string>()), Times.Once);
            Assert.Empty(Directory.GetFiles(directory));
            var path = Path.Combine(directory, "existing.png"); await File.WriteAllBytesAsync(path, [1, 2, 3], TestContext.Current.CancellationToken);
            using var cancelled = new CancellationTokenSource(); cancelled.Cancel();
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => new ExportWallpaperPngUseCase().ExecuteAsync(path, vm.PreviewPng!, cancelled.Token));
            Assert.Equal(new byte[] { 1, 2, 3 }, await File.ReadAllBytesAsync(path, TestContext.Current.CancellationToken));
            AssertUnchanged(save, before);
        }
        finally { Directory.Delete(directory, true); }
    }

    [Fact]
    public void AbsentImageAndCodecErrorsDisableExportTruthfully()
    {
        var save = RsBoxWallpaperFixture.Create(false, false); var before = Snapshot(save);
        using (var absent = new RsBoxWallpaperViewModel(save, new PngImageCodec(), Mock.Of<IDialogService>()))
        {
            Assert.False(absent.HasImage); Assert.False(absent.ExportCommand.CanExecute(null));
            Assert.Equal(LocalizedStrings.Instance["RsBoxWallpaper_Unavailable"], absent.Status);
        }
        AssertUnchanged(save, before);
        save = RsBoxWallpaperFixture.Create(); before = Snapshot(save);
        var codec = new Mock<IImageCodec>(); codec.Setup(c => c.EncodePng(It.IsAny<PixelImage>())).Throws(new InvalidOperationException("test codec failure"));
        using var failed = new RsBoxWallpaperViewModel(save, codec.Object, Mock.Of<IDialogService>());
        Assert.False(failed.HasImage); Assert.False(failed.CanExport);
        Assert.Contains("test codec failure", failed.Status);
        AssertUnchanged(save, before);
    }

    [AvaloniaFact]
    public async Task CapabilityMenuCommandAndViewLocatorFollowActualRsBoxPayload()
    {
        using var app = new HeadlessAppFixture();
        var action = Assert.Single(app.ViewModel.ToolMenuGroups.SelectMany(group => group.Items),
            item => item.Title == LocalizedStrings.Instance["RsBoxWallpaper_Title"]);
        Assert.False(action.IsAvailable);
        app.LoadSaveInstance(new SAV3E()); Assert.False(action.IsAvailable);
        app.LoadSaveInstance(RsBoxWallpaperFixture.Create(false, false)); Assert.False(action.IsAvailable);
        var save = RsBoxWallpaperFixture.Create(false, true); var before = Snapshot(save);
        app.LoadSaveInstance(save);
        Assert.True(action.IsAvailable); Assert.True(app.ViewModel.OpenRsBoxWallpaperCommand.CanExecute(null));
        await app.ViewModel.OpenRsBoxWallpaperCommand.ExecuteAsync(null);
        using var vm = Assert.IsType<RsBoxWallpaperViewModel>(app.Windows.ShownDialogs.Last().ViewModel);
        var view = Assert.IsType<RsBoxWallpaperView>(ViewLocator.Build(vm));
        Assert.Same(vm, view.DataContext);
        app.Window.Content = view; app.Window.Width = 620; app.Window.Height = 430; app.Pump();
        Assert.NotNull(view.FindControl<Image>("WallpaperPreview")!.Source);
        Assert.True(view.FindControl<Button>("ExportWallpaper")!.IsEnabled);
        AssertUnchanged(save, before);
    }

    private static Mock<IDialogService> Dialogs(string? path)
    {
        var dialogs = new Mock<IDialogService>();
        dialogs.Setup(d => d.SaveFileAsync(It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<string[]?>())).ReturnsAsync(path);
        return dialogs;
    }
    private static void AssertPixel(PixelImage image, int x, int y, byte[] expected) =>
        Assert.Equal(expected, image.Bgra.AsSpan(((y * image.Width) + x) * 4, 4).ToArray());
    private sealed record SourceSnapshot(byte[] Data, byte[] BoxBuffer, bool Enabled, bool Edited);
    private static SourceSnapshot Snapshot(SAV3RSBox save) => new(save.Data.ToArray(), RsBoxWallpaperFixture.BoxBuffer(save), save.MyWallpaperEnabled, save.State.Edited);
    private static void AssertUnchanged(SAV3RSBox save, SourceSnapshot before)
    {
        Assert.Equal(before.Data, save.Data.ToArray()); Assert.Equal(before.BoxBuffer, RsBoxWallpaperFixture.BoxBuffer(save));
        Assert.Equal(before.Enabled, save.MyWallpaperEnabled); Assert.Equal(before.Edited, save.State.Edited);
    }
}
