using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Media.Imaging;
using Avalonia.Styling;
using Avalonia.VisualTree;
using PKHeX.Application.Services;
using PKHeX.Avalonia.Services;
using PKHeX.Avalonia.Views;
using PKHeX.Core;
using PKHeX.Presentation.ViewModels;

namespace PKHeX.Avalonia.Render.Tests;

public class AdditionalInventoryArtTests
{
    [AvaloniaTheory]
    [InlineData(false)] [InlineData(true)]
    public async Task AllReportedItemsHaveArtworkAndReadableNamesInTheActualInventory(bool dark)
    {
        var previous = global::Avalonia.Application.Current!.RequestedThemeVariant;
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Tests/savefiles/gen9a_legendsza.main"))) directory = directory.Parent;
        var save = Assert.IsType<SAV9ZA>(SaveUtil.GetSaveFile(File.ReadAllBytes(Path.Combine(directory!.FullName, "Tests/savefiles/gen9a_legendsza.main"))));
        var vm = new InventoryEditorViewModel(save, new AvaloniaSpriteRenderer(new AppSettings()));
        var view = new InventoryEditor { DataContext = vm };
        var window = new Window { Content = view, Width = 900, Height = 650 };
        var ids = new[] { 2137, 2619 }.Concat(Enumerable.Range(2595, 19)).Concat(Enumerable.Range(2620, 15));
        try
        {
            global::Avalonia.Application.Current.RequestedThemeVariant = dark ? ThemeVariant.Dark : ThemeVariant.Light;
            window.Show(); int page = 0;
            foreach (var group in ids.GroupBy(id => vm.Pouches.First(p => p.ItemList.Any(item => item.Value == id))))
            foreach (var batch in group.Chunk(6))
            {
                vm.SelectedPouch = group.Key;
                for (int i = 0; i < batch.Length; i++)
                {
                    group.Key.Items[i].ItemId = batch[i]; group.Key.Items[i].Count = 1;
                    Assert.NotNull(group.Key.Items[i].Sprite);
                }
                await PKHeX.Testing.HeadlessRenderSettling.Settle(window);
                var grid = view.GetVisualDescendants().OfType<DataGrid>().Single(g => g.IsEffectivelyVisible);
                grid.ScrollIntoView(group.Key.Items[0], null);
                await PKHeX.Testing.HeadlessRenderSettling.Settle(window);
                Assert.True(grid.GetVisualDescendants().OfType<DataGridRow>().Count() >= batch.Length);
                using var frame = new RenderTargetBitmap(new PixelSize(900, 650)); frame.Render(window);
                if (Environment.GetEnvironmentVariable("PKHEX_HEADLESS_CAPTURE") == "1" && Environment.GetEnvironmentVariable("PKHEX_HEADLESS_CAPTURE_DIR") is { } path)
                { Directory.CreateDirectory(path); frame.Save(Path.Combine(path, $"inventory-new-art-{dark}-{page}.png"), PngBitmapEncoderOptions.Default); }
                page++;
            }
        }
        finally { window.Close(); global::Avalonia.Application.Current.RequestedThemeVariant = previous; }
    }
}
