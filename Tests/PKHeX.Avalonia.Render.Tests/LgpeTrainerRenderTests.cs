using System.Buffers.Binary;
using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Media.Imaging;
using PKHeX.Application.Services;
using PKHeX.Avalonia.Views;
using PKHeX.Core;
using PKHeX.Presentation.Localization;
using PKHeX.Presentation.ViewModels;

namespace PKHeX.Avalonia.Render.Tests;

public class LgpeTrainerRenderTests
{
    [AvaloniaTheory]
    [InlineData("en", 900, 600)] [InlineData("de", 620, 420)] [InlineData("ja", 620, 420)] [InlineData("pt-BR", 620, 420)]
    public async Task PopulatedTrainerMapAndParkFramesPreserveTheSource(string language, int width, int height)
    {
        var previous = LocalizedStrings.Instance.CurrentLanguage;
        var dataLanguage = GameInfo.CurrentLanguage; var strings = GameInfo.Strings; var filtered = GameInfo.FilteredSources;
        var culture = CultureInfo.CurrentCulture; var uiCulture = CultureInfo.CurrentUICulture;
        var defaultCulture = CultureInfo.DefaultThreadCurrentCulture; var defaultUiCulture = CultureInfo.DefaultThreadCurrentUICulture;
        Window? window = null;
        try
        {
            new LanguageService().SetLanguage(language); LocalizedStrings.Instance.SetLanguage(language);
            var directory = new DirectoryInfo(AppContext.BaseDirectory);
            while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Tests/savefiles/gen7b_letsgopikachu.bin"))) directory = directory.Parent;
            Assert.NotNull(directory);
            // The corpus has known LGPE storage but lacks auto-detection metadata.
            // Use the typed constructor; this does not assert ordinary file-open detection.
            var save = new SAV7b(File.ReadAllBytes(Path.Combine(directory.FullName, "Tests/savefiles/gen7b_letsgopikachu.bin")));
            save.Park.DeleteAll();
            for (int index = 0; index < 5; index++)
            {
                var record = new GP1().Data.ToArray();
                BinaryPrimitives.WriteUInt16LittleEndian(record.AsSpan(0x28), (ushort)(25 + index));
                BinaryPrimitives.WriteSingleLittleEndian(record.AsSpan(0x30), 10 + index);
                BinaryPrimitives.WriteInt32LittleEndian(record.AsSpan(0x2C), 123 + index * 100);
                record[0x70] = 1; save.Park[index] = GP1.FromData(record);
            }
            var before = save.Data.ToArray();
            using var vm = new Misc7bEditorViewModel(save);
            var view = new Misc7bEditor { DataContext = vm };
            window = new Window { Content = view, Width = width, Height = height }; window.Show();
            var tabs = view.FindControl<TabControl>("LgpeTrainerSections")!;
            for (int section = 0; section < 3; section++)
            {
                tabs.SelectedIndex = section;
                await PKHeX.Testing.HeadlessRenderSettling.Settle(window);
                var apply = view.FindControl<Button>("LgpeTrainerApply")!;
                var point = apply.TranslatePoint(default, view)!.Value;
                Assert.True(point.Y >= 0 && point.Y + apply.Bounds.Height <= view.Bounds.Height);
                Assert.True(point.X >= 0 && point.X + apply.Bounds.Width <= view.Bounds.Width);
                if (section == 2)
                {
                    var grid = view.FindControl<DataGrid>("LgpeParkSlots")!;
                    Assert.True(grid.Bounds.Height >= 80, $"Grid height {grid.Bounds.Height} at {language} {width}x{height}");
                    Assert.True(vm.HasOccupiedSelection);
                    Assert.Contains(GameInfo.Strings.Species[25], vm.SelectedSlot!.Name);
                }
                using var frame = new RenderTargetBitmap(new PixelSize(width, height)); frame.Render(window);
                using var bytes = new MemoryStream(); frame.Save(bytes, PngBitmapEncoderOptions.Default);
                using var pixels = SkiaSharp.SKBitmap.Decode(bytes.ToArray()); Assert.NotNull(pixels);
                Assert.Contains(pixels.Pixels, pixel => pixel != pixels.GetPixel(0, 0));
                if (Environment.GetEnvironmentVariable("PKHEX_HEADLESS_CAPTURE") == "1" && Environment.GetEnvironmentVariable("PKHEX_HEADLESS_CAPTURE_DIR") is { } path)
                { Directory.CreateDirectory(path); frame.Save(Path.Combine(path, $"trainerlgpe-{language}-section{section}.png"), PngBitmapEncoderOptions.Default); }
            }
            vm.SaveCommand.Execute(null); Assert.Equal(before, save.Data.ToArray());
        }
        finally
        {
            window?.Close(); LocalizedStrings.Instance.SetLanguage(previous); GameInfo.CurrentLanguage = dataLanguage;
            GameInfo.Strings = strings; GameInfo.FilteredSources = filtered;
            CultureInfo.CurrentCulture = culture; CultureInfo.CurrentUICulture = uiCulture;
            CultureInfo.DefaultThreadCurrentCulture = defaultCulture; CultureInfo.DefaultThreadCurrentUICulture = defaultUiCulture;
        }
    }
}
