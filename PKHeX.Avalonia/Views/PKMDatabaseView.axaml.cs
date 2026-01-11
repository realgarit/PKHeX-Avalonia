using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using PKHeX.Avalonia.ViewModels;

namespace PKHeX.Avalonia.Views;

public partial class PKMDatabaseView : UserControl
{
    public PKMDatabaseView()
    {
        InitializeComponent();
    }

    private void OnDoubleTapped(object? sender, TappedEventArgs e)
    {
        if (DataContext is PKMDatabaseViewModel vm && sender is DataGrid grid && grid.SelectedItem is PKMDatabaseEntry entry)
        {
            vm.SelectPokemonCommand.Execute(entry);
            
            // Close dialog if we are in one
            var window = VisualRoot as Window;
            window?.Close();
        }
    }
}
