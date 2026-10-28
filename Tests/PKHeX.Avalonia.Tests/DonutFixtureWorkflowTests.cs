using System.Buffers.Binary;
using Moq;
using PKHeX.Avalonia.Tests.Fixtures;
using PKHeX.Core;
using PKHeX.Presentation.ViewModels;

namespace PKHeX.Avalonia.Tests;

public class DonutFixtureWorkflowTests
{
    internal static SAV9ZA CreateSave()
    {
        var file = Path.Combine(SaveFileFixture.FindSaveFilesPath()!, "gen9a_legendsza.main");
        var source = Assert.IsType<SAV9ZA>(SaveUtil.GetSaveFile(File.ReadAllBytes(file)));
        const uint key = 0xBE007476;
        // The committed pre-unlock fixture lacks this optional block. Add a synthetic object
        // through Core's public parser; keep every existing fixture block byte-for-byte.
        var xor = new SCXorShift32(key); int length = DonutPocket9a.MaxCount * Donut9a.Size;
        var encoded = new byte[length + 5]; encoded[0] = (byte)((byte)SCTypeCode.Object ^ xor.Next());
        BinaryPrimitives.WriteInt32LittleEndian(encoded.AsSpan(1), length ^ xor.Next32());
        for (int i = 0; i < length; i++) encoded[i + 5] = (byte)xor.Next();
        int offset = 0; var block = SCBlock.ReadFromOffset(encoded, key, ref offset);
        var blocks = source.AllBlocks.Where(existing => existing.Key != key).Append(block).OrderBy(existing => existing.Key).ToArray();
        return new SAV9ZA(SwishCrypto.Encrypt(blocks));
    }
    [Fact]
    public async Task FixtureDirectEditsInvalidHexImportCancelAndSavePreserveOtherBlocks()
    {
        var save = CreateSave(); var record = save.Donuts.GetDonut(0);
        record.MillisecondsSince1970 = 1; record.Flavor0 = ulong.MaxValue; record.Reserved = 12345;
        save.State.Edited = false;
        var before = save.AllBlocks.ToDictionary(block => block.Key, block => block.Data.ToArray());
        using (var cancelled = new DonutEditorViewModel(save))
        {
            cancelled.SelectedDonut!.Stars = 5; cancelled.CancelCommand.Execute(null);
        }
        foreach (var block in save.AllBlocks) Assert.Equal(before[block.Key], block.Data.ToArray());
        var path = Path.GetTempFileName();
        try
        {
            using var vm = new DonutEditorViewModel(save);
            var row = vm.SelectedDonut!; row.Berry1 = 65000; row.DonutType = 65001;
            row.Flavor0Text = "invalid"; Assert.False(vm.CanSave);
            Assert.Equal(ulong.MaxValue, row.Flavor0);
            row.Flavor0Text = "FEDCBA9876543210"; Assert.True(vm.CanSave);
            await File.WriteAllBytesAsync(path, new byte[Donut9a.Size + 1], TestContext.Current.CancellationToken);
            await vm.ImportPathAsync(path); Assert.NotEmpty(vm.Error);
            foreach (var block in save.AllBlocks) Assert.Equal(before[block.Key], block.Data.ToArray());
            vm.SaveCommand.Execute(null);
            var roundtrip = new SAV9ZA(save.Write());
            Assert.Equal(65000, roundtrip.Donuts.GetDonut(0).Berry1);
            Assert.Equal(65001, roundtrip.Donuts.GetDonut(0).Donut);
            Assert.Equal(0xFEDCBA9876543210ul, roundtrip.Donuts.GetDonut(0).Flavor0);
            Assert.Equal(12345ul, roundtrip.Donuts.GetDonut(0).Reserved);
            foreach (var block in roundtrip.AllBlocks.Where(block => block.Key != 0xBE007476)) Assert.Equal(before[block.Key], block.Data.ToArray());
        }
        finally { File.Delete(path); }
    }

    [Theory] [InlineData(0)] [InlineData(1)] [InlineData(2)] [InlineData(3)] [InlineData(4)]
    public async Task FixtureBackedBulkActionsStayStagedUndoAndCommitRoundtrip(int action)
    {
        var save = CreateSave(); var donut = save.Donuts.GetDonut(3); donut.MillisecondsSince1970 = 1;
        donut.Stars = 2; donut.Berry1 = 170; donut.Flavor0 = ulong.MaxValue; donut.Reserved = 12345;
        save.State.Edited = false;
        var before = save.AllBlocks.ToDictionary(block => block.Key, block => block.Data.ToArray());
        var dialog = new Mock<IDialogService>();
        dialog.Setup(d => d.ShowConfirmationAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>())).ReturnsAsync(true);
        using var vm = new DonutEditorViewModel(save, dialog.Object);
        vm.SelectedDonut = vm.Donuts[3]; vm.GenerateStart = 0; vm.GenerateEnd = 3;
        var command = action switch { 0 => vm.RandomizeAllCommand, 1 => vm.CloneCurrentCommand, 2 => vm.ShinyAssortmentCommand, 3 => vm.CompressCommand, _ => vm.GenerateCommand };
        await command.ExecuteAsync(null);
        foreach (var block in save.AllBlocks) Assert.Equal(before[block.Key], block.Data.ToArray());
        vm.UndoCommand.Execute(null);
        Assert.Equal(ulong.MaxValue, vm.Donuts[3].Flavor0);
        await command.ExecuteAsync(null); vm.SaveCommand.Execute(null);
        Assert.True(save.State.Edited);
        var roundtrip = new SAV9ZA(save.Write());
        Assert.True(DonutDataSession.IsOccupied(roundtrip.Donuts.GetDonut(0)));
        foreach (var block in roundtrip.AllBlocks.Where(block => block.Key != 0xBE007476)) Assert.Equal(before[block.Key], block.Data.ToArray());
    }
}
