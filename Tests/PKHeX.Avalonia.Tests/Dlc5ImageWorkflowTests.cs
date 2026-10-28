using System.Buffers.Binary;
using Moq;
using PKHeX.Application.Abstractions;
using PKHeX.Application.Services;
using PKHeX.Avalonia.Services;
using PKHeX.Core;
using PKHeX.Presentation.ViewModels;

namespace PKHeX.Avalonia.Tests;

public sealed class Dlc5ImageWorkflowTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "pkhex-dlc5-" + Guid.NewGuid().ToString("N"));
    public Dlc5ImageWorkflowTests() => Directory.CreateDirectory(_directory);
    public void Dispose() => Directory.Delete(_directory, true);
    [Theory] [InlineData(false)] [InlineData(true)]
    public async Task CgearBinaryAndPng_ImportExport_KeepSaveRoundTripAndInvalidInputUntouched(bool sequel)
    {
        SAV5 save = sequel ? new SAV5B2W2() : new SAV5BW();
        var codec = new PngImageCodec(); var dialogs = new Mock<IDialogService>();
        var input = Path.Combine(_directory, "input.png"); var output = Path.Combine(_directory, "output.png");
        await File.WriteAllBytesAsync(input, codec.EncodePng(SkinImage5Tests.Solid(Color15Bit.GetColorExpand(31))), TestContext.Current.CancellationToken);
        dialogs.Setup(d => d.OpenFileAsync(It.IsAny<string>(), It.IsAny<string[]?>())).ReturnsAsync(input);
        dialogs.Setup(d => d.SaveFileAsync(It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<string[]?>())).ReturnsAsync(output);
        var vm = new DLC5EditorViewModel(save, dialogs.Object, codec);
        await vm.ImportCGearImageCommand.ExecuteAsync(null);
        Assert.NotNull(vm.CGearPng); Assert.True(save.State.Edited);
        var snapshot = save.Data.ToArray(); save.State.Edited = false;
        await vm.ExportCGearImageCommand.ExecuteAsync(null);
        Assert.Equal(vm.CGearPng, await File.ReadAllBytesAsync(output, TestContext.Current.CancellationToken));
        Assert.Equal(snapshot, save.Data.ToArray()); Assert.False(save.State.Edited);
        await vm.ImportCGearImageCommand.ExecuteAsync(null);
        Assert.Equal(snapshot, save.Data.ToArray()); Assert.False(save.State.Edited);
        await File.WriteAllBytesAsync(input, codec.EncodePng(new(1, 1, [0, 0, 0, 255])), TestContext.Current.CancellationToken);
        await vm.ImportCGearImageCommand.ExecuteAsync(null);
        Assert.Equal(snapshot, save.Data.ToArray()); Assert.NotEmpty(vm.ImageStatus);
        var binary = Path.Combine(_directory, sequel ? "skin.cgb" : "skin.psk");
        await File.WriteAllBytesAsync(binary, save.CGearSkinData.ToArray(), TestContext.Current.CancellationToken);
        dialogs.Setup(d => d.OpenFileAsync(It.IsAny<string>(), It.IsAny<string[]?>())).ReturnsAsync(binary);
        await vm.ImportCGearCommand.ExecuteAsync(null);
        SAV5 reload = sequel ? new SAV5B2W2(save.Write()) : new SAV5BW(save.Write());
        Assert.Equal(save.CGearSkinData.ToArray(), reload.CGearSkinData.ToArray());
    }
    [Fact]
    public async Task DexBothPngSurfaces_ExportAndReimport_WithoutNormalizingOtherBytes()
    {
        var save = new SAV5B2W2(); var bytes = new byte[PokeDexSkin5.SIZE];
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(0x6002), 31);
        bytes[0] = 0x01; bytes[^1] = 0xA7; bytes[0x6100] = 0xD5;
        save.SetPokeDexSkin(bytes); save.State.Edited = false;
        var before = save.Data.ToArray(); var dialogs = new Mock<IDialogService>(); var codec = new PngImageCodec();
        var path = Path.Combine(_directory, "skin.png");
        dialogs.Setup(d => d.SaveFileAsync(It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<string[]?>())).ReturnsAsync(path);
        dialogs.Setup(d => d.OpenFileAsync(It.IsAny<string>(), It.IsAny<string[]?>())).ReturnsAsync(path);
        var vm = new DLC5EditorViewModel(save, dialogs.Object, codec);
        Assert.NotNull(vm.DexForegroundPng); Assert.NotNull(vm.DexBackgroundPng); Assert.NotNull(vm.DexCompositePng);
        await vm.ExportDexForegroundCommand.ExecuteAsync(null); await vm.ImportDexForegroundCommand.ExecuteAsync(null);
        Assert.Equal(before, save.Data.ToArray()); Assert.False(save.State.Edited);
        await vm.ExportDexBackgroundCommand.ExecuteAsync(null); await vm.ImportDexBackgroundCommand.ExecuteAsync(null);
        Assert.Equal(before, save.Data.ToArray()); Assert.False(save.State.Edited);
    }
    [Fact]
    public async Task DecryptedVideoFile_HasExplicitTypeAndName_AndDoesNotMutateSource()
    {
        var save = new SAV5B2W2(); var bytes = new byte[BattleVideo5.SIZE];
        BinaryPrimitives.WriteUInt64LittleEndian(bytes.AsSpan(0xCC), LCRNG64.Mult);
        BinaryPrimitives.WriteUInt64LittleEndian(bytes.AsSpan(0xD4), LCRNG64.Add);
        var video = new BattleVideo5(bytes) { IsDecrypted = true }; video.RefreshChecksums(); var expected = bytes.ToArray(); video.Encrypt();
        save.SetBattleVideo(2, bytes); save.State.Edited = false; var before = save.Data.ToArray();
        var path = Path.Combine(_directory, "decrypted.bv5"); var dialogs = new Mock<IDialogService>();
        dialogs.Setup(d => d.SaveFileAsync(It.IsAny<string>(), "BattleVideo_02_decrypted.bv5", It.Is<string[]?>(types => types != null && types.SequenceEqual(new[] { "bv5" })))).ReturnsAsync(path);
        var vm = new DLC5EditorViewModel(save, dialogs.Object) { BattleVideoIndex = 2 };
        Assert.True(vm.CanExportDecrypted); await vm.ExportBattleVideoDecryptedCommand.ExecuteAsync(null);
        Assert.Equal(expected, await File.ReadAllBytesAsync(path, TestContext.Current.CancellationToken)); Assert.Equal(before, save.Data.ToArray()); Assert.False(save.State.Edited);
        vm.BattleVideoIndex = 1; Assert.False(vm.CanExportDecrypted);
    }
}
