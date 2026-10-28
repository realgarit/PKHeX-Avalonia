using Moq;
using PKHeX.Core;
using PKHeX.Presentation.ViewModels;

namespace PKHeX.Avalonia.Tests;

public class MysteryGiftPreviewLifecycleTests
{
    [Fact]
    public async Task ImportSelectionDeleteAndResetRefreshPreviewWithoutWritingSource()
    {
        var path = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".wc7");
        try
        {
            var save = BlankSaveFile.Get(GameVersion.SN);
            var storage = ((IMysteryGiftStorageProvider)save).MysteryGiftStorage;
            storage.SetMysteryGift(0, new WC7 { IsItem = true, ItemID = 100, Quantity = 2, CardID = 123 });
            var original = save.Data.ToArray();
            var imported = new WC7 { IsEntity = true, Species = 25, Level = 50, CardID = 456 };
            await File.WriteAllBytesAsync(path, imported.Data.ToArray(), TestContext.Current.CancellationToken);
            var dialog = new Mock<IDialogService>();
            dialog.Setup(d => d.OpenFileAsync(It.IsAny<string>(), It.IsAny<string[]>())).ReturnsAsync(path);
            var sprites = new Mock<ISpriteRenderer>();
            sprites.Setup(s => s.GetItemSprite(It.IsAny<int>(), It.IsAny<EntityContext>(), It.IsAny<GameVersion>())).Returns([1]);
            sprites.Setup(s => s.GetSprite(It.IsAny<ushort>(), It.IsAny<byte>(), It.IsAny<byte>(), It.IsAny<uint>(), It.IsAny<bool>(), It.IsAny<EntityContext>())).Returns([2]);
            var vm = new MysteryGiftEditorViewModel(save, dialog.Object, sprites: sprites.Object);
            Assert.Equal([1], vm.SelectedGift!.Sprite);
            await vm.ImportGiftCommand.ExecuteAsync(null);
            Assert.Equal([2], vm.SelectedGift.Sprite);
            Assert.Contains("456", vm.SelectedGift.CardIdentity);
            Assert.Equal(original, save.Data.ToArray());
            vm.SelectGiftCommand.Execute(vm.Gifts[1]);
            Assert.Empty(vm.SelectedGift!.PreviewRows);
            vm.SelectGiftCommand.Execute(vm.Gifts[0]);
            Assert.Contains("456", vm.SelectedGift!.CardIdentity);
            vm.DeleteGiftCommand.Execute(null);
            Assert.Empty(vm.SelectedGift.PreviewRows);
            Assert.Null(vm.SelectedGift.Sprite);
            Assert.Equal(original, save.Data.ToArray());
            vm.ResetCommand.Execute(null);
            Assert.Contains("123", vm.SelectedGift!.CardIdentity);
            Assert.Equal([1], vm.SelectedGift.Sprite);
            vm.SaveCommand.Execute(null);
            Assert.Equal(original, save.Data.ToArray());
        }
        finally { File.Delete(path); }
    }
}
