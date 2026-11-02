using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.LogicalTree;
using Avalonia.Platform.Storage;
using PKHeX.Avalonia.Input;
using PKHeX.Avalonia.Services;
using PKHeX.Presentation.ViewModels;

namespace PKHeX.Avalonia.Views;

public partial class MainWindow : Window
{
    private bool _closeApproved;
    private bool _closePending;
    private KeyboardShortcutBinder? _shortcutBinder;
    private static readonly IDisposable MenuSubmenuHandlerRegistration =
        MenuItem.IsSubMenuOpenProperty.Changed.AddClassHandler<MenuItem>(OnSubmenuOpenChanged);

    public MainWindow()
    {
        InitializeComponent();
        GC.KeepAlive(MenuSubmenuHandlerRegistration);
    }

    /// <summary>The configurable shortcut bindings of the current view model, if one is attached.</summary>
    internal KeyboardShortcutBinder? ShortcutBinder => _shortcutBinder;

    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);

        // The configurable shortcuts follow the main view model; App and the test harness both set
        // DataContext after InitializeComponent, and teardown clears it.
        _shortcutBinder?.Dispose();
        _shortcutBinder = DataContext is MainWindowViewModel vm ? new KeyboardShortcutBinder(this, vm) : null;
    }

    protected override void OnClosed(EventArgs e)
    {
        _shortcutBinder?.Dispose();
        _shortcutBinder = null;
        base.OnClosed(e);
    }

    protected override async void OnClosing(WindowClosingEventArgs e)
    {
        if (!_closeApproved && DataContext is MainWindowViewModel vm)
        {
            e.Cancel = true;
            if (_closePending) return;
            _closePending = true;
            try
            {
                if (await vm.CanLeaveCurrentSaveAsync())
                {
                    _closeApproved = true;
                    Close();
                }
            }
            finally
            {
                _closePending = false;
            }
            return;
        }
        base.OnClosing(e);
    }

    private static void OnSubmenuOpenChanged(MenuItem opened, AvaloniaPropertyChangedEventArgs e)
    {
        if (e.NewValue is not true || GetTopLevelMenuItem(opened) is not { } topLevel)
            return;

        if (topLevel.Parent is Menu menu)
        {
            foreach (var sibling in menu.ItemsPanelRoot?.Children.OfType<MenuItem>() ?? [])
            {
                if (!ReferenceEquals(sibling, topLevel))
                    sibling.IsSubMenuOpen = false;
            }
        }

        foreach (var sibling in EnumerateMenuItems(topLevel).Where(item =>
                     !ReferenceEquals(item, opened) && item.IsSubMenuOpen && !IsMenuAncestor(item, opened)))
        {
            sibling.IsSubMenuOpen = false;
        }
    }

    private static MenuItem? GetTopLevelMenuItem(MenuItem item)
    {
        var current = item;
        while (current.Parent is MenuItem parent)
            current = parent;

        return current.Parent is Menu ? current : null;
    }

    private static IEnumerable<MenuItem> EnumerateMenuItems(MenuItem owner)
    {
        var seen = new HashSet<MenuItem>();
        var pending = new Stack<MenuItem>();
        AddChildren(owner);

        while (pending.Count > 0)
        {
            var item = pending.Pop();
            yield return item;
            AddChildren(item);
        }

        void AddChildren(MenuItem parent)
        {
            foreach (var child in parent.ItemsPanelRoot?.Children.OfType<MenuItem>() ?? [])
                Add(child);
            if (parent is ILogical logical)
            {
                foreach (var child in logical.LogicalChildren.OfType<MenuItem>())
                    Add(child);
            }
        }

        void Add(MenuItem item)
        {
            if (seen.Add(item))
                pending.Push(item);
        }
    }

    private static bool IsMenuAncestor(MenuItem candidate, MenuItem item)
    {
        for (var parent = item.Parent as MenuItem; parent is not null; parent = parent.Parent as MenuItem)
        {
            if (ReferenceEquals(parent, candidate))
                return true;
        }

        return false;
    }

    // Fallback for OS files dropped anywhere on the window that weren't already handled by a
    // more specific target (a box/party slot, or the editor panel) — e.g. a save file dropped
    // over the trainer/inventory tabs. Save files open (same path as File > Open); Pokémon
    // entity files load into the current editor.
    private async void OnWindowDrop(object? sender, DragEventArgs e)
    {
        // The editor sprite drag only targets box and party slots; a miss must not reimport it.
        if (e.Handled || DataContext is not MainWindowViewModel vm || SlotDragTransfer.HasEditorPayload(e.DataTransfer))
            return;

        var files = e.DataTransfer.TryGetFiles();
        if (files is not { Length: > 0 })
            return;

        var paths = files.Select(f => f.TryGetLocalPath()).OfType<string>().ToList();
        if (paths.Count == 0)
            return;

        e.Handled = true;
        await vm.HandleWindowFileDropAsync(paths);
    }

    private void OnLauncherItemClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is MainWindowViewModel vm)
            vm.CloseToolLauncherCommand.Execute(null);
    }
}
