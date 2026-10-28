using System.Windows.Input;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Interactivity;
using PKHeX.Avalonia.Input;

namespace PKHeX.Avalonia.Controls;

/// <summary>
/// A button that records a key combination. A click, Enter or Space starts recording; the next
/// non-modifier key (with the modifiers held) is passed to <see cref="RecordCommand"/> as a
/// <c>ShortcutChord?</c>. While recording, Escape cancels, Backspace or Delete runs
/// <see cref="ClearCommand"/>, and Tab ends recording without being consumed so focus still moves.
/// </summary>
/// <remarks>
/// Key bindings on ancestors run before this control's key handler, so a dialog's Escape binding must
/// be unavailable while <see cref="IsRecording"/> is true for Escape to reach the recorder.
/// The control is styled as a <see cref="Button"/>; the <c>:recording</c> pseudo-class marks the
/// recording state.
/// </remarks>
public sealed class ShortcutRecorder : Button
{
    public static readonly StyledProperty<bool> IsRecordingProperty =
        AvaloniaProperty.Register<ShortcutRecorder, bool>(nameof(IsRecording), defaultBindingMode: BindingMode.TwoWay);

    public static readonly StyledProperty<ICommand?> RecordCommandProperty =
        AvaloniaProperty.Register<ShortcutRecorder, ICommand?>(nameof(RecordCommand));

    public static readonly StyledProperty<ICommand?> ClearCommandProperty =
        AvaloniaProperty.Register<ShortcutRecorder, ICommand?>(nameof(ClearCommand));

    public static readonly StyledProperty<string?> PartialTextProperty =
        AvaloniaProperty.Register<ShortcutRecorder, string?>(nameof(PartialText));

    public static readonly StyledProperty<string?> ShortcutTextProperty =
        AvaloniaProperty.Register<ShortcutRecorder, string?>(nameof(ShortcutText));

    public static readonly StyledProperty<string?> RecordingTextProperty =
        AvaloniaProperty.Register<ShortcutRecorder, string?>(nameof(RecordingText));

    // Modifier keys pressed during this recording and not released since. Both keys of one modifier
    // (the left and right Ctrl keys) can be held, and releasing one leaves the modifier held.
    private readonly HashSet<Key> _modifierKeysDown = [];

    protected override Type StyleKeyOverride => typeof(Button);

    /// <summary>True while the control waits for a key combination.</summary>
    public bool IsRecording
    {
        get => GetValue(IsRecordingProperty);
        set => SetValue(IsRecordingProperty, value);
    }

    /// <summary>Receives the recorded <c>ShortcutChord?</c>.</summary>
    public ICommand? RecordCommand
    {
        get => GetValue(RecordCommandProperty);
        set => SetValue(RecordCommandProperty, value);
    }

    /// <summary>Runs when Backspace or Delete is pressed while recording.</summary>
    public ICommand? ClearCommand
    {
        get => GetValue(ClearCommandProperty);
        set => SetValue(ClearCommandProperty, value);
    }

    /// <summary>The modifiers held so far, for example "Ctrl+…"; <see langword="null"/> before any.</summary>
    public string? PartialText
    {
        get => GetValue(PartialTextProperty);
        set => SetValue(PartialTextProperty, value);
    }

    /// <summary>Text shown while not recording: the current shortcut.</summary>
    public string? ShortcutText
    {
        get => GetValue(ShortcutTextProperty);
        set => SetValue(ShortcutTextProperty, value);
    }

    /// <summary>Prompt shown while recording, until a modifier is held.</summary>
    public string? RecordingText
    {
        get => GetValue(RecordingTextProperty);
        set => SetValue(RecordingTextProperty, value);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property == IsRecordingProperty)
        {
            PseudoClasses.Set(":recording", IsRecording);
            _modifierKeysDown.Clear();
            if (!IsRecording)
                PartialText = null;
            UpdateContent();
        }
        else if (change.Property == PartialTextProperty
                 || change.Property == ShortcutTextProperty
                 || change.Property == RecordingTextProperty)
        {
            UpdateContent();
        }
    }

    protected override void OnClick()
    {
        if (!IsRecording)
            IsRecording = true;
        base.OnClick();
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (!IsRecording)
        {
            base.OnKeyDown(e);
            return;
        }

        var key = e.Key;
        var modifiers = e.KeyModifiers;

        // Tab (and Shift+Tab) ends recording but is left unhandled, so keyboard focus still moves.
        if (key == Key.Tab)
        {
            IsRecording = false;
            return;
        }

        e.Handled = true;

        if (ShortcutGestureMapper.IsNonShortcutKey(key))
        {
            var held = modifiers | ModifierOf(key);
            ForgetModifierKeysNotHeld(held);
            if (ModifierOf(key) != KeyModifiers.None)
                _modifierKeysDown.Add(key);
            ShowHeld(held);
            return;
        }

        if (modifiers == KeyModifiers.None && key == Key.Escape)
        {
            IsRecording = false;
            return;
        }

        if (modifiers == KeyModifiers.None && key is (Key.Back or Key.Delete))
        {
            Run(ClearCommand, null);
            IsRecording = false;
            return;
        }

        Run(RecordCommand, ShortcutGestureMapper.FromKeyEvent(key, modifiers));
        IsRecording = false;
    }

    protected override void OnKeyUp(KeyEventArgs e)
    {
        // The release of a recorded key must not click the button again.
        if (IsRecording)
        {
            e.Handled = true;

            // Show only the modifiers that are still held. Platforms differ on whether the released
            // modifier is still in KeyModifiers, so remove it explicitly. It stays only while its other
            // key (for example the right Ctrl key after the left one is released) is still down.
            _modifierKeysDown.Remove(e.Key);
            ForgetModifierKeysNotHeld(e.KeyModifiers);
            var held = e.KeyModifiers & ~ModifierOf(e.Key);
            foreach (var down in _modifierKeysDown)
                held |= ModifierOf(down);
            ShowHeld(held);
            return;
        }

        base.OnKeyUp(e);
    }

    protected override void OnLostFocus(FocusChangedEventArgs e)
    {
        base.OnLostFocus(e);
        IsRecording = false;
    }

    // A key event reports every modifier that is held. A modifier key whose modifier is not reported
    // was released while this control did not get the key (for example in another window).
    private void ForgetModifierKeysNotHeld(KeyModifiers reported) =>
        _modifierKeysDown.RemoveWhere(down => (reported & ModifierOf(down)) == KeyModifiers.None);

    private void ShowHeld(KeyModifiers held) =>
        PartialText = held == KeyModifiers.None ? null : FormatPartial(held);

    private void UpdateContent() =>
        Content = IsRecording ? PartialText ?? RecordingText : ShortcutText;

    private static void Run(ICommand? command, object? parameter)
    {
        if (command?.CanExecute(parameter) == true)
            command.Execute(parameter);
    }

    private static KeyModifiers ModifierOf(Key key) => key switch
    {
        Key.LeftCtrl or Key.RightCtrl => KeyModifiers.Control,
        Key.LeftShift or Key.RightShift => KeyModifiers.Shift,
        Key.LeftAlt or Key.RightAlt => KeyModifiers.Alt,
        Key.LWin or Key.RWin => KeyModifiers.Meta,
        _ => KeyModifiers.None,
    };

    // "Ctrl+Shift" becomes "Ctrl+Shift+…"; a symbol-style format such as "⇧⌘" becomes "⇧⌘…".
    private static string FormatPartial(KeyModifiers held)
    {
        var text = new KeyGesture(Key.None, held).ToString("p", null);
        return text.Length > 0 && char.IsLetterOrDigit(text[^1]) ? text + "+…" : text + "…";
    }
}
