using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using PKHeX.Presentation.ViewModels;

namespace PKHeX.Avalonia.Input;

/// <summary>
/// Runs the main window's configurable shortcuts, kept in step with
/// <see cref="MainWindowViewModel.ShortcutBindings"/>. Each shortcut is a <see cref="KeyBinding"/>
/// that this binder matches from the key events itself. The bindings are deliberately not added to
/// <see cref="InputElement.KeyBindings"/>.
/// </summary>
/// <remarks>
/// <para>
/// Key bindings run before the key event is raised, so they hide the key from the window's
/// <see cref="IAccessKeyHandler"/>. For a chord with Alt (Alt+F5, or Cmd+Option+N on macOS), the
/// handler then never learns that Alt was part of a chord, and releasing Alt opens the menu bar,
/// which takes keyboard focus. The access-key handler must see every key first.
/// </para>
/// <para>
/// The access-key handler listens for the tunnelling key event on the window. Tunnelling handlers of
/// one element run in reverse order of registration, so a tunnelling handler added to the window
/// later would run before it. The binder therefore matches keys in two places:
/// </para>
/// <list type="bullet">
/// <item>A tunnelling handler on the window's content, the next element on the route. The access-key
/// handler has seen the key, and the shortcut still runs before the focused control, as a window
/// key binding did.</item>
/// <item>A bubbling class handler on the window, for a key raised outside the content: the window
/// itself when no element has focus. Class handlers of an element run before its instance handlers,
/// so the access-key handler's bubbling handler, which runs Alt access keys, comes after it.</item>
/// </list>
/// <para>
/// The access-key handler does not open the menu bar on Alt's release when, during that Alt press, it
/// saw the Alt key and then another key raised inside the window. Since Avalonia 12 a key raised on
/// the window itself (no focused element) counts as inside; keys from outside the window, such as an
/// element of a separate top level, are ignored. The binder applies the same rule to the same key
/// events, with its own tunnelling handlers on the window, which run just before the access-key
/// handler's. When a shortcut with Alt ran during an Alt press whose release the access-key handler
/// would not ignore, the binder consumes that release. In all other cases the release is not
/// consumed, so the access-key handler resets its own state.
/// </para>
/// <para>
/// Each Alt press that this window sees starts the state again. A release that this window never gets
/// (because the shortcut moved focus to another window) therefore cannot make the binder consume a
/// later release. Deactivation does not reset the state: Alt can still be held when the window becomes
/// active again, and its release then belongs to the press that ran the shortcut.
/// </para>
/// <para>
/// The access-key handler only forgets an ignored release when it sees one, so a release that came up
/// in another top level would make it ignore the window's next Alt tap too. When the binder sees that
/// happen, it gives the handler the missed release (an Alt key-up raised on the window) just before the
/// window's next Alt press.
/// </para>
/// <para>
/// Only keys that come from this window are handled. A native popup (a menu or a context menu) is a
/// separate top level whose key events still route through the window's content; window key
/// bindings never ran for it either.
/// </para>
/// <para>
/// Rebinding while a shortcut runs is safe: the handlers match on a copy of the list.
/// </para>
/// </remarks>
internal sealed class KeyboardShortcutBinder : IDisposable
{
    private readonly Window _window;
    private readonly InputElement _content;
    private readonly MainWindowViewModel _viewModel;
    private readonly IDisposable _windowHandler;
    private readonly IDisposable _otherTopLevelsHandler;
    private readonly List<KeyBinding> _owned = [];
    private KeyEventArgs? _seenByContent;
    private bool _disposed;

    // The current Alt press, as the access-key handler sees it (see remarks).
    private bool _accessKeysSawAltPress;
    private bool _accessKeysIgnoreAltRelease;
    private bool _altShortcutRan;
    // That ignored release came up in another top level, so the access-key handler still waits for it.
    private bool _accessKeysMissedAltRelease;

    /// <exception cref="InvalidOperationException">The window has no content yet.</exception>
    public KeyboardShortcutBinder(Window window, MainWindowViewModel viewModel)
    {
        _window = window ?? throw new ArgumentNullException(nameof(window));
        _viewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
        _content = window.Content as InputElement
            ?? throw new InvalidOperationException("The window's content must exist before its shortcuts are bound.");

        _viewModel.ShortcutBindingsChanged += OnShortcutBindingsChanged;
        // Added after the access-key handler's, so they run just before it (tunnelling handlers run in
        // reverse order) and get the same keys with the same focus. Like it, they skip handled keys.
        _window.AddHandler(InputElement.KeyDownEvent, OnWindowPreviewKeyDown, RoutingStrategies.Tunnel);
        _window.AddHandler(InputElement.KeyUpEvent, OnWindowPreviewKeyUp, RoutingStrategies.Tunnel);
        _content.AddHandler(InputElement.KeyDownEvent, OnContentKeyDown, RoutingStrategies.Tunnel, handledEventsToo: true);
        _windowHandler = InputElement.KeyDownEvent.AddClassHandler<Window>(OnWindowKeyDown, RoutingStrategies.Bubble);
        // A key routed through this window tunnels through it first, so its own handlers have already
        // seen the release when another top level on the same route (a popup root) gets it.
        _otherTopLevelsHandler = InputElement.KeyUpEvent.AddClassHandler<TopLevel>(OnTopLevelPreviewKeyUp, RoutingStrategies.Tunnel, handledEventsToo: true);
        Rebind();
    }

    /// <summary>The shortcut bindings this binder currently matches.</summary>
    internal IReadOnlyList<KeyBinding> OwnedBindings => _owned;

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;
        _viewModel.ShortcutBindingsChanged -= OnShortcutBindingsChanged;
        _window.RemoveHandler(InputElement.KeyDownEvent, OnWindowPreviewKeyDown);
        _window.RemoveHandler(InputElement.KeyUpEvent, OnWindowPreviewKeyUp);
        _content.RemoveHandler(InputElement.KeyDownEvent, OnContentKeyDown);
        _windowHandler.Dispose();
        _otherTopLevelsHandler.Dispose();
        _owned.Clear();
        _seenByContent = null;
        ResetAltPress();
        _accessKeysMissedAltRelease = false;
    }

    private void OnWindowPreviewKeyDown(object? sender, KeyEventArgs e)
    {
        var inside = IsRaisedInsideWindow(e);
        if (IsAltKey(e.Key))
        {
            if (_accessKeysMissedAltRelease)
            {
                _accessKeysMissedAltRelease = false;
                _window.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyUpEvent, Key = e.Key, PhysicalKey = e.PhysicalKey });
            }
            ResetAltPress();
            _accessKeysSawAltPress = inside;
        }
        else if (_accessKeysSawAltPress && inside)
        {
            _accessKeysIgnoreAltRelease = true;
        }
    }

    private void OnTopLevelPreviewKeyUp(TopLevel topLevel, KeyEventArgs e)
    {
        if (ReferenceEquals(topLevel, _window) || !IsAltKey(e.Key) || !_accessKeysSawAltPress)
            return;

        _accessKeysMissedAltRelease = _accessKeysIgnoreAltRelease;
        ResetAltPress();
    }

    private void OnWindowPreviewKeyUp(object? sender, KeyEventArgs e)
    {
        if (!IsAltKey(e.Key))
            return;

        if (_altShortcutRan && !_accessKeysIgnoreAltRelease)
            e.Handled = true;
        ResetAltPress();
    }

    private void OnContentKeyDown(object? sender, KeyEventArgs e)
    {
        _seenByContent = e;
        TryHandle(e);
    }

    // Keys that tunnelled through the content were matched there, handled or not.
    private void OnWindowKeyDown(Window window, KeyEventArgs e)
    {
        if (!ReferenceEquals(window, _window) || ReferenceEquals(e, _seenByContent))
            return;

        TryHandle(e);
    }

    private void TryHandle(KeyEventArgs e)
    {
        if (_disposed || e.Handled || _owned.Count == 0)
            return;
        if (!ReferenceEquals(TopLevel.GetTopLevel(e.Source as Visual), _window))
            return;

        foreach (var binding in _owned.ToArray())
        {
            binding.TryHandle(e);
            if (!e.Handled)
                continue;

            if ((e.KeyModifiers & KeyModifiers.Alt) != 0)
                _altShortcutRan = true;
            return;
        }
    }

    private void ResetAltPress()
    {
        _accessKeysSawAltPress = false;
        _accessKeysIgnoreAltRelease = false;
        _altShortcutRan = false;
    }

    // The access-key handler's own test: the key's source is the window or an element inside it.
    private bool IsRaisedInsideWindow(KeyEventArgs e) =>
        e.Source is Visual source && (ReferenceEquals(source, _window) || _window.IsVisualAncestorOf(source));

    private static bool IsAltKey(Key key) => key is Key.LeftAlt or Key.RightAlt;

    private void OnShortcutBindingsChanged(object? sender, EventArgs e)
    {
        if (Dispatcher.UIThread.CheckAccess())
            Rebind();
        else
            Dispatcher.UIThread.Post(Rebind);
    }

    private void Rebind()
    {
        if (_disposed)
            return;

        _owned.Clear();
        foreach (var entry in _viewModel.ShortcutBindings)
        {
            _owned.Add(new KeyBinding
            {
                Gesture = ShortcutGestureMapper.ToKeyGesture(entry.Chord),
                Command = new ShortcutKeyCommand(entry, _window),
            });
        }
    }
}
