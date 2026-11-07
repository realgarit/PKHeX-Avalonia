using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.VisualTree;
using PKHeX.Avalonia.Views;
using PKHeX.Presentation.Models;
using PKHeX.Presentation.ViewModels;

namespace PKHeX.Avalonia.Controls;

/// <summary>
/// Attaches the rich slot hover card (issue #244) to a box or party slot button.
/// <list type="bullet">
/// <item>Pointer hover opens the card after the configured delay, at the pointer. The card lives in
/// Avalonia's tooltip popup, which never takes focus and is moved back inside the screen near edges.</item>
/// <item>Keyboard focus (Tab or arrow navigation) opens the same card below the slot.</item>
/// <item>Screen readers get the card as plain text through <see cref="AutomationProperties.HelpTextProperty"/>.</item>
/// <item>With the setting off, or on an empty slot, the slot keeps its plain text tooltip.</item>
/// </list>
/// The card content is built only when it opens, from the viewer's <see cref="ISlotPreviewSource"/>.
/// </summary>
public static class SlotHoverPreview
{
    public static readonly AttachedProperty<bool> IsEnabledProperty =
        AvaloniaProperty.RegisterAttached<Control, bool>("IsEnabled", typeof(SlotHoverPreview));

    public static bool GetIsEnabled(Control control) => control.GetValue(IsEnabledProperty);
    public static void SetIsEnabled(Control control, bool value) => control.SetValue(IsEnabledProperty, value);

    static SlotHoverPreview()
    {
        IsEnabledProperty.Changed.AddClassHandler<Control>(OnIsEnabledChanged);
    }

    private static void OnIsEnabledChanged(Control control, AvaloniaPropertyChangedEventArgs e)
    {
        if (e.NewValue is true)
        {
            control.AddHandler(ToolTip.ToolTipOpeningEvent, OnToolTipOpening);
            control.AddHandler(InputElement.GotFocusEvent, OnGotFocus);
            control.AddHandler(InputElement.LostFocusEvent, OnLostFocus);
            control.AddHandler(InputElement.PointerExitedEvent, OnPointerExited);
            control.AttachedToVisualTree += OnAttached;
            control.DataContextChanged += OnDataContextChanged;
            Refresh(control);
        }
        else
        {
            control.RemoveHandler(ToolTip.ToolTipOpeningEvent, OnToolTipOpening);
            control.RemoveHandler(InputElement.GotFocusEvent, OnGotFocus);
            control.RemoveHandler(InputElement.LostFocusEvent, OnLostFocus);
            control.RemoveHandler(InputElement.PointerExitedEvent, OnPointerExited);
            control.AttachedToVisualTree -= OnAttached;
            control.DataContextChanged -= OnDataContextChanged;
        }
    }

    /// <summary>The viewer that owns the slot, or null when the slot is not hosted by one.</summary>
    public static ISlotPreviewSource? FindSource(Control control)
    {
        for (Visual? visual = control; visual is not null; visual = visual.GetVisualParent())
        {
            if (visual is StyledElement { DataContext: ISlotPreviewSource source } && !ReferenceEquals(visual, control))
                return source;
        }
        return null;
    }

    /// <summary>
    /// Builds the tooltip content the slot shows now: the rich card when the setting is on and the slot is
    /// occupied, otherwise the plain text summary. Exposed for tests.
    /// </summary>
    public static object? CreateTip(Control control)
    {
        if (!TryGetSlot(control, out var location, out var summary, out var isEmpty))
            return null;
        var source = FindSource(control);
        if (isEmpty || source is not { HoverSettings.Enabled: true })
            return summary;
        var preview = source.GetSlotPreview(location);
        if (preview is null)
            return summary;
        return new ToolTip
        {
            Content = new SlotPreviewCard { DataContext = preview },
            Padding = new Thickness(0),
            Background = Brushes.Transparent,
            BorderThickness = new Thickness(0),
            MaxWidth = 320,
        };
    }

    private static void OnAttached(object? sender, VisualTreeAttachmentEventArgs e)
    {
        if (sender is Control control)
            Refresh(control);
    }

    private static void OnDataContextChanged(object? sender, EventArgs e)
    {
        if (sender is Control control)
            Refresh(control);
    }

    /// <summary>Applies the current delay and the plain summary as tip and screen-reader help text.</summary>
    private static void Refresh(Control control)
    {
        if (FindSource(control) is { } source)
            ToolTip.SetShowDelay(control, source.HoverSettings.DelayMs);
        if (TryGetSlot(control, out _, out var summary, out _))
        {
            ToolTip.SetTip(control, summary);
            AutomationProperties.SetHelpText(control, summary);
        }
    }

    private static void OnToolTipOpening(object? sender, CancelRoutedEventArgs e)
    {
        if (sender is not Control control)
            return;
        var tip = CreateTip(control);
        if (tip is null)
        {
            e.Cancel = true;
            return;
        }
        ToolTip.SetTip(control, tip);
    }

    private static void OnPointerExited(object? sender, PointerEventArgs e)
    {
        // The tooltip service reads the delay when the pointer enters, so refresh it for the next hover.
        if (sender is Control control && FindSource(control) is { } source)
            ToolTip.SetShowDelay(control, source.HoverSettings.DelayMs);
    }

    private static void OnGotFocus(object? sender, FocusChangedEventArgs e)
    {
        if (sender is not Control control || !ReferenceEquals(e.Source, control))
            return;

        if (TryGetSlot(control, out var location, out _, out var isEmpty) && !isEmpty
            && FindSource(control) is { } source && source.GetSlotPreview(location) is { } preview)
        {
            // Screen readers get the same facts as the card, whether or not the card is shown.
            AutomationProperties.SetHelpText(control, preview.AccessibleText);
        }

        if (e.NavigationMethod is NavigationMethod.Tab or NavigationMethod.Directional
            && FindSource(control) is { HoverSettings.Enabled: true })
        {
            ToolTip.SetPlacement(control, PlacementMode.Bottom);
            ToolTip.SetIsOpen(control, true);
        }
    }

    private static void OnLostFocus(object? sender, FocusChangedEventArgs e)
    {
        if (sender is not Control control || !ReferenceEquals(e.Source, control))
            return;
        if (ToolTip.GetPlacement(control) == PlacementMode.Bottom)
        {
            ToolTip.SetIsOpen(control, false);
            ToolTip.SetPlacement(control, PlacementMode.Pointer);
        }
    }

    private static bool TryGetSlot(Control control, out SlotLocation location, out string summary, out bool isEmpty)
    {
        switch (control.DataContext)
        {
            case SlotData slot:
                (location, summary, isEmpty) = (slot.Location, slot.ToolTipSummary, slot.IsEmpty);
                return true;
            case PartySlotData slot:
                (location, summary, isEmpty) = (slot.Location, slot.ToolTipSummary, slot.IsEmpty);
                return true;
            default:
                (location, summary, isEmpty) = (default, string.Empty, true);
                return false;
        }
    }
}
