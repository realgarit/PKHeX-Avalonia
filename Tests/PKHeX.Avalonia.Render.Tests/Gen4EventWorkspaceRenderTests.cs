using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Media.Imaging;
using Avalonia.Styling;
using Avalonia.VisualTree;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using PKHeX.Application.Abstractions;
using PKHeX.Application.Models.Events;
using PKHeX.Application.Services;
using PKHeX.Avalonia.Views;
using PKHeX.Core;
using PKHeX.Presentation.Localization;
using PKHeX.Presentation.Models;
using PKHeX.Presentation.ViewModels;

namespace PKHeX.Avalonia.Render.Tests;

public sealed class Gen4EventWorkspaceRenderTests
{
    [AvaloniaTheory]
    [InlineData("gen4_diamond.sav", "en", false, 600, 14)]
    [InlineData("gen4_platinum.sav", "de", true, 600, 14)]
    [InlineData("gen4_heartgold.sav", "ja", false, 600, 14)]
    [InlineData("gen4_platinum.sav", "pt-BR", true, 600, 14)]
    [InlineData("gen4_heartgold.sav", "ja", false, 570, 14)]
    [InlineData("gen4_heartgold.sav", "ja", false, 570, 18)]
    [InlineData("gen4_heartgold.sav", "ja", false, 600, 22)]
    public async Task OriginalDsEventsRenderInTheCompactMainWorkspaceWithReachableLabelsAndApply(string file, string language, bool dark, int height, double fontSize)
    {
        var previousLanguage = LocalizedStrings.Instance.CurrentLanguage;
        var previousData = GameInfo.CurrentLanguage;
        var previousStrings = GameInfo.Strings;
        var previousFiltered = GameInfo.FilteredSources;
        var previousTheme = global::Avalonia.Application.Current!.RequestedThemeVariant;
        var culture = CultureInfo.CurrentCulture;
        var uiCulture = CultureInfo.CurrentUICulture;
        var defaultCulture = CultureInfo.DefaultThreadCurrentCulture;
        var defaultUiCulture = CultureInfo.DefaultThreadCurrentUICulture;
        var root = Path.Combine(Path.GetTempPath(), "pkhex-events-render-" + Guid.NewGuid().ToString("N"));
        var paths = new Mock<IAppPaths>();
        paths.SetupGet(path => path.ConfigDirectory).Returns(Path.Combine(root, "config"));
        paths.SetupGet(path => path.DataDirectory).Returns(Path.Combine(root, "data"));
        paths.SetupGet(path => path.ConfigFilePath).Returns(Path.Combine(root, "config.json"));
        paths.SetupGet(path => path.LegacyConfigFilePath).Returns(Path.Combine(root, "legacy.json"));
        using var services = (ServiceProvider)App.BuildServiceProvider(paths.Object, Mock.Of<ISettingsStore>(), new AppSettings(), overrides =>
        {
            overrides.AddSingleton(Mock.Of<IDialogService>());
            overrides.AddSingleton(Mock.Of<IWindowService>());
        });
        var gateway = services.GetRequiredService<ISaveFileGateway>();
        Window? window = null;
        try
        {
            global::Avalonia.Application.Current.RequestedThemeVariant = dark ? ThemeVariant.Dark : ThemeVariant.Light;
            var main = services.GetRequiredService<MainWindowViewModel>();
            main.LanguageService.SetLanguage(language);
            main.SelectedTheme = dark ? AppTheme.Dark : AppTheme.Light;
            var directory = new DirectoryInfo(AppContext.BaseDirectory);
            while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Tests/savefiles", file))) directory = directory.Parent;
            Assert.NotNull(directory);
            var save = Assert.IsAssignableFrom<SAV4>(SaveUtil.GetSaveFile(File.ReadAllBytes(Path.Combine(directory.FullName, "Tests/savefiles", file))));
            gateway.OpenLoadedSave(save, "fixture.sav");
            Assert.True(main.HasSave);
            save.State.Edited = false;
            var before = save.Data.ToArray();
            var general = save.General.ToArray();
            var vm = Assert.IsType<CategorizedEventEditorViewModel>(main.EventFlagsEditor);
            main.SelectSaveNavigationCommand.Execute(null);
            window = new MainWindow { DataContext = main, Width = 900, MinHeight = height, Height = height };
            window.Show();
            await PKHeX.Testing.HeadlessRenderSettling.Settle(window);
            var view = window.GetVisualDescendants().OfType<CategorizedEventEditor>().Single(control => control.IsEffectivelyVisible);
            view.FontSize = fontSize;
            await PKHeX.Testing.HeadlessRenderSettling.Settle(window);
            var grid = view.FindControl<DataGrid>("EventGrid")!;
            Assert.Equal(fontSize, grid.FontSize);
            Assert.Equal(view.FontWeight, grid.FontWeight);

            foreach (var (kind, index) in new[] { (EventDataKind.Flag, 214), (EventDataKind.Flag, 304), (EventDataKind.Work, save is SAV4HGSS ? 304 : 64), (EventDataKind.Flag, save.EventFlagCount - 1) })
            {
                vm.SelectedBank = vm.Banks.Single(bank => bank.Kind == kind);
                vm.SelectedIndex = index;
                var row = vm.SelectedRow!;
                // Let the bank's ItemsSource binding reach the virtualized grid before
                // asking it to realize a row from that new bank.
                await PKHeX.Testing.HeadlessRenderSettling.Settle(window);
                grid.ScrollIntoView(row, grid.Columns[0]);
                await PKHeX.Testing.HeadlessRenderSettling.Settle(window);
                var name = $"events4-{Path.GetFileNameWithoutExtension(file)}-{language}-{(dark ? "dark" : "light")}-{kind}-{index}" + (height == 600 ? string.Empty : $"-height{height}");
                if (fontSize != 14) name += $"-font{fontSize:0}";
                WriteLayoutDiagnostics(window, view, grid, name);
                Capture(window, name);
                Assert.True(grid.Bounds.Height >= 40, $"Events grid height {grid.Bounds.Height} for {file}/{language}/{kind}/{index} at {window.ClientSize}");
                var realized = Assert.Single(grid.GetVisualDescendants().OfType<DataGridRow>(), control => ReferenceEquals(control.DataContext, row));
                AssertVisible(window, realized);
                AssertInsideVerticalClips(realized);
                var rowTop = realized.TranslatePoint(default, grid)!.Value.Y;
                Assert.True(rowTop >= 0 && rowTop + realized.Bounds.Height <= grid.Bounds.Height + 1,
                    $"The selected event row must fit fully inside the table: {rowTop} + {realized.Bounds.Height} in {grid.Bounds.Height}");
                var indexInput = view.GetVisualDescendants().OfType<NumericUpDown>().Single();
                var presenter = indexInput.GetVisualDescendants().OfType<TextPresenter>().Single();
                Assert.Equal(index.ToString(CultureInfo.CurrentCulture), presenter.Text);
                var line = Assert.Single(presenter.TextLayout.TextLines);
                var viewport = presenter.GetVisualAncestors().OfType<ScrollContentPresenter>().First();
                Assert.True(line.WidthIncludingTrailingWhitespace <= viewport.Viewport.Width + 1,
                    $"Index {index} text width {line.WidthIncludingTrailingWhitespace} exceeds viewport {viewport.Viewport.Width}");
                Assert.NotEmpty(row.Name);
                var footerButtons = view.GetVisualDescendants().OfType<Button>().Where(button => ReferenceEquals(button.Command, vm.ApplyCommand) || ReferenceEquals(button.Command, vm.CancelCommand) || ReferenceEquals(button.Command, vm.ResetCommand)).ToArray();
                foreach (var button in footerButtons)
                {
                    AssertVisible(window, button);
                    var position = button.TranslatePoint(default, view)!.Value;
                    Assert.True(position.Y >= 0 && position.Y + button.Bounds.Height <= view.Bounds.Height + 1,
                        $"Footer action must fit inside the Events workspace: {position} + {button.Bounds} in {view.Bounds}");
                }
                var details = view.FindControl<ContentControl>("EditorDetails")!;
                AssertVisible(window, details);
                var footerTop = footerButtons.Min(button => button.TranslatePoint(default, window)!.Value.Y);
                foreach (var content in new Control[] { grid, details })
                {
                    AssertInsideVerticalClips(content);
                    var bottom = content.TranslatePoint(new Point(0, content.Bounds.Height), window)!.Value.Y;
                    Assert.True(bottom <= footerTop + 1, $"Event content bottom {bottom} must stay above footer {footerTop}");
                }
                if (height < 600)
                    Assert.True(details.Bounds.Height <= grid.Bounds.Height + 1, $"Details height {details.Bounds.Height} must leave the grid at least as much space ({grid.Bounds.Height})");
                if ((height < 600 || fontSize > 14) && kind == EventDataKind.Work)
                {
                    var scroll = details.GetVisualDescendants().OfType<ScrollViewer>().Single(control => control.Content is StackPanel);
                    var input = details.GetVisualDescendants().OfType<TextBox>().Single(control => control.IsEffectivelyVisible);
                    Assert.Equal(fontSize, input.FontSize);
                    Assert.Equal(view.FontWeight, input.FontWeight);
                    Assert.True(input.Focus(NavigationMethod.Tab));
                    await PKHeX.Testing.HeadlessRenderSettling.Settle(window);
                    var inputClip = input.GetVisualAncestors().OfType<ScrollContentPresenter>().First();
                    var inputPoint = input.TranslatePoint(default, inputClip)!.Value;
                    Assert.True(inputPoint.Y >= 0 && inputPoint.Y + input.Bounds.Height <= inputClip.Viewport.Height + 1,
                        $"Keyboard-focused work input must fit completely: {inputPoint} + {input.Bounds} in {inputClip.Viewport}");
                    AssertInsideVerticalClips(input);
                    WriteLayoutDiagnostics(window, view, grid, name + "-input-focused");
                    Capture(window, name + "-input-focused");
                    scroll.ScrollToEnd();
                    await PKHeX.Testing.HeadlessRenderSettling.Settle(window);
                    var bounds = details.GetVisualDescendants().OfType<TextBlock>().Single(control => control.Text == row.BoundsText);
                    var point = bounds.TranslatePoint(default, scroll)!.Value;
                    Assert.True(point.Y >= 0 && point.Y + bounds.Bounds.Height <= scroll.Bounds.Height + 1,
                        $"Work bounds must be reachable by scrolling: {point} + {bounds.Bounds} in {scroll.Bounds}");
                    WriteLayoutDiagnostics(window, view, grid, name + "-details-end");
                    Capture(window, name + "-details-end");
                    scroll.ScrollToHome();
                    await PKHeX.Testing.HeadlessRenderSettling.Settle(window);
                }
                if (fontSize > 14)
                {
                    var warningScroll = view.FindControl<ScrollViewer>("EventWarningScroll")!;
                    var originalWarningLimit = warningScroll.MaxHeight;
                    // Force overflow even when the host's fallback CJK font is narrower
                    // than macOS's, and exercise the actual keyboard scroll route.
                    warningScroll.MaxHeight = fontSize;
                    await PKHeX.Testing.HeadlessRenderSettling.Settle(window);
                    Assert.True(warningScroll.Extent.Height > warningScroll.Viewport.Height);
                    Assert.True(warningScroll.Focus());
                    window.KeyPressQwerty(PhysicalKey.PageDown, RawInputModifiers.None);
                    window.KeyReleaseQwerty(PhysicalKey.PageDown, RawInputModifiers.None);
                    await PKHeX.Testing.HeadlessRenderSettling.Settle(window);
                    Assert.True(warningScroll.Offset.Y > 0, "Focused warning must scroll with PageDown");
                    warningScroll.ScrollToHome();
                    warningScroll.MaxHeight = originalWarningLimit;
                    await PKHeX.Testing.HeadlessRenderSettling.Settle(window);
                }
            }
            vm.ApplyCommand.Execute(null);
            Assert.Equal(before, save.Data.ToArray());
            Assert.Equal(general, save.General.ToArray());
            Assert.False(save.State.Edited);
        }
        finally
        {
            gateway.CloseSave();
            window?.Close();
            if (window is not null) window.Content = null;
            LocalizedStrings.Instance.SetLanguage(previousLanguage);
            GameInfo.CurrentLanguage = previousData;
            GameInfo.Strings = previousStrings;
            GameInfo.FilteredSources = previousFiltered;
            global::Avalonia.Application.Current.RequestedThemeVariant = previousTheme;
            CultureInfo.CurrentCulture = culture;
            CultureInfo.CurrentUICulture = uiCulture;
            CultureInfo.DefaultThreadCurrentCulture = defaultCulture;
            CultureInfo.DefaultThreadCurrentUICulture = defaultUiCulture;
        }
    }

    private static string CaptureDirectory => Environment.GetEnvironmentVariable("PKHEX_HEADLESS_CAPTURE_DIR") ?? Path.Combine(AppContext.BaseDirectory, "TestResults");

    private static void WriteLayoutDiagnostics(Window window, CategorizedEventEditor view, DataGrid grid, string name)
    {
        var warning = view.GetVisualDescendants().OfType<TextBlock>().Single(control => control.Text == LocalizedStrings.Instance["Events_Warning"]);
        var details = view.FindControl<ContentControl>("EditorDetails")!;
        var selectedRow = grid.GetVisualDescendants().OfType<DataGridRow>().SingleOrDefault(row => ReferenceEquals(row.DataContext, grid.SelectedItem));
        var selectedName = selectedRow?.GetVisualDescendants().OfType<TextBlock>().FirstOrDefault(text => text.Text == (grid.SelectedItem as EventDataRow)?.Name);
        var lines = new[]
        {
            $"Client={window.ClientSize}; View={view.Bounds}; Grid={grid.Bounds}; Details={details.Bounds}",
            $"Filters={view.FindControl<StackPanel>("EditorFilters")!.Bounds}",
            $"Warning={warning.Bounds}; Font={warning.FontFamily}/{warning.FontSize}; Lines={warning.TextLayout.TextLines.Count}",
            $"Rows={grid.GetVisualDescendants().OfType<DataGridRow>().Count()}; Culture={CultureInfo.CurrentCulture.Name}; SelectedRow={selectedRow?.Bounds}; RowFont={selectedName?.FontSize}",
            $"ViewOrigin={view.TranslatePoint(default, window)}; WarningScroll={view.FindControl<ScrollViewer>("EventWarningScroll")!.Bounds}",
            $"OuterPadding={((Border)view.Content!).Padding}; TabPadding={view.GetVisualDescendants().OfType<TabControl>().Single().Padding}; Header={((TabItem)view.GetVisualDescendants().OfType<TabControl>().Single().Items[0]!).Bounds}; DataFont={grid.FontSize}/{grid.FontWeight}",
        };
        Directory.CreateDirectory(CaptureDirectory);
        File.WriteAllLines(Path.Combine(CaptureDirectory, name + ".txt"), lines);
    }

    private static void AssertVisible(Window window, Control control)
    {
        var point = control.TranslatePoint(default, window)!.Value;
        Assert.True(point.X >= 0 && point.Y >= 0 && point.X + control.Bounds.Width <= window.ClientSize.Width + 1 && point.Y + control.Bounds.Height <= window.ClientSize.Height + 1,
            $"{control.GetType().Name} at {point} + {control.Bounds.Size} outside {window.ClientSize}");
    }

    private static void AssertInsideVerticalClips(Control control)
    {
        foreach (var clip in control.GetVisualAncestors().Where(visual => visual.ClipToBounds))
        {
            var point = control.TranslatePoint(default, clip)!.Value;
            var bounds = clip.Clip?.Bounds ?? new Rect(clip.Bounds.Size);
            Assert.True(point.Y >= bounds.Top - 1 && point.Y + control.Bounds.Height <= bounds.Bottom + 1,
                $"{control.GetType().Name} [{point.Y}, {point.Y + control.Bounds.Height}] crosses {clip.GetType().Name} clip {bounds}");
        }
    }

    private static void Capture(Window window, string name)
    {
        using var frame = new RenderTargetBitmap(PixelSize.FromSize(window.ClientSize, 1));
        frame.Render(window);
        using var stream = new MemoryStream();
        frame.Save(stream, PngBitmapEncoderOptions.Default);
        using var pixels = SkiaSharp.SKBitmap.Decode(stream.ToArray());
        Assert.Contains(pixels.Pixels, pixel => pixel != pixels.GetPixel(0, 0));
        Directory.CreateDirectory(CaptureDirectory);
        frame.Save(Path.Combine(CaptureDirectory, name + ".png"), PngBitmapEncoderOptions.Default);
    }
}
