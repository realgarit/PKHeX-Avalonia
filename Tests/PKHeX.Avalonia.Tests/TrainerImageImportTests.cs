using System.Buffers.Binary;
using Moq;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using PKHeX.Application.Abstractions;
using PKHeX.Application.Services;
using PKHeX.Avalonia.Services;
using PKHeX.Core;
using PKHeX.Presentation.ViewModels;

namespace PKHeX.Avalonia.Tests;

public class TrainerImageImportTests
{
    internal static readonly (uint Data, uint Width, uint Height)[] Keys =
    [
        (SaveBlockAccessor9ZA.KPictureCurrentData, SaveBlockAccessor9ZA.KPictureCurrentWidth, SaveBlockAccessor9ZA.KPictureCurrentHeight),
        (SaveBlockAccessor9ZA.KPictureSBCData, SaveBlockAccessor9ZA.KPictureSBCWidth, SaveBlockAccessor9ZA.KPictureSBCHeight),
        (SaveBlockAccessor9ZA.KPictureInitialData, SaveBlockAccessor9ZA.KPictureInitialWidth, SaveBlockAccessor9ZA.KPictureInitialHeight),
    ];
    internal static PixelImage Pixels(int width = 8, int height = 8, bool black = false)
    {
        var bytes = new byte[width * height * 4];
        for (int y = 0; y < height; y++) for (int x = 0; x < width; x++)
        {
            int p = (y * width + x) * 4;
            bytes[p] = black ? (byte)0 : (byte)(x * 255 / Math.Max(1, width - 1));
            bytes[p + 1] = black ? (byte)0 : (byte)(y * 255 / Math.Max(1, height - 1));
            bytes[p + 2] = black ? (byte)0 : (byte)200; bytes[p + 3] = 255;
        }
        return new(width, height, bytes);
    }
    internal static SAV9ZA Save()
    {
        var source = ZaTrainerWorkflowTests.CreateSave();
        var dimension = new byte[4]; BinaryPrimitives.WriteUInt32LittleEndian(dimension, 8);
        var texture = Dxt1ImageEncoder.Encode(Pixels(black: true)).Concat(Enumerable.Repeat((byte)0xA7, 13)).ToArray();
        var removed = Keys.SelectMany(k => new[] { k.Data, k.Width, k.Height }).ToHashSet();
        var blocks = source.AllBlocks.Where(b => !removed.Contains(b.Key)).ToList();
        foreach (var k in Keys)
        {
            blocks.Add(ZaTrainerWorkflowTests.ParseBlock(k.Data, SCTypeCode.Object, texture));
            blocks.Add(ZaTrainerWorkflowTests.ParseBlock(k.Width, SCTypeCode.UInt32, dimension));
            blocks.Add(ZaTrainerWorkflowTests.ParseBlock(k.Height, SCTypeCode.UInt32, dimension));
        }
        return new SAV9ZA(SwishCrypto.Encrypt(blocks.OrderBy(b => b.Key).ToArray()));
    }

    [Fact]
    public void EncoderPreservesChannelOrderOpaqueColorsAndBlockOrdering()
    {
        var pixels = new byte[8 * 4 * 4];
        for (int i = 0; i < 32; i++) { pixels[i * 4 + (i % 8 < 4 ? 2 : 0)] = 255; pixels[i * 4 + 3] = 255; }
        var encoded = Dxt1ImageEncoder.Encode(new(8, 4, pixels));
        Assert.Equal(16, encoded.Length);
        Assert.Equal(pixels, DXT1.Decompress(encoded, 8, 4));
    }

    [Fact]
    public void EncoderSupportsBinaryTransparencyAndDocumentedAlphaThreshold()
    {
        var input = Pixels(4, 4); byte[] alpha = [0, 127, 128, 254];
        for (int i = 0; i < 16; i++) input.Bgra[i * 4 + 3] = alpha[i % 4];
        var output = DXT1.Decompress(Dxt1ImageEncoder.Encode(input), 4, 4);
        for (int i = 0; i < 16; i++) Assert.Equal(alpha[i % 4] < 128 ? 0 : 255, output[i * 4 + 3]);
    }

    [Fact]
    public void SmoothImageCompressionRetainsUsefulDetailWithoutChangingInput()
    {
        var input = Pixels(128, 128); var before = input.Bgra.ToArray();
        var output = DXT1.Decompress(Dxt1ImageEncoder.Encode(input), 128, 128);
        double squared = 0;
        for (int i = 0; i < output.Length; i++) if (i % 4 != 3) squared += Math.Pow(output[i] - before[i], 2);
        Assert.True(Math.Sqrt(squared / (128 * 128 * 3)) < 10);
        Assert.Equal(before, input.Bgra);
    }

    [Theory] [InlineData(0)] [InlineData(1)] [InlineData(2)]
    public async Task EveryPictureImportsAsAStagedPreviewThenRoundtripsOnlyItsBlock(int index)
    {
        var save = Save(); var before = save.AllBlocks.ToDictionary(b => b.Key, b => b.Data.ToArray());
        var codec = new PngImageCodec(); var path = Path.GetTempFileName();
        var dialogs = new Mock<IDialogService>(); dialogs.Setup(d => d.OpenFileAsync(It.IsAny<string>(), It.IsAny<string[]?>())).ReturnsAsync(path);
        using var vm = new TrainerEditorViewModel(save, dialogs.Object, codec);
        try
        {
            await File.WriteAllBytesAsync(path, codec.EncodePng(Pixels()), TestContext.Current.CancellationToken);
            await vm.ImportZaImageCommand.ExecuteAsync(vm.ZaImages[index]);
            Assert.False(vm.HasZaError); Assert.True(vm.CanUndoZaCollection);
            foreach (var b in save.AllBlocks) Assert.Equal(before[b.Key], b.Data.ToArray());
            var preview = vm.ZaImages[index].Png!.ToArray();
            vm.SaveCommand.Execute(null);
            foreach (var b in save.AllBlocks)
                if (b.Key != Keys[index].Data) Assert.Equal(before[b.Key], b.Data.ToArray());
            var data = save.Blocks.GetBlock(Keys[index].Data).Data.ToArray();
            Assert.Equal(before[Keys[index].Data][32..], data[32..]);
            using var reopened = new TrainerEditorViewModel(new SAV9ZA(save.Write()), imageCodec: codec);
            Assert.Equal(codec.DecodePng(preview, 8, 8).Image!.Bgra, codec.DecodePng(reopened.ZaImages[index].Png!, 8, 8).Image!.Bgra);
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public void UndoResetAndIdenticalExportReimportPreserveOriginalBytesAndEditedFlag()
    {
        var save = Save(); save.State.Edited = false; var before = save.AllBlocks.ToDictionary(b => b.Key, b => b.Data.ToArray());
        var session = new ZaTrainerDataSession(save); var k = Keys[0];
        var original = new PixelImage(8, 8, DXT1.Decompress(before[k.Data], 8, 8));
        session.ImportImage(k.Data, k.Width, k.Height, original);
        Assert.False(session.CanUndo); Assert.True(session.TryCommit()); Assert.False(save.State.Edited);
        session.ImportImage(k.Data, k.Width, k.Height, Pixels()); session.Undo(); Assert.True(session.TryCommit());
        session.ImportImage(k.Data, k.Width, k.Height, Pixels()); session.Reset(); Assert.True(session.TryCommit());
        foreach (var b in save.AllBlocks) Assert.Equal(before[b.Key], b.Data.ToArray());
        Assert.False(save.State.Edited);
    }

    [Fact]
    public async Task ImportAndUndoRetainOtherPendingTrainerEdits()
    {
        var save = Save(); var original = save.Blocks.GetBlock(Keys[0].Data).Data.ToArray();
        var path = Path.GetTempFileName(); var codec = new PngImageCodec();
        var dialogs = new Mock<IDialogService>(); dialogs.Setup(d => d.OpenFileAsync(It.IsAny<string>(), It.IsAny<string[]?>())).ReturnsAsync(path);
        using var vm = new TrainerEditorViewModel(save, dialogs.Object, codec); vm.TrainerName = "Draft";
        try
        {
            await File.WriteAllBytesAsync(path, codec.EncodePng(Pixels()), TestContext.Current.CancellationToken);
            await vm.ImportZaImageCommand.ExecuteAsync(vm.ZaImages[0]);
            Assert.Equal("Draft", vm.TrainerName); Assert.NotEqual("Draft", save.OT);
            vm.UndoZaCollectionCommand.Execute(null); Assert.Equal("Draft", vm.TrainerName);
            vm.SaveCommand.Execute(null); Assert.Equal("Draft", save.OT);
            Assert.Equal(original, save.Blocks.GetBlock(Keys[0].Data).Data.ToArray());
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public void MetadataConflictRejectsAllWritesAndUnrelatedLiveChangesSurvive()
    {
        var save = Save(); var session = new ZaTrainerDataSession(save); var k = Keys[0];
        session.ImportImage(k.Data, k.Width, k.Height, Pixels());
        save.SetValue(k.Width, 12u); var before = save.AllBlocks.ToDictionary(b => b.Key, b => b.Data.ToArray());
        Assert.False(session.TryCommit());
        foreach (var b in save.AllBlocks) Assert.Equal(before[b.Key], b.Data.ToArray());
        save.SetValue(k.Width, 8u); save.Money = 123;
        Assert.True(session.TryCommit()); Assert.Equal(123u, save.Money);
    }

    [Fact]
    public void InvalidMetadataCapacityAndUnexpectedBlockKeysCannotStageWrites()
    {
        var save = Save(); var session = new ZaTrainerDataSession(save); var k = Keys[0];
        Assert.Throws<ArgumentException>(() => session.ImportImage(k.Data, k.Width, k.Height, Pixels(4, 4)));
        Assert.Throws<ArgumentException>(() => session.ImportCompressedImage(k.Data, k.Width, k.Height, 8, 8, new byte[31]));
        Assert.Throws<ArgumentException>(() => session.ImportImage(SaveBlockAccessor9ZA.KStreetName, k.Width, k.Height, Pixels()));
        Assert.False(session.CanUndo); Assert.False(save.State.Edited);
        save.SetValue(k.Width, 12u); save.SetValue(k.Height, 12u);
        var undersized = new ZaTrainerDataSession(save);
        Assert.Throws<ArgumentException>(() => undersized.ImportImage(k.Data, k.Width, k.Height, Pixels(12, 12)));
        Assert.False(undersized.CanUndo);
    }

    [Theory] [InlineData(false)] [InlineData(true)]
    public async Task MalformedOrWrongSizedPngDoesNotAlterDraftsOrTheSave(bool wrongSize)
    {
        var save = Save(); var before = save.AllBlocks.ToDictionary(b => b.Key, b => b.Data.ToArray());
        var codec = new PngImageCodec(); var path = Path.GetTempFileName();
        var dialogs = new Mock<IDialogService>(); dialogs.Setup(d => d.OpenFileAsync(It.IsAny<string>(), It.IsAny<string[]?>())).ReturnsAsync(path);
        using var vm = new TrainerEditorViewModel(save, dialogs.Object, codec); vm.TrainerName = "Draft";
        try
        {
            await File.WriteAllBytesAsync(path, wrongSize ? codec.EncodePng(Pixels(4, 4)) : [1, 2, 3], TestContext.Current.CancellationToken);
            await vm.ImportZaImageCommand.ExecuteAsync(vm.ZaImages[0]);
            Assert.True(vm.HasZaError); Assert.False(vm.CanUndoZaCollection); Assert.Equal("Draft", vm.TrainerName);
            foreach (var b in save.AllBlocks) Assert.Equal(before[b.Key], b.Data.ToArray());
        }
        finally { File.Delete(path); }
    }

    [AvaloniaFact]
    public async Task ImagePreparationRunsOffTheUiThreadAndResetDiscardsALateResult()
    {
        var save = Save(); var before = save.AllBlocks.ToDictionary(b => b.Key, b => b.Data.ToArray());
        var plain = new PngImageCodec(); using var codec = new BlockingCodec(plain);
        var dialogs = new Mock<IDialogService>(); var path = Path.GetTempFileName();
        dialogs.Setup(d => d.OpenFileAsync(It.IsAny<string>(), It.IsAny<string[]?>())).ReturnsAsync(path);
        using var vm = new TrainerEditorViewModel(save, dialogs.Object, codec);
        try
        {
            await File.WriteAllBytesAsync(path, plain.EncodePng(Pixels()));
            int uiThread = Environment.CurrentManagedThreadId;
            var pending = vm.ImportZaImageCommand.ExecuteAsync(vm.ZaImages[0]);
            await codec.Entered.Task.WaitAsync(TimeSpan.FromSeconds(10));
            Assert.True(Dispatcher.UIThread.CheckAccess()); Assert.NotEqual(uiThread, codec.ThreadId);
            vm.ResetCommand.Execute(null); codec.Release.Set(); await pending;
            Assert.False(vm.HasZaError); Assert.False(vm.CanUndoZaCollection);
            foreach (var b in save.AllBlocks) Assert.Equal(before[b.Key], b.Data.ToArray());
        }
        finally { codec.Release.Set(); File.Delete(path); }
    }

    private sealed class BlockingCodec(IImageCodec inner) : IImageCodec, IDisposable
    {
        public readonly TaskCompletionSource<bool> Entered = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public readonly ManualResetEventSlim Release = new();
        public int ThreadId;
        public ImageDecodeResult DecodePng(byte[] png, int width, int height)
        {
            ThreadId = Environment.CurrentManagedThreadId; Entered.TrySetResult(true);
            if (!Release.Wait(TimeSpan.FromSeconds(15))) throw new TimeoutException("Image preparation did not release.");
            return inner.DecodePng(png, width, height);
        }
        public byte[] EncodePng(PixelImage image) => inner.EncodePng(image);
        public void Dispose() => Release.Dispose();
    }

    [Theory] [InlineData(false)] [InlineData(true)]
    public async Task ResetOrCloseInvalidatesAPendingPickerWithoutChangingTheSave(bool close)
    {
        var save = Save(); var before = save.AllBlocks.ToDictionary(b => b.Key, b => b.Data.ToArray());
        var picker = new TaskCompletionSource<string?>(); var dialogs = new Mock<IDialogService>();
        dialogs.Setup(d => d.OpenFileAsync(It.IsAny<string>(), It.IsAny<string[]?>())).Returns(picker.Task);
        using var vm = new TrainerEditorViewModel(save, dialogs.Object, new PngImageCodec());
        var pending = vm.ImportZaImageCommand.ExecuteAsync(vm.ZaImages[0]);
        if (close) vm.Dispose(); else vm.ResetCommand.Execute(null);
        picker.SetResult("missing.png"); await pending;
        Assert.False(vm.HasZaError); Assert.False(vm.CanUndoZaCollection);
        foreach (var b in save.AllBlocks) Assert.Equal(before[b.Key], b.Data.ToArray());
    }
}
