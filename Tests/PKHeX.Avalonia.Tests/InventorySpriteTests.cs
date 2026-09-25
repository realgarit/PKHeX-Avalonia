using PKHeX.Avalonia.Services;
using PKHeX.Core;
using PKHeX.Presentation.ViewModels;
using Avalonia.Headless.XUnit;
using PKHeX.Avalonia.Tests.Harness;
using PKHeX.Avalonia.Views;
using PKHeX.Application.Services;
using SkiaSharp;

namespace PKHeX.Avalonia.Tests;

public class InventorySpriteTests
{
    [Theory]
    [InlineData(EntityContext.Gen1, 10, "81")]
    [InlineData(EntityContext.Gen1, 20, "17")]
    [InlineData(EntityContext.Gen1, 201, "tm")]
    [InlineData(EntityContext.Gen2, 8, "81")]
    [InlineData(EntityContext.Gen2, 18, "17")]
    [InlineData(EntityContext.Gen2, 191, "tm")]
    [InlineData(EntityContext.Gen3, 94, "81")]
    [InlineData(EntityContext.Gen3, 95, "82")]
    [InlineData(EntityContext.Gen3, 84, "77")]
    [InlineData(EntityContext.Gen3, 73, "55")]
    [InlineData(EntityContext.Gen3, 289, "tm")]
    [InlineData(EntityContext.Gen3, 339, "tm")]
    [InlineData(EntityContext.Gen4, 81, "81")]
    [InlineData(EntityContext.Gen4, 328, "tm")]
    [InlineData(EntityContext.Gen5, 17, "17")]
    [InlineData(EntityContext.Gen6, 17, "17")]
    [InlineData(EntityContext.Gen7, 17, "17")]
    [InlineData(EntityContext.Gen7b, 17, "17")]
    [InlineData(EntityContext.Gen8, 1130, "tr")]
    [InlineData(EntityContext.Gen8b, 420, "tm")]
    [InlineData(EntityContext.Gen8a, 17, "17")]
    [InlineData(EntityContext.Gen9, 2160, "tm")]
    [InlineData(EntityContext.Gen9a, 2160, "tm")]
    [InlineData(EntityContext.Gen3, 9999, "unk")]
    [InlineData(EntityContext.Gen1, -1, "unk")]
    public void ItemIdentity_LoadsExpectedPixels(EntityContext context, int id, string asset)
    {
        using var actual = new SpriteLoader().GetItemSprite(id, context, GameVersion.Any);
        AssertAsset(actual, $"Big_Items.bitem_{asset}.png");
    }

    [Fact]
    public void ArtworkOnlyItem_LoadsExistingArtwork()
    {
        using var actual = new SpriteLoader().GetItemSprite(2563, EntityContext.Gen9a, GameVersion.ZA);
        AssertAsset(actual, "Artwork_Items.aitem_2563.png");
    }

    [Fact]
    public void HeldItemOverlay_UsesTheSameIdentityAcrossFormats()
    {
        var renderer = new AvaloniaSpriteRenderer(new AppSettings());
        renderer.Initialize(BlankSaveFile.Get(GameVersion.FR));
        var oldFormat = new PK3 { Species = 25, HeldItem = 94 };
        var modernFormat = new PK4 { Species = 25, HeldItem = 81 };
        using var oldImage = SKBitmap.Decode(renderer.GetSprite(oldFormat)!);
        using var modernImage = SKBitmap.Decode(renderer.GetSprite(modernFormat)!);
        Assert.Equal(modernImage.Bytes, oldImage.Bytes);
    }

    [Fact]
    public void EmptyItem_HasNoIcon()
        => Assert.Null(new SpriteLoader().GetItemSprite(0, EntityContext.Gen3, GameVersion.FR));

    [Fact]
    public void LegacyKeyItem_ResolvesDespiteDecoratedDisplayNames()
    {
        _ = GameInfo.GetStrings("en"); // Adds game labels to shared item-name arrays.
        var reference = ItemSpriteResolver.Resolve(6, EntityContext.Gen1, GameVersion.RD);
        Assert.Contains(450, reference.ItemIds); // Bicycle -> Bike
        Assert.DoesNotContain(6, reference.ItemIds); // Never Net Ball
    }

    [Theory]
    [InlineData(GameVersion.COLO)]
    [InlineData(GameVersion.XD)]
    public void GameCubeExclusiveItem_DoesNotUseUnrelatedModernId(GameVersion version)
    {
        // ID 500 is a Jail Key/Safe Key in these games, not the modern Park Ball.
        using var actual = new SpriteLoader().GetItemSprite(500, EntityContext.Gen3, version);
        AssertAsset(actual, "Big_Items.bitem_unk.png");
    }

    [AvaloniaFact]
    public void CaptureFireRedInventory_WhenEnabled()
    {
        if (Environment.GetEnvironmentVariable("PKHEX_HEADLESS_CAPTURE") != "1")
            return;
        using var app = new HeadlessAppFixture();
        var vm = new InventoryEditorViewModel(BlankSaveFile.Get(GameVersion.FR), new AvaloniaSpriteRenderer(new AppSettings()));
        var pouch = vm.Pouches.First(p => p.ItemList.Any(i => i.Value == 94));
        vm.SelectedPouch = pouch;
        int[] ids = [94, 95, 84, 73];
        for (int i = 0; i < ids.Length; i++)
        {
            pouch.Items[i].ItemId = ids[i];
            pouch.Items[i].Count = 1;
        }
        app.Window.Content = new InventoryEditor { DataContext = vm };
        app.Window.Width = 900;
        app.Window.Height = 600;
        app.Pump();
        var directory = Environment.GetEnvironmentVariable("PKHEX_HEADLESS_CAPTURE_DIR") ?? Path.GetTempPath();
        Directory.CreateDirectory(directory);
        Assert.NotNull(app.CaptureFrame(Path.Combine(directory, "inventory-firered.png")));
    }

    [Fact]
    public void Inventory_ChangesAndResetRetainSaveContextAndRawIds()
    {
        var save = BlankSaveFile.Get(GameVersion.FR);
        var renderer = new AvaloniaSpriteRenderer(new AppSettings());
        var vm = new InventoryEditorViewModel(save, renderer);
        var pouch = vm.Pouches.First(p => p.ItemList.Any(i => i.Value == 94));
        var item = pouch.Items[0];
        bool spriteChanged = false;
        item.PropertyChanged += (_, e) => spriteChanged |= e.PropertyName == nameof(item.Sprite);
        item.ItemId = 94;
        item.Count = 1;
        using (var bitmap = SKBitmap.Decode(item.Sprite!))
            AssertAsset(bitmap, "Big_Items.bitem_81.png");
        Assert.True(spriteChanged);
        vm.SaveCommand.Execute(null);
        item.ItemId = 95;
        using (var bitmap = SKBitmap.Decode(item.Sprite!))
            AssertAsset(bitmap, "Big_Items.bitem_82.png");
        vm.ResetCommand.Execute(null);
        Assert.Equal(94, pouch.Items[0].ItemId);
        using (var bitmap = SKBitmap.Decode(pouch.Items[0].Sprite!))
            AssertAsset(bitmap, "Big_Items.bitem_81.png");
        var reloaded = new InventoryEditorViewModel(save, renderer);
        Assert.Equal(94, reloaded.Pouches.First(p => p.PouchName == pouch.PouchName).Items[0].ItemId);
    }

    private static void AssertAsset(SKBitmap? actual, string resource)
    {
        Assert.NotNull(actual);
        using var stream = typeof(SpriteLoader).Assembly.GetManifestResourceStream("PKHeX.Avalonia.Assets.Images." + resource);
        Assert.NotNull(stream);
        using var expected = SKBitmap.Decode(stream);
        Assert.Equal(expected.Width, actual.Width);
        Assert.Equal(expected.Height, actual.Height);
        Assert.Equal(expected.Bytes, actual.Bytes);
    }
}
