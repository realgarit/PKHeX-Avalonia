using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Media.Imaging;
using Avalonia.VisualTree;
using PKHeX.Application.Abstractions;
using PKHeX.Application.Services;
using PKHeX.Avalonia.Views;
using PKHeX.Core;
using PKHeX.Presentation.Localization;
using PKHeX.Presentation.ViewModels;

namespace PKHeX.Avalonia.Render.Tests;

public class BlockEditorRenderTests
{
    [AvaloniaTheory]
    [InlineData("en", 860, 580)]
    [InlineData("de", 620, 440)]
    [InlineData("ja", 620, 440)]
    public async Task DumpAndCompareTabsRenderCompactWithoutSourceWrites(string language, int width, int height)
    {
        var previous = LocalizedStrings.Instance.CurrentLanguage;
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Tests/savefiles/gen8_sword_ct_public.main")))
            directory = directory.Parent;
        Assert.NotNull(directory);
        var path = Path.Combine(directory.FullName, "Tests/savefiles/gen8_sword_ct_public.main");
        var save = Assert.IsType<SAV8SWSH>(SaveUtil.GetSaveFile(File.ReadAllBytes(path)));
        var before = save.Write().ToArray();
        Window? window = null;
        try
        {
            LocalizedStrings.Instance.SetLanguage(language);
            var vm = new BlockEditorViewModel(save, new NullDialogs());
            vm.SelectedBlock = vm.FilteredBlocks.Single(z => z.Entry.Key == 0x19722C89);
            var other = Assert.IsType<SAV8SWSH>(SaveUtil.GetSaveFile(File.ReadAllBytes(path)));
            other.Accessor.GetBlock(0x19722C89).Data[0] ^= 1;
            vm.ApplyComparison(SaveBlockSet.Compare(vm.BlockSet, SaveBlockSet.Create(other)));

            var view = new BlockEditor { DataContext = vm };
            window = new Window { Content = view, Width = width, Height = height };
            window.Show();
            var tabs = view.FindControl<TabControl>("Tabs")!;
            for (var i = 0; i < tabs.Items.Count; i++)
            {
                tabs.SelectedIndex = i;
                await PKHeX.Testing.HeadlessRenderSettling.Settle(window);
                foreach (var button in view.GetVisualDescendants().OfType<Button>().Where(b => b.IsEffectivelyVisible))
                {
                    var origin = button.TranslatePoint(default, window)!.Value;
                    Assert.True(origin.X + button.Bounds.Width <= width + 0.5, $"{language}: {button.Content} overflows");
                    Assert.True(origin.Y + button.Bounds.Height <= height + 0.5, $"{language}: {button.Content} is below the window");
                }
                using var frame = new RenderTargetBitmap(new PixelSize(width, height));
                frame.Render(window);
                if (Environment.GetEnvironmentVariable("PKHEX_HEADLESS_CAPTURE") == "1" && Environment.GetEnvironmentVariable("PKHEX_HEADLESS_CAPTURE_DIR") is { } capture)
                {
                    Directory.CreateDirectory(capture);
                    frame.Save(Path.Combine(capture, $"block-editor-{language}-tab{i}.png"), PngBitmapEncoderOptions.Default);
                }
            }
            Assert.Single(vm.CompareResults);
            Assert.Equal(before, save.Write().ToArray());
        }
        finally
        {
            window?.Close();
            LocalizedStrings.Instance.SetLanguage(previous);
        }
    }

    private sealed class NullDialogs : IDialogService
    {
        public Task<string?> OpenFileAsync(string title, string[]? filters = null) => Task.FromResult<string?>(null);
        public Task<string?> OpenFolderAsync(string title) => Task.FromResult<string?>(null);
        public Task<string?> SaveFileAsync(string title, string? defaultFileName = null, string[]? filters = null) => Task.FromResult<string?>(null);
        public Task ShowErrorAsync(string title, string message) => Task.CompletedTask;
        public Task ShowInformationAsync(string title, string message) => Task.CompletedTask;
        public Task<bool> ShowConfirmationAsync(string title, string message, string confirmText = "Yes", string cancelText = "Cancel") => Task.FromResult(false);
        public Task<UnsavedChangesChoice> ShowUnsavedChangesAsync(string title, string message, string saveText, string discardText, string cancelText) => Task.FromResult(UnsavedChangesChoice.Cancel);
        public Task<string?> GetClipboardTextAsync() => Task.FromResult<string?>(null);
        public Task SetClipboardTextAsync(string text) => Task.CompletedTask;
        public void RevealInFileManager(string path) { }
    }
}
