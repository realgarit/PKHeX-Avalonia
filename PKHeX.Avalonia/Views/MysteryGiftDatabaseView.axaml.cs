using Avalonia.Controls;
using Avalonia.Input;
using PKHeX.Avalonia.ViewModels;

namespace PKHeX.Avalonia.Views;

public partial class MysteryGiftDatabaseView : UserControl
{
    public MysteryGiftDatabaseView()
    {
        InitializeComponent();
    }

    private void OnDoubleTapped(object? sender, TappedEventArgs e)
    {
        if (sender is DataGrid dg && dg.SelectedItem is MysteryGiftDatabaseEntry entry)
        {
            if (DataContext is MysteryGiftDatabaseViewModel vm)
            {
                vm.SelectGiftCommand.Execute(entry);
                // Close the dialog - this is usually handled by the IDialogService.
                // But we need a way to close this specific window.
                // In this implementation, the IDialogService handles the window lifecycle.
            }
        }
    }
}
