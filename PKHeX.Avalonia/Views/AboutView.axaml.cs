using Avalonia.Controls;
using Avalonia.Interactivity;

namespace PKHeX.Avalonia.Views;

public partial class AboutView : UserControl
{
    public AboutView()
    {
        InitializeComponent();
    }

    private void OnCloseClick(object sender, RoutedEventArgs e)
    {
        if (VisualRoot is Window window)
            window.Close();
    }
}
