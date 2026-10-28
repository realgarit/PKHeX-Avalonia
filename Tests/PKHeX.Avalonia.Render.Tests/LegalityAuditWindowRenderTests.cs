using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless.XUnit;
using Avalonia.Media.Imaging;
using Avalonia.Styling;
using Avalonia.VisualTree;
using Moq;
using PKHeX.Application.Abstractions;
using PKHeX.Avalonia.Services;
using PKHeX.Avalonia.Views;
using PKHeX.Core;
using PKHeX.Presentation.Localization;
using PKHeX.Presentation.ViewModels;

namespace PKHeX.Avalonia.Render.Tests;

public class LegalityAuditWindowRenderTests
{
    [AvaloniaTheory]
    [InlineData("en", false)] [InlineData("en", true)]
    [InlineData("de", false)] [InlineData("de", true)]
    [InlineData("ja", false)] [InlineData("ja", true)]
    [InlineData("pt-BR", false)] [InlineData("pt-BR", true)]
    public async Task PopulatedAuditFitsMinimumClientAreaWithReachableRowsAndActions(string language, bool dark)
    {
        var previousLanguage = LocalizedStrings.Instance.CurrentLanguage;
        var application = global::Avalonia.Application.Current!;
        var previousTheme = application.RequestedThemeVariant;
        Window? window = null;
        try
        {
            LocalizedStrings.Instance.SetLanguage(language);
            application.RequestedThemeVariant = dark ? ThemeVariant.Dark : ThemeVariant.Light;
            var save = new SAV3E();
            for (int i = 0; i < 30; i++)
            {
                var pokemon = new PK3 { Species = (ushort)Species.Mudkip, CurrentLevel = 5, Nickname = $"Audit{i:D2}" };
                pokemon.RefreshChecksum();
                save.SetBoxSlotAtIndex(pokemon, 0, i);
            }
            var vm = new LegalityAuditViewModel(save, Mock.Of<IDialogService>());
            await vm.RunCommand.ExecuteAsync(null);
            Assert.Equal(30, vm.Rows.Count);
            var view = new LegalityAuditView { DataContext = vm };
            window = new Window { Content = view, Width = 474, Height = 304, MaxWidth = 1200, MaxHeight = 760 };
            WindowService.ConstrainToolBounds(window);
            window.Show();
            await PKHeX.Testing.HeadlessRenderSettling.Settle(window);
            Assert.Equal(new Size(700, 400), window.ClientSize);
            foreach (var button in view.GetVisualDescendants().OfType<Button>().Where(button => button.Command is not null))
                AssertVisible(window, button);
            var headers = view.GetVisualDescendants().OfType<DataGridColumnHeader>()
                .Where(header => header.IsEffectivelyVisible && header.Content is string { Length: > 0 }).ToArray();
            Assert.Equal(5, headers.Length);
            foreach (var header in headers) AssertVisible(window, header);
            var grid = view.FindControl<DataGrid>("AuditGrid")!;
            var row = Assert.Single(grid.GetVisualDescendants().OfType<DataGridRow>(), row => row.DataContext == vm.Rows[0]);
            AssertVisible(window, row);
            Capture(window, $"audit-{language}-{(dark ? "dark" : "light")}-top");

            grid.ScrollIntoView(vm.Rows[^1], grid.Columns[0]);
            vm.SelectedRow = vm.Rows[^1];
            await PKHeX.Testing.HeadlessRenderSettling.Settle(window);
            var last = Assert.Single(grid.GetVisualDescendants().OfType<DataGridRow>(), row => row.DataContext == vm.Rows[^1]);
            AssertVisible(window, last);
            Assert.True(vm.CopySelectedReportCommand.CanExecute(null));
            AssertVisible(window, view.FindControl<TextBlock>("AuditStatus")!);
            Capture(window, $"audit-{language}-{(dark ? "dark" : "light")}-last");
        }
        finally
        {
            window?.Close();
            LocalizedStrings.Instance.SetLanguage(previousLanguage);
            application.RequestedThemeVariant = previousTheme;
        }
    }

    private static void AssertVisible(Window window, Control control)
    {
        var point = control.TranslatePoint(default, window)!.Value;
        Assert.True(point.X >= 0 && point.Y >= 0 && point.X + control.Bounds.Width <= window.ClientSize.Width + 1 &&
            point.Y + control.Bounds.Height <= window.ClientSize.Height + 1,
            $"{control.GetType().Name} at {point} + {control.Bounds.Size} outside {window.ClientSize}");
    }

    private static void Capture(Window window, string name)
    {
        using var bitmap = new RenderTargetBitmap(PixelSize.FromSize(window.ClientSize, 1));
        bitmap.Render(window);
        using var stream = new MemoryStream();
        bitmap.Save(stream, PngBitmapEncoderOptions.Default);
        using var pixels = SkiaSharp.SKBitmap.Decode(stream.ToArray());
        Assert.Contains(pixels.Pixels, pixel => pixel != pixels.GetPixel(0, 0));
        if (Environment.GetEnvironmentVariable("PKHEX_HEADLESS_CAPTURE_DIR") is { } path)
        {
            Directory.CreateDirectory(path);
            bitmap.Save(Path.Combine(path, $"{name}.png"), PngBitmapEncoderOptions.Default);
        }
    }
}
