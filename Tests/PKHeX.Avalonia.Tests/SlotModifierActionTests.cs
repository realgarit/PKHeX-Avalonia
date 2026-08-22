using Avalonia.Input;
using PKHeX.Avalonia.Views;

namespace PKHeX.Avalonia.Tests;

public class SlotModifierActionTests
{
    [Fact]
    public void MacOptionModifier_ResolvesToDelete()
    {
        // Avalonia's platform-neutral Alt modifier is macOS's Option (⌥) key.
        Assert.Equal(SlotClickAction.Delete, SlotClickActionResolver.Resolve(KeyModifiers.Alt));
    }

    [Fact]
    public void ControlModifier_ResolvesToView()
    {
        Assert.Equal(SlotClickAction.View, SlotClickActionResolver.Resolve(KeyModifiers.Control));
    }

    [Fact]
    public void ShiftModifier_ResolvesToSet()
    {
        Assert.Equal(SlotClickAction.Set, SlotClickActionResolver.Resolve(KeyModifiers.Shift));
    }

    [Fact]
    public void NoModifierClick_RemainsASelectionClick()
    {
        Assert.Equal(SlotClickAction.None, SlotClickActionResolver.Resolve(KeyModifiers.None));
    }
}
