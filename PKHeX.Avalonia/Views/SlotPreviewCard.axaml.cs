using Avalonia.Controls;

namespace PKHeX.Avalonia.Views;

/// <summary>
/// The rich slot hover card (issue #244). Its DataContext is a framework-free
/// <see cref="PKHeX.Presentation.ViewModels.SlotPreviewViewModel"/>; <see cref="Controls.SlotHoverPreview"/>
/// hosts it in a non-focusable tooltip popup.
/// </summary>
public partial class SlotPreviewCard : UserControl
{
    public SlotPreviewCard()
    {
        InitializeComponent();
    }
}
