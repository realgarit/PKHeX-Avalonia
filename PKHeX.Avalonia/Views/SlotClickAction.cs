using Avalonia.Input;

namespace PKHeX.Avalonia.Views;

internal enum SlotClickAction
{
    None,
    View,
    Set,
    Delete,
}

internal static class SlotClickActionResolver
{
    /// <summary>
    /// Resolves the platform-neutral Avalonia modifier state for a slot click.
    /// Avalonia reports macOS Option (⌥) as <see cref="KeyModifiers.Alt"/>.
    /// </summary>
    public static SlotClickAction Resolve(KeyModifiers modifiers)
    {
        if (modifiers.HasFlag(KeyModifiers.Control))
            return SlotClickAction.View;

        if (modifiers.HasFlag(KeyModifiers.Shift))
            return SlotClickAction.Set;

        if (modifiers.HasFlag(KeyModifiers.Alt))
            return SlotClickAction.Delete;

        return SlotClickAction.None;
    }
}
