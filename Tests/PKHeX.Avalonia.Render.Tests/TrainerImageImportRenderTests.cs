using System.Buffers.Binary;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Media.Imaging;
using Avalonia.VisualTree;
using Moq;
using PKHeX.Application.Abstractions;
using PKHeX.Application.Services;
using PKHeX.Avalonia.Services;
using PKHeX.Avalonia.Views;
using PKHeX.Core;
using PKHeX.Presentation.Localization;
using PKHeX.Presentation.ViewModels;

namespace PKHeX.Avalonia.Render.Tests;

public class TrainerImageImportRenderTests
{
    [AvaloniaTheory] [InlineData("en", 900, 600)] [InlineData("de", 620, 420)]
    [InlineData("ja", 620, 420)] [InlineData("pt-BR", 620, 420)]
    public async Task ImportedCompressedPreviewAndActionsRenderBeforeApplying(string language, int width, int height)
    {
        string previous = LocalizedStrings.Instance.CurrentLanguage;
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Tests/savefiles/gen9a_legendsza.main"))) directory = directory.Parent;
        Assert.NotNull(directory);
        var original = Assert.IsType<SAV9ZA>(SaveUtil.GetSaveFile(File.ReadAllBytes(Path.Combine(directory.FullName, "Tests/savefiles/gen9a_legendsza.main"))));
        const int imageWidth = 512, imageHeight = 256;
        var pixels = new byte[imageWidth * imageHeight * 4];
        for (int y = 0; y < imageHeight; y++) for (int x = 0; x < imageWidth; x++)
        {
            int p = (y * imageWidth + x) * 4; pixels[p] = (byte)(x * 255 / (imageWidth - 1));
            pixels[p + 1] = (byte)y; pixels[p + 2] = 200; pixels[p + 3] = 255;
        }
        var w = new byte[4]; var h = new byte[4]; BinaryPrimitives.WriteUInt32LittleEndian(w, imageWidth); BinaryPrimitives.WriteUInt32LittleEndian(h, imageHeight);
        var blocks = new[]
        {
            ZaTrainerRenderTests.Parse(SaveBlockAccessor9ZA.KPictureCurrentData, SCTypeCode.Object, new byte[imageWidth * imageHeight / 2]),
            ZaTrainerRenderTests.Parse(SaveBlockAccessor9ZA.KPictureCurrentWidth, SCTypeCode.UInt32, w),
            ZaTrainerRenderTests.Parse(SaveBlockAccessor9ZA.KPictureCurrentHeight, SCTypeCode.UInt32, h),
        };
        var keys = blocks.Select(b => b.Key).ToHashSet();
        var save = new SAV9ZA(SwishCrypto.Encrypt(original.AllBlocks.Where(b => !keys.Contains(b.Key)).Concat(blocks).OrderBy(b => b.Key).ToArray()));
        var before = save.AllBlocks.ToDictionary(b => b.Key, b => b.Data.ToArray());
        var codec = new PngImageCodec(); var path = Path.GetTempFileName(); Window? window = null;
        var dialogs = new Mock<IDialogService>(); dialogs.Setup(d => d.OpenFileAsync(It.IsAny<string>(), It.IsAny<string[]?>())).ReturnsAsync(path);
        using var vm = new TrainerEditorViewModel(save, dialogs.Object, codec);
        try
        {
            LocalizedStrings.Instance.SetLanguage(language); vm.RefreshLanguage();
            await File.WriteAllBytesAsync(path, codec.EncodePng(new(imageWidth, imageHeight, pixels)));
            await vm.ImportZaImageCommand.ExecuteAsync(vm.ZaImages[0]);
            Assert.False(vm.HasZaError); Assert.True(vm.CanUndoZaCollection);
            var view = new TrainerZaWorkspace { DataContext = vm };
            view.FindControl<TabControl>("TrainerZaSections")!.SelectedIndex = 2;
            window = new Window { Content = view, Width = width, Height = height }; window.Show();
            await PKHeX.Testing.HeadlessRenderSettling.Settle(window);
            var import = view.GetVisualDescendants().OfType<Button>().First(b => ReferenceEquals(b.Command, vm.ImportZaImageCommand));
            var importPoint = import.TranslatePoint(default, view)!.Value;
            Assert.True(importPoint.Y >= 0 && importPoint.Y + import.Bounds.Height <= view.Bounds.Height);
            var apply = view.FindControl<Button>("TrainerZaApply")!; var applyPoint = apply.TranslatePoint(default, view)!.Value;
            Assert.True(applyPoint.Y >= 0 && applyPoint.Y + apply.Bounds.Height <= view.Bounds.Height);
            using var frame = new RenderTargetBitmap(new PixelSize(width, height)); frame.Render(window);
            if (Environment.GetEnvironmentVariable("PKHEX_HEADLESS_CAPTURE") == "1" && Environment.GetEnvironmentVariable("PKHEX_HEADLESS_CAPTURE_DIR") is { } capture)
            { Directory.CreateDirectory(capture); frame.Save(Path.Combine(capture, $"trainer-image-import-{language}.png"), PngBitmapEncoderOptions.Default); }
            foreach (var b in save.AllBlocks) Assert.Equal(before[b.Key], b.Data.ToArray());
        }
        finally { window?.Close(); File.Delete(path); LocalizedStrings.Instance.SetLanguage(previous); }
    }
}
