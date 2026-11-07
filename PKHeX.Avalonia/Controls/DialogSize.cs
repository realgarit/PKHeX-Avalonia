using Avalonia;
using Avalonia.Controls;

namespace PKHeX.Avalonia.Controls;

/// <summary>
/// Preferred first-frame size of a view that is hosted in a measured dialog or tool window.
/// </summary>
/// <remarks>
/// A dialog sizes itself to its measured content. Lists, grids and long item collections measure to
/// their full height, so without a preference such a dialog opens as large as the work area (the
/// O-Power, Pokémon Link and Underground editors once used most of a 1024 x 768 desktop), and a
/// data grid has no realized rows before the window opens, so its measured height is meaningless.
/// A declared preference is therefore the first-frame size. It only sets the initial bounds: the
/// window stays resizable, the view still stretches, and a smaller work area still wins.
/// </remarks>
public static class DialogSize
{
    public static readonly AttachedProperty<double> PreferredWidthProperty =
        AvaloniaProperty.RegisterAttached<Control, double>("PreferredWidth", typeof(DialogSize), double.NaN);

    public static readonly AttachedProperty<double> PreferredHeightProperty =
        AvaloniaProperty.RegisterAttached<Control, double>("PreferredHeight", typeof(DialogSize), double.NaN);

    public static double GetPreferredWidth(Control control) => control.GetValue(PreferredWidthProperty);
    public static void SetPreferredWidth(Control control, double value) => control.SetValue(PreferredWidthProperty, value);
    public static double GetPreferredHeight(Control control) => control.GetValue(PreferredHeightProperty);
    public static void SetPreferredHeight(Control control, double value) => control.SetValue(PreferredHeightProperty, value);

    /// <summary>Returns <paramref name="available"/> reduced to the view's preferred size, when it declares one.</summary>
    public static Size Limit(Control? control, Size available)
    {
        if (control is null)
            return available;
        var width = GetPreferredWidth(control);
        var height = GetPreferredHeight(control);
        return new Size(
            double.IsFinite(width) && width > 0 ? Math.Min(width, available.Width) : available.Width,
            double.IsFinite(height) && height > 0 ? Math.Min(height, available.Height) : available.Height);
    }
}
