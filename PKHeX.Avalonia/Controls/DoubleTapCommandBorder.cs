using System.Windows.Input;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;

namespace PKHeX.Avalonia.Controls;

/// <summary>
/// A lightweight tab-header surface that invokes a command on a double tap.
/// Attaching the gesture to the header instead of the whole TabItem keeps content gestures
/// (for example Pokémon slot activation) in their own visual subtree.
/// </summary>
public sealed class DoubleTapCommandBorder : Border
{
    public static readonly StyledProperty<ICommand?> CommandProperty =
        AvaloniaProperty.Register<DoubleTapCommandBorder, ICommand?>(nameof(Command));

    public ICommand? Command
    {
        get => GetValue(CommandProperty);
        set => SetValue(CommandProperty, value);
    }

    public DoubleTapCommandBorder() => DoubleTapped += OnDoubleTapped;

    private void OnDoubleTapped(object? sender, TappedEventArgs e)
    {
        if (Command is not { } command || !command.CanExecute(null))
            return;

        command.Execute(null);
        e.Handled = true;
    }
}
