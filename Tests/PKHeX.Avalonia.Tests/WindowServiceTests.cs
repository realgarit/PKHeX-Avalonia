using Avalonia.Controls;
using Moq;
using PKHeX.Core;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using PKHeX.Avalonia.Services;
using PKHeX.Avalonia.Tests.Shortcuts;
using PKHeX.Avalonia.Views;
using PKHeX.Presentation.ViewModels;
using Xunit;

namespace PKHeX.Avalonia.Tests;

public sealed class WindowServiceTests
{
    [Fact]
    public void ClampInitialBounds_BoundsLargeTabDrivenContentToLaptopWorkspace()
    {
        var bounds = WindowService.ClampInitialBounds(1380, 804, 978, 760);

        Assert.Equal(978, bounds.Width);
        Assert.Equal(760, bounds.Height);
    }

    [Fact]
    public void ClampInitialBoundsProvidesStableMinimumForCompactTabs()
    {
        var bounds = WindowService.ClampInitialBounds(352, 288, 978, 760);

        Assert.Equal(420, bounds.Width);
        Assert.Equal(300, bounds.Height);
    }

    // ---------------------------------------------------------------------
    // macOS report (Discord #bug-reports, 2026-11-03): on a Retina MacBook Air the PKM Database
    // opened about 690 wide, below its 800-wide view minimum, and its filter pane and result
    // columns were cut off. Screen bounds are desktop units; dividing them by the render scale
    // (2 on Retina, while macOS desktop units are points) halved every dialog's size limit.
    // ---------------------------------------------------------------------

    [Theory]
    // macOS: working area reported in points, DesktopScaling 1 (render scale 2 must not apply).
    [InlineData(1470, 870, 1, 1200, 822)]
    // Windows at 200 %: working area in physical pixels, DesktopScaling 2.
    [InlineData(2940, 1740, 2, 1200, 822)]
    // Windows at 100 % on a small laptop.
    [InlineData(1366, 728, 1, 1200, 680)]
    // A broken scale value falls back to 1 instead of producing infinity or NaN.
    [InlineData(1024, 700, 0, 976, 652)]
    public void ComputeAvailableToolSize_UsesDesktopUnits(double width, double height, double desktopScaling, double expectedWidth, double expectedHeight)
    {
        var size = WindowService.ComputeAvailableToolSize(width, height, desktopScaling);

        Assert.Equal(expectedWidth, size.Width);
        Assert.Equal(expectedHeight, size.Height);
    }

    [AvaloniaFact]
    public async Task ShowDialogAsync_KeepsThePkmDatabaseAtOrAboveItsViewMinimum()
    {
        var main = new Window { Width = 1000, Height = 700 };
        main.Show();
        var service = new WindowService(() => main);
        var vm = new PKMDatabaseViewModel(new SAV7USUM(), Mock.Of<ISpriteRenderer>(), Mock.Of<IDialogService>());
        try
        {
            var task = service.ShowDialogAsync(vm, "database");
            Dispatcher.UIThread.RunJobs();
            var dialog = Assert.Single(main.OwnedWindows);
            var view = Assert.IsType<PKMDatabaseView>(dialog.Content);

            // The dialog can never be resized below the view's own minimum, so Avalonia never
            // centers an oversized view and clips both of its edges.
            Assert.True(dialog.MinWidth >= view.MinWidth, $"MinWidth {dialog.MinWidth} < view {view.MinWidth}");
            Assert.True(dialog.MinHeight >= view.MinHeight, $"MinHeight {dialog.MinHeight} < view {view.MinHeight}");
            Assert.True(dialog.Width >= view.MinWidth);
            Assert.True(dialog.Height >= view.MinHeight);
            Assert.True(dialog.CanResize);

            dialog.Close();
            await task;
        }
        finally
        {
            foreach (var window in main.OwnedWindows.ToArray())
                window.Close();
            main.Close();
        }
    }

    // ---------------------------------------------------------------------
    // Nested modal dialogs (issue #224): a dialog opened from another dialog, such as the shortcut
    // editor opened from Settings, is owned by that dialog; size limits still follow the main window.
    // ---------------------------------------------------------------------

    [AvaloniaFact]
    public async Task ShowDialogAsync_OwnsANestedDialogByTheDialogThatOpenedIt()
    {
        var main = new Window { Width = 1000, Height = 700 };
        main.Show();
        var service = new WindowService(() => main);
        try
        {
            var outerTask = service.ShowDialogAsync(new AboutViewModel(), "outer");
            Dispatcher.UIThread.RunJobs();
            var outer = Assert.Single(main.OwnedWindows);
            Assert.IsType<AboutView>(outer.Content);

            var innerTask = service.ShowDialogAsync(new LegalityViewModel("report"), "inner");
            Dispatcher.UIThread.RunJobs();
            var inner = Assert.Single(outer.OwnedWindows);
            Assert.IsType<LegalityView>(inner.Content);
            Assert.Same(outer, Assert.Single(main.OwnedWindows));

            inner.Close();
            await innerTask;
            Assert.Empty(outer.OwnedWindows);

            // The outer dialog owns the next one again once the inner one closed.
            var againTask = service.ShowDialogAsync(new LegalityViewModel("again"), "again");
            Dispatcher.UIThread.RunJobs();
            var again = Assert.Single(outer.OwnedWindows);
            again.Close();
            await againTask;

            outer.Close();
            await outerTask;
            Assert.Empty(main.OwnedWindows);

            // With no dialog open, the main window is the owner.
            var topTask = service.ShowDialogAsync(new LegalityViewModel("top"), "top");
            Dispatcher.UIThread.RunJobs();
            var top = Assert.Single(main.OwnedWindows);
            Assert.IsType<LegalityView>(top.Content);
            top.Close();
            await topTask;
        }
        finally
        {
            foreach (var window in main.OwnedWindows.ToArray())
                window.Close();
            main.Close();
        }
    }

    [AvaloniaFact]
    public async Task ShowDialogAsync_SizesTheShortcutEditorFromTheMainWindow_AndReleasesItOnClose()
    {
        var main = new Window { Width = 1000, Height = 700 };
        main.Show();
        var service = new WindowService(() => main);
        var (_, editor, shortcuts) = ShortcutTestViewModels.CreateEditor(new AppSettings(), new FakeSettingsStore());
        try
        {
            var outerTask = service.ShowDialogAsync(new AboutViewModel(), "outer");
            Dispatcher.UIThread.RunJobs();
            var outer = Assert.Single(main.OwnedWindows);

            var editorTask = service.ShowDialogAsync(editor, "shortcuts");
            Dispatcher.UIThread.RunJobs();
            var dialog = Assert.Single(outer.OwnedWindows);

            Assert.IsType<KeyboardShortcutsView>(dialog.Content);
            Assert.Equal(SizeToContent.Manual, dialog.SizeToContent);
            Assert.Equal(WindowService.GetMaxWindowWidth(main), dialog.MaxWidth);
            Assert.Equal(WindowService.GetToolMaxHeight(main), dialog.MaxHeight);
            Assert.Equal(Math.Min(640, dialog.MaxWidth), dialog.Width);
            Assert.Equal(Math.Min(580, dialog.MaxHeight), dialog.Height);
            Assert.Equal(Math.Min(480, dialog.MaxWidth), dialog.MinWidth);
            Assert.Equal(Math.Min(400, dialog.MaxHeight), dialog.MinHeight);

            // Closing the window (the title-bar close box, not Cancel) disposes the editor, which
            // lets the main-window shortcuts run again.
            Assert.NotNull(editor.CloseRequested);
            Assert.True(shortcuts.IsEditorOpen);
            dialog.Close();
            await editorTask;
            Assert.False(shortcuts.IsEditorOpen);
            Assert.Empty(outer.OwnedWindows);

            outer.Close();
            await outerTask;
        }
        finally
        {
            foreach (var window in main.OwnedWindows.ToArray())
                window.Close();
            main.Close();
        }
    }
}
