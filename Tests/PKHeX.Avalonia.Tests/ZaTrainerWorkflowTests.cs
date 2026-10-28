using Moq;
using System.Buffers.Binary;
using PKHeX.Avalonia.Tests.Fixtures;
using PKHeX.Avalonia.Services;
using PKHeX.Core;
using PKHeX.Presentation.ViewModels;

namespace PKHeX.Avalonia.Tests;

public class ZaTrainerWorkflowTests
{
    internal static SAV9ZA CreateSave()
    {
        var save = Assert.IsType<SAV9ZA>(SaveUtil.GetSaveFile(File.ReadAllBytes(
            Path.Combine(SaveFileFixture.FindSaveFilesPath()!, "gen9a_legendsza.main"))));
        save.SetValue(SaveBlockAccessor9ZA.KSaveRevision, 1ul);
        // This pre-unlock corpus save lacks both optional DLC blocks. Build an
        // explicitly synthetic variant through public Core parsing, retaining
        // every original block; this is test data, not a production repair.
        var survey = ParseBlock(SaveBlockAccessor9ZA.KHyperspaceSurveyPoints, SCTypeCode.UInt32, new byte[4]);
        var street = ParseBlock(SaveBlockAccessor9ZA.KStreetName, SCTypeCode.Object, new byte[0x26]);
        var keys = new[] { survey.Key, street.Key };
        var blocks = save.AllBlocks.Where(block => !keys.Contains(block.Key)).Append(survey).Append(street).OrderBy(block => block.Key).ToArray();
        return new SAV9ZA(SwishCrypto.Encrypt(blocks));
    }

    internal static SCBlock ParseBlock(uint key, SCTypeCode type, byte[] data)
    {
        var xor = new SCXorShift32(key); bool objectType = type == SCTypeCode.Object;
        int start = objectType ? 5 : 1;
        var encoded = new byte[start + data.Length]; encoded[0] = (byte)((byte)type ^ xor.Next());
        if (objectType) BinaryPrimitives.WriteInt32LittleEndian(encoded.AsSpan(1), data.Length ^ xor.Next32());
        for (int i = 0; i < data.Length; i++) encoded[start + i] = (byte)(data[i] ^ xor.Next());
        int offset = 0; return SCBlock.ReadFromOffset(encoded, key, ref offset);
    }

    [Fact]
    public void MapDateAndDlcFieldsRoundtripAndOnlyExpectedBlocksChange()
    {
        var save = CreateSave(); var expected = (SAV9ZA)save.Clone();
        var before = save.AllBlocks.ToDictionary(block => block.Key, block => block.Data.ToArray());
        var vm = new TrainerEditorViewModel(save);
        Assert.True(vm.HasHyperspacePoints); Assert.True(vm.HasStreetName);
        vm.ZaMap = "test_map"; vm.X = 11.5; vm.Y = -3.25; vm.Z = 50; vm.ZaRotation = 135;
        vm.ZaLastSavedDate = new DateTimeOffset(2026, 10, 2, 0, 0, 0, TimeSpan.Zero);
        vm.ZaLastSavedTime = new TimeSpan(13, 25, 0);
        vm.ZaLastSavedSecond = 15;
        vm.HyperspacePoints = 12345; vm.StreetName = "Test Street";
        foreach (var block in save.AllBlocks) Assert.Equal(before[block.Key], block.Data.ToArray());
        expected.Coordinates.Map = "test_map"; expected.Coordinates.X = 11.5f; expected.Coordinates.Y = -3.25f;
        expected.Coordinates.Z = 50f;
        double angle = 135 * Math.PI / 360;
        expected.Coordinates.SetPlayerRotation(0, 0, (float)Math.Sin(angle), (float)Math.Cos(angle));
        expected.LastSaved.Timestamp = new DateTime(2026, 10, 2, 13, 25, 15);
        expected.SetValue(SaveBlockAccessor9ZA.KHyperspaceSurveyPoints, 12345u);
        expected.SetString(expected.Blocks.GetBlock(SaveBlockAccessor9ZA.KStreetName).Data, "Test Street", 18, StringConverterOption.ClearZero);
        vm.SaveCommand.Execute(null);
        foreach (var block in save.AllBlocks) Assert.Equal(expected.Blocks.GetBlock(block.Key).Data.ToArray(), block.Data.ToArray());
        var reopened = new TrainerEditorViewModel(new SAV9ZA(save.Write()));
        Assert.Equal(12345u, reopened.HyperspacePoints); Assert.Equal("Test Street", reopened.StreetName);
        Assert.Equal("test_map", reopened.ZaMap); Assert.Equal(135, reopened.ZaRotation, 4);
        Assert.Equal(new TimeSpan(13, 25, 15), reopened.ZaLastSavedTime);
        Assert.Equal(15, reopened.ZaLastSavedSecond);
    }

    [Fact]
    public void RevertingFieldsAfterAConflictDoesNotApplyTheRejectedValues()
    {
        var save = CreateSave(); var vm = new TrainerEditorViewModel(save);
        string originalName = vm.TrainerName, originalMap = vm.ZaMap;
        vm.TrainerName = "Rejected"; vm.ZaMap = "rejected";
        save.Coordinates.X += 10;
        var before = save.AllBlocks.ToDictionary(block => block.Key, block => block.Data.ToArray());
        vm.SaveCommand.Execute(null); Assert.True(vm.HasZaError);
        vm.TrainerName = originalName; vm.ZaMap = originalMap;
        vm.SaveCommand.Execute(null); Assert.False(vm.HasZaError);
        foreach (var block in save.AllBlocks) Assert.Equal(before[block.Key], block.Data.ToArray());
    }

    [Fact]
    public void NoOpApplyAndResetDiscardPreserveEveryByteAndEditedFlag()
    {
        var save = CreateSave(); save.State.Edited = false;
        var before = save.AllBlocks.ToDictionary(block => block.Key, block => block.Data.ToArray());
        var vm = new TrainerEditorViewModel(save);
        vm.SaveCommand.Execute(null);
        Assert.False(save.State.Edited);
        vm.ZaMap = "discard"; vm.HyperspacePoints = 123;
        vm.ResetCommand.Execute(null); vm.SaveCommand.Execute(null);
        foreach (var block in save.AllBlocks) Assert.Equal(before[block.Key], block.Data.ToArray());
        Assert.False(save.State.Edited);
    }

    [Theory] [InlineData("Screws")] [InlineData("TMs")]
    public async Task ResetOrCloseDiscardsALateCollectionConfirmation(string kind)
    {
        var save = CreateSave(); var before = save.AllBlocks.ToDictionary(block => block.Key, block => block.Data.ToArray());
        var answer = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var dialogs = new Mock<IDialogService>();
        dialogs.Setup(dialog => dialog.ShowConfirmationAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>())).Returns(answer.Task);
        var vm = new TrainerEditorViewModel(save, dialogs.Object);
        var pending = vm.CollectZaCommand.ExecuteAsync(kind);
        if (kind == "Screws") vm.ResetCommand.Execute(null); else vm.Dispose();
        answer.SetResult(true); await pending;
        Assert.False(vm.CanUndoZaCollection);
        foreach (var block in save.AllBlocks) Assert.Equal(before[block.Key], block.Data.ToArray());
    }

    [Theory] [InlineData("Screws")] [InlineData("TMs")]
    public async Task CollectionUpdatesTheOverworldFlagAndInventoryThenRoundtrips(string kind)
    {
        var save = CreateSave();
        string name = kind == "Screws" ? "itb_a0101_25" : TechnicalMachine9a.TechnicalMachines[0].FieldItem;
        ushort item = kind == "Screws" ? ColorfulScrew9a.ColorfulScrewItemIndex : TechnicalMachine9a.TechnicalMachines[0].ItemID;
        if (kind == "Screws") ColorfulScrew9a.SetAllScrews(save, true);
        else TechnicalMachine9a.SetAllTechnicalMachines(save, true);
        var field = save.Blocks.FieldItems; ulong key = FnvHash.HashFnv1a_64(name);
        int index = field.GetIndex(key);
        if (index < 0) { Assert.True(field.HasSpace(out index)); field.SetKey(index, key); }
        field.SetValue(index, false);
        save.Items.SetItemQuantity(item, 10);
        var before = save.AllBlocks.ToDictionary(block => block.Key, block => block.Data.ToArray());
        var dialogs = new Mock<IDialogService>();
        dialogs.Setup(dialog => dialog.ShowConfirmationAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>())).ReturnsAsync(true);
        var vm = new TrainerEditorViewModel(save, dialogs.Object);
        await vm.CollectZaCommand.ExecuteAsync(kind);
        Assert.True(vm.CanUndoZaCollection);
        foreach (var block in save.AllBlocks) Assert.Equal(before[block.Key], block.Data.ToArray());
        vm.SaveCommand.Execute(null);
        var reopened = new SAV9ZA(save.Write());
        Assert.True(reopened.Blocks.FieldItems.GetValue(reopened.Blocks.FieldItems.GetIndex(key)));
        Assert.Equal(kind == "Screws" ? 11u : 1u, reopened.Items.GetItemQuantity(item));
        uint inventory = save.AllBlocks.Single(block => block.Data.Overlaps(save.Items.Data)).Key;
        uint flags = save.AllBlocks.Single(block => block.Data.Overlaps(field.Data)).Key;
        foreach (var block in reopened.AllBlocks.Where(block => block.Key != inventory && block.Key != flags))
            Assert.Equal(before[block.Key], block.Data.ToArray());
    }

    [Theory] [InlineData("Screws")] [InlineData("TMs")]
    public async Task ConfirmedCollectionCanBeUndoneWithoutLosingPendingFieldEdits(string kind)
    {
        var save = CreateSave(); var before = save.AllBlocks.ToDictionary(block => block.Key, block => block.Data.ToArray());
        var dialogs = new Mock<IDialogService>();
        dialogs.Setup(dialog => dialog.ShowConfirmationAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>())).ReturnsAsync(true);
        var vm = new TrainerEditorViewModel(save, dialogs.Object) { StreetName = "Pending" };
        await vm.CollectZaCommand.ExecuteAsync(kind);
        foreach (var block in save.AllBlocks) Assert.Equal(before[block.Key], block.Data.ToArray());
        vm.UndoZaCollectionCommand.Execute(null);
        Assert.Equal("Pending", vm.StreetName);
        vm.ResetCommand.Execute(null); vm.SaveCommand.Execute(null);
        foreach (var block in save.AllBlocks) Assert.Equal(before[block.Key], block.Data.ToArray());
    }

    [Fact]
    public async Task DxtPreviewAndPngExportPreserveOriginalCompressedBlocks()
    {
        var save = CreateSave();
        var image = new byte[8]; BinaryPrimitives.WriteUInt16LittleEndian(image, 0xF800);
        var dimension = new byte[4]; BinaryPrimitives.WriteUInt32LittleEndian(dimension, 4);
        var extra = new[] {
            ParseBlock(SaveBlockAccessor9ZA.KPictureCurrentData, SCTypeCode.Object, image),
            ParseBlock(SaveBlockAccessor9ZA.KPictureCurrentWidth, SCTypeCode.UInt32, dimension),
            ParseBlock(SaveBlockAccessor9ZA.KPictureCurrentHeight, SCTypeCode.UInt32, dimension),
        };
        var keys = extra.Select(block => block.Key).ToArray();
        save = new SAV9ZA(SwishCrypto.Encrypt(save.AllBlocks.Where(block => !keys.Contains(block.Key)).Concat(extra).OrderBy(block => block.Key).ToArray()));
        var before = save.AllBlocks.ToDictionary(block => block.Key, block => block.Data.ToArray());
        var path = Path.Combine(Path.GetTempPath(), $"pkhex-za-image-{Guid.NewGuid():N}.png");
        var dialogs = new Mock<IDialogService>();
        dialogs.Setup(dialog => dialog.SaveFileAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string[]>())).ReturnsAsync(path);
        var codec = new PngImageCodec(); var vm = new TrainerEditorViewModel(save, dialogs.Object, codec);
        try
        {
            Assert.Equal(3, vm.ZaImages.Count);
            var preview = vm.ZaImages[0]; Assert.True(preview.HasImage);
            var decoded = codec.DecodePng(preview.Png!, 4, 4);
            Assert.NotNull(decoded.Image);
            Assert.Equal(new byte[] { 0, 0, 255, 255 }, decoded.Image.Bgra[..4]);
            await vm.ExportZaImageCommand.ExecuteAsync(preview);
            Assert.Equal(preview.Png, await File.ReadAllBytesAsync(path, TestContext.Current.CancellationToken));
            vm.SaveCommand.Execute(null);
            foreach (var block in save.AllBlocks) Assert.Equal(before[block.Key], block.Data.ToArray());
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public void UnsupportedImageDimensionsDoNotPreventEditingOrNormalizeSource()
    {
        var save = CreateSave();
        var width = new byte[8]; BinaryPrimitives.WriteUInt64LittleEndian(width, 4);
        var malformed = ParseBlock(SaveBlockAccessor9ZA.KPictureCurrentWidth, SCTypeCode.UInt64, width);
        save = new SAV9ZA(SwishCrypto.Encrypt(save.AllBlocks.Where(block => block.Key != malformed.Key).Append(malformed).OrderBy(block => block.Key).ToArray()));
        var before = save.AllBlocks.ToDictionary(block => block.Key, block => block.Data.ToArray());
        var vm = new TrainerEditorViewModel(save, imageCodec: new PngImageCodec());
        Assert.False(vm.ZaImages[0].HasImage);
        vm.SaveCommand.Execute(null);
        foreach (var block in save.AllBlocks) Assert.Equal(before[block.Key], block.Data.ToArray());
    }

    [Fact]
    public void PreUnlockSaveDoesNotExposeOrManufactureMissingDlcBlocks()
    {
        var save = Assert.IsType<SAV9ZA>(SaveUtil.GetSaveFile(File.ReadAllBytes(
            Path.Combine(SaveFileFixture.FindSaveFilesPath()!, "gen9a_legendsza.main"))));
        var keys = save.AllBlocks.Select(block => block.Key).ToArray();
        var vm = new TrainerEditorViewModel(save);
        Assert.False(vm.HasHyperspacePoints); Assert.False(vm.HasStreetName);
        vm.SaveCommand.Execute(null);
        Assert.Equal(keys, save.AllBlocks.Select(block => block.Key).ToArray());
    }

    [Theory] [InlineData("Screws")] [InlineData("TMs")]
    public async Task DeclinedCollectionLeavesSourceAndUndoUnchanged(string kind)
    {
        var save = CreateSave(); var before = save.AllBlocks.ToDictionary(block => block.Key, block => block.Data.ToArray());
        var dialogs = new Mock<IDialogService>();
        dialogs.Setup(dialog => dialog.ShowConfirmationAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>())).ReturnsAsync(false);
        var vm = new TrainerEditorViewModel(save, dialogs.Object);
        await vm.CollectZaCommand.ExecuteAsync(kind);
        Assert.False(vm.CanUndoZaCollection);
        foreach (var block in save.AllBlocks) Assert.Equal(before[block.Key], block.Data.ToArray());
    }
}
