using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Controls.Primitives;
using PKHeX.Application.Abstractions;
using PKHeX.Presentation.ViewModels;

namespace PKHeX.Avalonia.Services;

/// <summary>
/// Shows a ViewModel as a modal dialog. The matching View is resolved via <see cref="ViewLocator"/>,
/// wrapped in a host <c>Window</c>, and shown over the main window — preserving the previous
/// modal/centered behavior while keeping View resolution out of the Presentation layer.
/// Also hosts modeless tool windows (see <see cref="ShowTool"/>).
/// </summary>
public sealed class WindowService : IWindowService
{
    private readonly Func<Window?> _getMainWindow;

    public WindowService() : this(() => MainWindow) { }

    internal WindowService(Func<Window?> getMainWindow) => _getMainWindow = getMainWindow;

    // Open modeless tool windows, keyed by their ViewModel instance, so re-invoking focuses
    // the existing window instead of opening a duplicate.
    private readonly Dictionary<object, Window> _tools = new();

    // Remembered size/position per tool ViewModel type, so a reopened tool (even after the VM
    // is rebuilt on save change) returns to where the user last left it for this session.
    private static readonly Dictionary<string, (PixelPoint Position, double Width, double Height)> ToolBounds = new();

    // Modal dialogs this service opened that are still open, oldest first. A dialog opened from
    // another dialog (the shortcut editor or an update dialog started from Settings) is owned by the
    // innermost one, so it is really modal over the window the user clicked in.
    private readonly List<Window> _openDialogs = [];

    public async Task ShowDialogAsync(object viewModel, string title)
    {
        // Size limits always follow the main window's screen; ownership follows the open dialogs.
        var main = _getMainWindow();
        if (main is null) return;
        var owner = _openDialogs.LastOrDefault(window => window.IsVisible) ?? main;

        var isSettings = viewModel is SettingsViewModel;
        var dialog = new Window
        {
            Title = title,
            Content = ViewLocator.Build(viewModel),
            SizeToContent = isSettings ? SizeToContent.Manual : SizeToContent.WidthAndHeight,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            CanResize = true,
            MaxWidth = isSettings ? 620 : GetMaxWindowWidth(main),
            MaxHeight = isSettings ? 820 : GetToolMaxHeight(main),
        };

        if (isSettings)
        {
            dialog.Width = 390;
            dialog.Height = 492;
            dialog.MinWidth = 390;
            dialog.MinHeight = 420;
        }
        else if (viewModel is UpdateDownloadViewModel or LegalityViewModel)
        {
            ConfigureCompactUtilityBounds(dialog, viewModel is LegalityViewModel, GetMaxWindowWidth(main), GetToolMaxHeight(main));
        }
        else if (viewModel is KeyboardShortcutsViewModel)
        {
            ConfigureKeyboardShortcutsBounds(dialog);
        }
        else
        {
            SetMeasuredInitialBounds(dialog);
        }

        if (viewModel is AboutViewModel)
            ConfigureAboutAutoSize(dialog);

        if (viewModel is CosmeticInventory4EditorViewModel)
        {
            dialog.SizeToContent = SizeToContent.Manual;
            dialog.Width = 620;
            dialog.Height = Math.Min(620, dialog.MaxHeight);
            dialog.MinWidth = 480;
            dialog.MinHeight = Math.Min(500, dialog.MaxHeight);
        }

        if (viewModel is Pokedex5EditorViewModel)
        {
            dialog.SizeToContent = SizeToContent.Manual;
            dialog.Width = Math.Min(900, dialog.MaxWidth);
            dialog.Height = Math.Min(650, dialog.MaxHeight);
            dialog.MinWidth = Math.Min(700, dialog.MaxWidth);
            dialog.MinHeight = Math.Min(500, dialog.MaxHeight);
        }

        if (viewModel is FashionEditorViewModel)
        {
            dialog.SizeToContent = SizeToContent.Manual;
            dialog.Width = Math.Min(700, dialog.MaxWidth);
            dialog.Height = Math.Min(620, dialog.MaxHeight);
            dialog.MinWidth = Math.Min(480, dialog.MaxWidth);
            dialog.MinHeight = Math.Min(460, dialog.MaxHeight);
        }

        if (viewModel is Fashion9EditorViewModel)
        {
            dialog.SizeToContent = SizeToContent.Manual;
            dialog.Width = Math.Min(850, dialog.MaxWidth);
            dialog.Height = Math.Min(650, dialog.MaxHeight);
            dialog.MinWidth = Math.Min(620, dialog.MaxWidth);
            dialog.MinHeight = Math.Min(500, dialog.MaxHeight);
        }

        if (viewModel is Raid9EditorViewModel)
        {
            dialog.SizeToContent = SizeToContent.Manual;
            dialog.Width = Math.Min(780, dialog.MaxWidth);
            dialog.Height = Math.Min(650, dialog.MaxHeight);
            dialog.MinWidth = Math.Min(480, dialog.MaxWidth);
            dialog.MinHeight = Math.Min(320, dialog.MaxHeight);
        }

        if (viewModel is RaidEditorViewModel)
        {
            dialog.SizeToContent = SizeToContent.Manual;
            dialog.Width = Math.Min(700, dialog.MaxWidth);
            dialog.Height = Math.Min(650, dialog.MaxHeight);
            dialog.MinWidth = Math.Min(480, dialog.MaxWidth);
            dialog.MinHeight = Math.Min(320, dialog.MaxHeight);
        }

        if (viewModel is RecordsEditorViewModel)
        {
            var key = typeof(RecordsEditorViewModel).FullName!;
            dialog.SizeToContent = SizeToContent.Manual;
            dialog.Width = ToolBounds.TryGetValue(key, out var remembered) ? Math.Min(remembered.Width, dialog.MaxWidth) : 620;
            dialog.Height = remembered.Height > 0 ? Math.Min(remembered.Height, dialog.MaxHeight) : 520;
            dialog.Closed += (_, _) => ToolBounds[key] = (dialog.Position, dialog.Width, dialog.Height);
        }

        if (viewModel is ICloseableDialog closeable)
            closeable.CloseRequested = dialog.Close;

        dialog.Closed += (_, _) => (viewModel as IDisposable)?.Dispose();

        _openDialogs.Add(dialog);
        dialog.Closed += (_, _) => _openDialogs.Remove(dialog);
        try
        {
            await dialog.ShowDialog(owner);
        }
        finally
        {
            _openDialogs.Remove(dialog);
        }
    }

    /// <summary>
    /// The shortcut editor is a long, scrolling list: a fixed initial size (remembered for the session
    /// once the user resizes it) instead of measuring every row.
    /// </summary>
    internal static void ConfigureKeyboardShortcutsBounds(Window dialog)
    {
        var key = typeof(KeyboardShortcutsViewModel).FullName!;
        dialog.SizeToContent = SizeToContent.Manual;
        dialog.WindowState = WindowState.Normal;
        dialog.MinWidth = Math.Min(480, dialog.MaxWidth);
        dialog.MinHeight = Math.Min(400, dialog.MaxHeight);
        var remembered = ToolBounds.TryGetValue(key, out var bounds) ? bounds : default;
        dialog.Width = remembered.Width > 0
            ? Math.Clamp(remembered.Width, dialog.MinWidth, dialog.MaxWidth)
            : Math.Min(640, dialog.MaxWidth);
        dialog.Height = remembered.Height > 0
            ? Math.Clamp(remembered.Height, dialog.MinHeight, dialog.MaxHeight)
            : Math.Min(580, dialog.MaxHeight);
        dialog.Closed += (_, _) =>
        {
            if (dialog.Width > 0 && dialog.Height > 0)
                ToolBounds[key] = (dialog.Position, dialog.Width, dialog.Height);
        };
    }

    internal static void ConfigureAboutAutoSize(Window dialog)
    {
        if (dialog.Content is not Control { DataContext: AboutViewModel vm } content) return;
        var closed = false;
        // Keep native initial bounds explicit, then remeasure at the user's current width after
        // bindings receive each asynchronous status change. Native SizeToContent can be disabled
        // by platform resize notifications, so it cannot own this dynamic content transition.
        void Resize()
        {
            if (closed) return;
            content.InvalidateMeasure();
            var width = dialog.ClientSize.Width > 0 ? dialog.ClientSize.Width : dialog.Width;
            content.Measure(new Size(width, dialog.MaxHeight));
            dialog.Height = Math.Clamp(content.DesiredSize.Height, Math.Min(300, dialog.MaxHeight), dialog.MaxHeight);
        }
        void StatusChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(AboutViewModel.UpdateCheckStatus))
                global::Avalonia.Threading.Dispatcher.UIThread.Post(Resize);
        }
        vm.PropertyChanged += StatusChanged;
        dialog.Closed += (_, _) => { closed = true; vm.PropertyChanged -= StatusChanged; };
        // The initial bounds were measured before the content was styled and against the wider working
        // area. Once open, size it like every later status change so clearing a status restores it.
        dialog.Opened += (_, _) => Resize();
    }

    public void ShowTool(object viewModel, string title)
    {
        // Already open for this ViewModel? Bring it forward instead of duplicating.
        if (_tools.TryGetValue(viewModel, out var existing))
        {
            existing.Activate();
            return;
        }

        var owner = _getMainWindow();
        if (owner is null) return;

        var key = viewModel.GetType().FullName ?? viewModel.GetType().Name;
        var availableSize = GetAvailableToolSize(owner);
        var maxToolHeight = availableSize.Height;
        var window = new Window
        {
            Title = title,
            Content = ViewLocator.Build(viewModel),
            CanResize = true,
            MaxWidth = availableSize.Width,
            MaxHeight = maxToolHeight,
        };

        if (ToolBounds.TryGetValue(key, out var bounds))
        {
            window.WindowStartupLocation = WindowStartupLocation.Manual;
            window.Position = bounds.Position;
            window.Width = Math.Min(bounds.Width, window.MaxWidth);
            window.Height = Math.Min(bounds.Height, maxToolHeight);
        }
        else
        {
            window.WindowStartupLocation = WindowStartupLocation.CenterOwner;
            if (viewModel is BoxReportViewModel)
            {
                window.Width = Math.Min(1100, window.MaxWidth);
                window.Height = Math.Min(600, maxToolHeight);
            }
            else
            {
                SetMeasuredInitialBounds(window);
            }
        }

        if (viewModel is BoxReportViewModel)
        {
            window.MinWidth = Math.Min(480, window.MaxWidth);
            window.MinHeight = Math.Min(300, maxToolHeight);
            window.Width = Math.Min(window.Width, window.MaxWidth);
        }

        ConstrainToolBounds(window);

        if (viewModel is ICloseableDialog closeable)
            closeable.CloseRequested = window.Close;

        window.Closed += (_, _) =>
        {
            // Remember where the user left it (skip if minimized/zeroed).
            if (window.Width > 0 && window.Height > 0)
                ToolBounds[key] = (window.Position, window.Width, window.Height);
            _tools.Remove(viewModel);
        };

        _tools[viewModel] = window;
        window.Show(owner);
    }

    public void CloseAllTools()
    {
        // Copy first: Close fires Closed handlers that mutate _tools.
        foreach (var window in _tools.Values.ToList())
            window.Close();
        _tools.Clear();
    }

    private static Window? MainWindow =>
        (global::Avalonia.Application.Current?.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime)?.MainWindow;

    internal static double GetToolMaxHeight(Window owner) => Math.Max(420, GetAvailableToolSize(owner).Height);

    internal static double GetMaxWindowWidth(Window owner) => Math.Max(620, GetAvailableToolSize(owner).Width);

    private static Size GetAvailableToolSize(Window owner)
    {
        var screen = owner.Screens.ScreenFromWindow(owner) ?? owner.Screens.Primary;
        if (screen is null)
            return new Size(1200, 760);

        var scaling = owner.RenderScaling > 0 ? owner.RenderScaling : 1;
        var workingWidth = screen.WorkingArea.Width / scaling;
        var workingHeight = screen.WorkingArea.Height / scaling;
        // Reserve the native title bar and a small work-area margin outside the client
        // area. Tool hosts can scroll below their view minima on a small work area;
        // modal dialogs retain their existing sizing policy through the helpers above.
        return new Size(Math.Clamp(workingWidth - 48, 1, 1200), Math.Max(1, workingHeight - 48));
    }

    internal static void ConstrainToolBounds(Window window)
    {
        if (window.Content is not Control content) return;

        // Window dimensions constrain the client area; native title bars are outside it.
        // Copy the view's layout minimum to its host so resizing/restoring cannot arrange
        // an oversized view outside the visible client rectangle (notably Legality Audit).
        var minWidth = content.MinWidth + content.Margin.Left + content.Margin.Right + window.Padding.Left + window.Padding.Right;
        var minHeight = content.MinHeight + content.Margin.Top + content.Margin.Bottom + window.Padding.Top + window.Padding.Bottom;
        window.MinWidth = Math.Min(Math.Max(window.MinWidth, minWidth), window.MaxWidth);
        window.MinHeight = Math.Min(Math.Max(window.MinHeight, minHeight), window.MaxHeight);

        // A small work area must remain usable even if the view cannot reflow below its
        // minimum. Keep that minimum inside a scrollable viewport instead of clipping it
        // or forcing the native window beyond the screen's available client size.
        if (minWidth > window.MaxWidth || minHeight > window.MaxHeight)
        {
            window.Content = null;
            window.Content = new ScrollViewer
            {
                Content = content,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            };
        }

        window.Width = Math.Clamp(window.Width, window.MinWidth, window.MaxWidth);
        window.Height = Math.Clamp(window.Height, window.MinHeight, window.MaxHeight);
    }

    internal static void SetMeasuredInitialBounds(Window window)
    {
        // Measure against a finite working area before creating the native window.
        // Reading Window.Width in Opened can adopt the platform's initial owner-sized
        // bounds instead of the content's intended dimensions on macOS.
        var content = window.Content as Control;
        content?.Measure(new Size(window.MaxWidth, window.MaxHeight));
        var desired = content?.DesiredSize ?? default;
        var (width, height) = ClampInitialBounds(desired.Width, desired.Height, window.MaxWidth, window.MaxHeight);
        window.SizeToContent = SizeToContent.Manual;
        window.WindowState = WindowState.Normal;
        window.Width = width;
        window.Height = height;
    }

    internal static (double Width, double Height) ClampInitialBounds(
        double measuredWidth,
        double measuredHeight,
        double maxWidth,
        double maxHeight)
    {
        // Keep compact editors compact. A broad global minimum made small native dialogs
        // (notably Daycare and other read-only tools) open with large empty margins.
        var minWidth = Math.Min(420, maxWidth);
        var minHeight = Math.Min(300, maxHeight);
        var width = Math.Clamp(double.IsFinite(measuredWidth) && measuredWidth > 0 ? measuredWidth : minWidth, minWidth, maxWidth);
        var height = Math.Clamp(double.IsFinite(measuredHeight) && measuredHeight > 0 ? measuredHeight : minHeight, minHeight, maxHeight);
        return (width, height);
    }

    internal static void ConfigureCompactUtilityBounds(Window window, bool legalityReport, double availableWidth, double availableHeight)
    {
        // Establish bounds before native showing/measurement, rather than adopting
        // a large initial platform size in the generic Opened callback.
        window.SizeToContent = SizeToContent.Manual;
        window.WindowState = WindowState.Normal;
        window.MaxWidth = Math.Min(legalityReport ? 960 : 640, availableWidth);
        window.MaxHeight = Math.Min(legalityReport ? 720 : 480, availableHeight);
        window.MinWidth = Math.Min(legalityReport ? 420 : 360, window.MaxWidth);
        window.MinHeight = Math.Min(legalityReport ? 300 : 180, window.MaxHeight);
        window.Width = Math.Min(legalityReport ? 620 : 480, window.MaxWidth);
        window.Height = Math.Min(legalityReport ? 440 : 240, window.MaxHeight);
    }
}
