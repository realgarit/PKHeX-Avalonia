using Avalonia.Controls;

namespace PKHeX.Avalonia.Views;

public partial class BoxViewer : UserControl
{
    public BoxViewer()
    {
        InitializeComponent();

        // Focus the control when it becomes visible for keyboard navigation
        AttachedToVisualTree += (_, _) => Focus();
    }
}
