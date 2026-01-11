using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.VisualTree;

namespace PKHeX.Avalonia.Views;

public partial class LegalityView : UserControl
{
    public LegalityView()
    {
        InitializeComponent();
    }

    public LegalityView(string report) : this()
    {
        ReportText.Text = report;
    }

    private void OnCloseClick(object sender, RoutedEventArgs e)
    {
        var window = this.GetVisualRoot() as Window;
        window?.Close();
    }

    private async void OnCopyClick(object sender, RoutedEventArgs e)
    {
        var topLevel = TopLevel.GetTopLevel(this);
        if (topLevel?.Clipboard is not null)
        {
            await topLevel.Clipboard.SetTextAsync(ReportText.Text);
        }
    }
}
