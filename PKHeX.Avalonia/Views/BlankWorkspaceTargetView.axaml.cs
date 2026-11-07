using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;

namespace PKHeX.Avalonia.Views;

public partial class BlankWorkspaceTargetView : UserControl
{
    public BlankWorkspaceTargetView()
    {
        InitializeComponent();
        // A double click on a game confirms it, like the explicit button.
        TargetList.DoubleTapped += OnTargetDoubleTapped;
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        // The suggested target (e.g. the game of an opened file) can be far down the list.
        if (TargetList.SelectedItem is { } selected)
            TargetList.ScrollIntoView(selected);
        TargetList.Focus();
    }

    private void OnTargetDoubleTapped(object? sender, TappedEventArgs e)
    {
        if (DataContext is PKHeX.Presentation.ViewModels.BlankWorkspaceTargetViewModel vm
            && vm.ConfirmCommand.CanExecute(null))
            vm.ConfirmCommand.Execute(null);
    }
}
