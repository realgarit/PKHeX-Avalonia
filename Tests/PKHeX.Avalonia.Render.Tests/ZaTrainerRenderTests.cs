using System.Buffers.Binary;
using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Media.Imaging;
using Avalonia.VisualTree;
using PKHeX.Application.Services;
using PKHeX.Avalonia.Services;
using PKHeX.Avalonia.Views;
using PKHeX.Core;
using PKHeX.Presentation.Localization;
using PKHeX.Presentation.ViewModels;

namespace PKHeX.Avalonia.Render.Tests;

public class ZaTrainerRenderTests
{
    [AvaloniaTheory]
    [InlineData("en", 900, 600)] [InlineData("de", 620, 420)]
    [InlineData("ja", 620, 420)] [InlineData("pt-BR", 620, 420)]
    public async Task RealSkiaFramesExposeFourSectionsWithoutMutatingSave(string language, int width, int height)
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
            while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Tests/savefiles/gen9a_legendsza.main"))) directory = directory.Parent;
            Assert.NotNull(directory);
            var source = Assert.IsType<SAV9ZA>(SaveUtil.GetSaveFile(File.ReadAllBytes(Path.Combine(directory.FullName, "Tests/savefiles/gen9a_legendsza.main"))));
            // The corpus predates DLC unlock. Add synthetic optional blocks only
            // to this disposable render fixture, never to a user's save.
            var blocks = new[] { Parse(SaveBlockAccessor9ZA.KHyperspaceSurveyPoints, SCTypeCode.UInt32, new byte[4]),
                Parse(SaveBlockAccessor9ZA.KStreetName, SCTypeCode.Object, new byte[0x26]) };
            var keys = blocks.Select(block => block.Key).ToArray();
            var save = new SAV9ZA(SwishCrypto.Encrypt(source.AllBlocks.Where(block => !keys.Contains(block.Key)).Concat(blocks).OrderBy(block => block.Key).ToArray()));
            save.SetValue(SaveBlockAccessor9ZA.KHyperspaceSurveyPoints, 12345u);
            save.SetString(save.Blocks.GetBlock(SaveBlockAccessor9ZA.KStreetName).Data, "Test Street", 18, StringConverterOption.ClearZero);
            var before = save.AllBlocks.ToDictionary(block => block.Key, block => block.Data.ToArray());
            var vm = new TrainerEditorViewModel(save, imageCodec: new PngImageCodec());
            var host = new TrainerEditor { DataContext = vm };
            window = new Window { Content = host, Width = width, Height = height }; window.Show();
            await PKHeX.Testing.HeadlessRenderSettling.Settle(window);
            var view = Assert.Single(host.GetVisualDescendants().OfType<TrainerZaWorkspace>());
            var tabs = view.FindControl<TabControl>("TrainerZaSections")!;
            for (int index = 0; index < 4; index++)
            {
                tabs.SelectedIndex = index;
                await PKHeX.Testing.HeadlessRenderSettling.Settle(window);
                var apply = view.FindControl<Button>("TrainerZaApply")!;
                var point = apply.TranslatePoint(default, view)!.Value;
                Assert.True(point.Y >= 0 && point.Y + apply.Bounds.Height <= view.Bounds.Height);
                using var frame = new RenderTargetBitmap(new PixelSize(width, height)); frame.Render(window);
                using var bytes = new MemoryStream(); frame.Save(bytes, PngBitmapEncoderOptions.Default);
                using var pixels = SkiaSharp.SKBitmap.Decode(bytes.ToArray()); Assert.NotNull(pixels);
                Assert.Contains(pixels.Pixels, pixel => pixel != pixels.GetPixel(0, 0));
                if (Environment.GetEnvironmentVariable("PKHEX_HEADLESS_CAPTURE") == "1" && Environment.GetEnvironmentVariable("PKHEX_HEADLESS_CAPTURE_DIR") is { } path)
                { Directory.CreateDirectory(path); frame.Save(Path.Combine(path, $"trainerza-{language}-section{index}.png"), PngBitmapEncoderOptions.Default); }
                if (index == 0)
                {
                    var scroll = Assert.Single(view.GetVisualDescendants().OfType<ScrollViewer>(), scroll => scroll.Content is StackPanel);
                    scroll.ScrollToEnd(); await PKHeX.Testing.HeadlessRenderSettling.Settle(window);
                    using var saved = new RenderTargetBitmap(new PixelSize(width, height)); saved.Render(window);
                    if (Environment.GetEnvironmentVariable("PKHEX_HEADLESS_CAPTURE") == "1" && Environment.GetEnvironmentVariable("PKHEX_HEADLESS_CAPTURE_DIR") is { } savedPath)
                        saved.Save(Path.Combine(savedPath, $"trainerza-{language}-lastsaved.png"), PngBitmapEncoderOptions.Default);
                }
            }
            vm.SaveCommand.Execute(null);
            foreach (var block in save.AllBlocks) Assert.Equal(before[block.Key], block.Data.ToArray());
        }
        finally
        {
            window?.Close(); LocalizedStrings.Instance.SetLanguage(previous); GameInfo.CurrentLanguage = dataLanguage;
            GameInfo.Strings = strings; GameInfo.FilteredSources = filtered;
            CultureInfo.CurrentCulture = culture; CultureInfo.CurrentUICulture = uiCulture;
            CultureInfo.DefaultThreadCurrentCulture = defaultCulture; CultureInfo.DefaultThreadCurrentUICulture = defaultUiCulture;
        }
    }

    internal static SCBlock Parse(uint key, SCTypeCode type, byte[] data)
    {
        var xor = new SCXorShift32(key); bool objectType = type == SCTypeCode.Object;
        int start = objectType ? 5 : 1;
        var encoded = new byte[start + data.Length]; encoded[0] = (byte)((byte)type ^ xor.Next());
        if (objectType) BinaryPrimitives.WriteInt32LittleEndian(encoded.AsSpan(1), data.Length ^ xor.Next32());
        for (int i = 0; i < data.Length; i++) encoded[start + i] = (byte)(data[i] ^ xor.Next());
        int offset = 0; return SCBlock.ReadFromOffset(encoded, key, ref offset);
    }
}
