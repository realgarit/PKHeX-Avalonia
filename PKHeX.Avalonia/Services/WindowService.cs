using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
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
    // Open modeless tool windows, keyed by their ViewModel instance, so re-invoking focuses
    // the existing window instead of opening a duplicate.
    private readonly Dictionary<object, Window> _tools = new();

    // Remembered size/position per tool ViewModel type, so a reopened tool (even after the VM
    // is rebuilt on save change) returns to where the user last left it for this session.
    private static readonly Dictionary<string, (PixelPoint Position, double Width, double Height)> ToolBounds = new();

    public async Task ShowDialogAsync(object viewModel, string title)
    {
        var owner = MainWindow;
        if (owner is null) return;

        var isSettings = viewModel is SettingsViewModel;
        var dialog = new Window
        {
            Title = title,
            Content = ViewLocator.Build(viewModel),
            SizeToContent = isSettings ? SizeToContent.Manual : SizeToContent.WidthAndHeight,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            CanResize = true,
            MaxWidth = isSettings ? 620 : GetMaxWindowWidth(owner),
            MaxHeight = isSettings ? 820 : GetToolMaxHeight(owner),
        };

        if (isSettings)
        {
            dialog.Width = 390;
            dialog.Height = 492;
            dialog.MinWidth = 390;
            dialog.MinHeight = 420;
        }
        else
        {
            StabilizeInitialBounds(dialog);
        }

        if (viewModel is ICloseableDialog closeable)
            closeable.CloseRequested = dialog.Close;

        await dialog.ShowDialog(owner);
    }

    public void ShowTool(object viewModel, string title)
    {
        // Already open for this ViewModel? Bring it forward instead of duplicating.
        if (_tools.TryGetValue(viewModel, out var existing))
        {
            existing.Activate();
            return;
        }

        var owner = MainWindow;
        if (owner is null) return;

        var key = viewModel.GetType().FullName ?? viewModel.GetType().Name;
        var maxToolHeight = GetToolMaxHeight(owner);
        var window = new Window
        {
            Title = title,
            Content = ViewLocator.Build(viewModel),
            CanResize = true,
            MaxWidth = GetMaxWindowWidth(owner),
            MaxHeight = maxToolHeight,
        };

        if (ToolBounds.TryGetValue(key, out var bounds))
        {
            window.WindowStartupLocation = WindowStartupLocation.Manual;
            window.Position = bounds.Position;
            window.Width = bounds.Width;
            window.Height = Math.Min(bounds.Height, maxToolHeight);
        }
        else
        {
            window.WindowStartupLocation = WindowStartupLocation.CenterOwner;
            window.SizeToContent = SizeToContent.WidthAndHeight;
            StabilizeInitialBounds(window);
        }

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

    private static double GetToolMaxHeight(Window owner)
    {
        var screen = owner.Screens.ScreenFromWindow(owner) ?? owner.Screens.Primary;
        if (screen is null)
            return 760;

        var scaling = owner.RenderScaling > 0 ? owner.RenderScaling : 1;
        var workingHeight = screen.WorkingArea.Height / scaling;

        // Leave room for the native title bar and a small work-area margin. Without this,
        // SizeToContent tools can consume the full working area and clip their close button.
        return Math.Max(420, workingHeight - 48);
    }

    private static double GetMaxWindowWidth(Window owner)
    {
        var screen = owner.Screens.ScreenFromWindow(owner) ?? owner.Screens.Primary;
        if (screen is null)
            return 1200;

        var scaling = owner.RenderScaling > 0 ? owner.RenderScaling : 1;
        var workingWidth = screen.WorkingArea.Width / scaling;
        return Math.Clamp(workingWidth - 48, 620, 1200);
    }

    private static void StabilizeInitialBounds(Window window)
    {
        window.Opened += (_, _) =>
        {
            var measuredWidth = double.IsFinite(window.Width) && window.Width > 0 ? window.Width : window.Bounds.Width;
            var measuredHeight = double.IsFinite(window.Height) && window.Height > 0 ? window.Height : window.Bounds.Height;
            var (width, height) = ClampInitialBounds(measuredWidth, measuredHeight, window.MaxWidth, window.MaxHeight);

            window.Width = width;
            window.Height = height;
            window.SizeToContent = SizeToContent.Manual;
        };
    }

    internal static (double Width, double Height) ClampInitialBounds(
        double measuredWidth,
        double measuredHeight,
        double maxWidth,
        double maxHeight)
    {
        var minWidth = Math.Min(760, maxWidth);
        var minHeight = Math.Min(560, maxHeight);
        var width = Math.Clamp(double.IsFinite(measuredWidth) && measuredWidth > 0 ? measuredWidth : minWidth, minWidth, maxWidth);
        var height = Math.Clamp(double.IsFinite(measuredHeight) && measuredHeight > 0 ? measuredHeight : minHeight, minHeight, maxHeight);
        return (width, height);
    }
}
