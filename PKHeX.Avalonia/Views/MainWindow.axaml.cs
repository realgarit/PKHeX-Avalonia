using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Platform.Storage;
using Avalonia.VisualTree;
using PKHeX.Presentation.ViewModels;

namespace PKHeX.Avalonia.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
    }

    private void OnBoxTabDoubleTapped(object? sender, TappedEventArgs e)
    {
        // A slot double-tap bubbles through the TabItem. Let BoxViewer keep ownership of its
        // existing slot activation gesture instead of opening a detached window as a side effect.
        if (IsInsideViewer<BoxViewer>(e.Source))
            return;

        if (DataContext is MainWindowViewModel vm && vm.OpenBoxWorkspaceCommand.CanExecute(null))
            vm.OpenBoxWorkspaceCommand.Execute(null);
    }

    private void OnPartyTabDoubleTapped(object? sender, TappedEventArgs e)
    {
        if (IsInsideViewer<PartyViewer>(e.Source))
            return;

        if (DataContext is MainWindowViewModel vm && vm.OpenPartyWorkspaceCommand.CanExecute(null))
            vm.OpenPartyWorkspaceCommand.Execute(null);
    }

    private static bool IsInsideViewer<TViewer>(object? source) where TViewer : Visual
    {
        for (var visual = source as Visual; visual is not null; visual = visual.GetVisualParent())
        {
            if (visual is TViewer)
                return true;
        }

        return false;
    }

    // Fallback for OS files dropped anywhere on the window that weren't already handled by a
    // more specific target (a box/party slot, or the editor panel) — e.g. a save file dropped
    // over the trainer/inventory tabs. Save files open (same path as File > Open); Pokémon
    // entity files load into the current editor.
    private async void OnWindowDrop(object? sender, DragEventArgs e)
    {
        if (e.Handled || DataContext is not MainWindowViewModel vm)
            return;

        var files = e.DataTransfer.TryGetFiles();
        if (files is not { Length: > 0 })
            return;

        var paths = files.Select(f => f.TryGetLocalPath()).OfType<string>().ToList();
        if (paths.Count == 0)
            return;

        e.Handled = true;
        await vm.HandleWindowFileDropAsync(paths);
    }
}
