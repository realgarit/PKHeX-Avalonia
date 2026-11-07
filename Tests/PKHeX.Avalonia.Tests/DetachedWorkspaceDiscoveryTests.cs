using System.Linq;
using Avalonia.Controls;
using Avalonia.Automation;
using Avalonia.Headless.XUnit;
using Avalonia.LogicalTree;
using Avalonia.VisualTree;
using PKHeX.Avalonia.Tests.Harness;
using PKHeX.Avalonia.Views;
using PKHeX.Core;
using PKHeX.Presentation.Localization;
using PKHeX.Presentation.ViewModels;

namespace PKHeX.Avalonia.Tests;

/// <summary>
/// Issue #247: visible pop-out and dock-back actions for Box, Party and the Pokémon editor, the
/// Window menu entries, and the save-change lifetime of every detached window.
/// </summary>
public sealed class DetachedWorkspaceDiscoveryTests
{
    [AvaloniaFact]
    public void PokemonEditorDetachesAsTheSingleSharedEditorAndDocksBack()
    {
        using var app = new HeadlessAppFixture();
        app.LoadSaveInstance(new SAV6XY());
        var vm = app.ViewModel;
        var editor = vm.CurrentPokemonEditor!;
        Assert.Same(editor, vm.EmbeddedPokemonEditor);

        vm.OpenPokemonEditorWorkspaceCommand.Execute(null);
        app.Pump();

        // The window hosts the same editor ViewModel; the main window drops its copy of the view.
        var host = Assert.IsType<DetachedPokemonEditorViewModel>(app.Windows.ShownTools.Single().ViewModel);
        Assert.Same(editor, host.Editor);
        Assert.True(vm.IsPokemonEditorDetached);
        Assert.Null(vm.EmbeddedPokemonEditor);
        Assert.Equal(LocalizedStrings.Instance["Workspace_PokemonEditorWindowTitle"], app.Windows.ShownTools.Single().Title);
        Assert.True(app.FindByName<StackPanel>("DetachedEditorPlaceholder")!.IsVisible);
        Assert.DoesNotContain(app.Window.GetVisualDescendants().OfType<PokemonEditor>(), view => view.IsEffectivelyVisible);

        // Re-invoking focuses the existing window instead of creating a competing copy.
        vm.OpenPokemonEditorWorkspaceCommand.Execute(null);
        Assert.Single(app.Windows.ShownTools);
        Assert.Same(host, app.Windows.FocusedTools.Last());

        vm.SelectWorkspaceTabCommand.Execute(MainWindowViewModel.TrainerWorkspaceTabIndex);
        host.ReturnToMainWindowCommand.Execute(null);
        app.Pump();

        Assert.Contains(host, app.Windows.ClosedTools);
        Assert.False(vm.IsPokemonEditorDetached);
        Assert.Same(editor, vm.EmbeddedPokemonEditor);
        Assert.True(vm.IsPokemonWorkspace);
        Assert.False(app.FindByName<StackPanel>("DetachedEditorPlaceholder")!.IsVisible);
    }

    [AvaloniaFact]
    public void SaveChangeClosesTheDetachedEditorAndEmbedsTheNewOne()
    {
        using var app = new HeadlessAppFixture();
        app.LoadSaveInstance(new SAV6XY());
        var vm = app.ViewModel;
        vm.OpenPokemonEditorWorkspaceCommand.Execute(null);
        var host = Assert.IsType<DetachedPokemonEditorViewModel>(app.Windows.ShownTools.Single().ViewModel);

        app.LoadSaveInstance(new SAV7SM());

        Assert.Contains(host, app.Windows.ClosedTools);
        Assert.False(vm.IsPokemonEditorDetached);
        Assert.NotSame(host.Editor, vm.CurrentPokemonEditor);
        Assert.Same(vm.CurrentPokemonEditor, vm.EmbeddedPokemonEditor);

        // A stale window from the old save cannot dock (or hide) the new save's detached editor.
        vm.OpenPokemonEditorWorkspaceCommand.Execute(null);
        var current = Assert.IsType<DetachedPokemonEditorViewModel>(app.Windows.ShownTools.Last().ViewModel);
        Assert.Same(vm.CurrentPokemonEditor, current.Editor);
        host.ReturnToMainWindowCommand.Execute(null);
        Assert.True(vm.IsPokemonEditorDetached);
        Assert.DoesNotContain(current, app.Windows.ClosedTools);
    }

    [AvaloniaFact]
    public void DetachedBoxOffersDockBackThatShowsTheEmbeddedBox()
    {
        using var app = new HeadlessAppFixture();
        app.LoadSaveInstance(new SAV6XY());
        var vm = app.ViewModel;
        var embedded = app.BoxViewer!;
        Assert.False(embedded.IsDetached);

        vm.OpenBoxWorkspaceCommand.Execute(null);
        var detached = Assert.IsType<BoxViewerViewModel>(app.Windows.ShownTools.Single().ViewModel);
        Assert.True(detached.IsDetached);

        // The detached view shows dock-back instead of pop-out; the embedded view keeps pop-out.
        var detachedView = (BoxViewer)ViewLocator.Build(detached);
        detachedView.DataContext = detached;
        var window = new Window { Content = detachedView };
        window.Show();
        app.Pump();
        Assert.True(detachedView.FindControl<Button>("ReturnToMainWindowButton")!.IsVisible);
        Assert.False(detachedView.FindControl<Button>("PopOutButton")!.IsVisible);
        Assert.Equal(LocalizedStrings.Instance["Workspace_ReturnToMainWindow"],
            AutomationProperties.GetName(detachedView.FindControl<Button>("ReturnToMainWindowButton")!));
        window.Close();

        var embeddedView = app.Find<BoxViewer>()!;
        Assert.True(embeddedView.FindControl<Button>("PopOutButton")!.IsVisible);
        Assert.False(embeddedView.FindControl<Button>("ReturnToMainWindowButton")!.IsVisible);

        vm.SelectWorkspaceTabCommand.Execute(MainWindowViewModel.InventoryWorkspaceTabIndex);
        detached.ReturnToMainWindowCommand.Execute(null);

        Assert.Contains(detached, app.Windows.ClosedTools);
        Assert.Equal(MainWindowViewModel.BoxWorkspaceTabIndex, vm.SelectedWorkspaceIndex);
        Assert.True(vm.IsPokemonWorkspace);
    }

    [AvaloniaFact]
    public void PartyPopOutIsVisibleAndDockBackFollowsTheWindowLifetime()
    {
        using var app = new HeadlessAppFixture();
        app.LoadSaveInstance(new SAV6XY());
        var vm = app.ViewModel;
        var party = vm.PartyViewer!;

        // The party strip under the box grid has a visible, named pop-out button.
        var strip = app.Find<PartyStrip>()!;
        var popOut = strip.FindControl<Button>("PopOutButton")!;
        Assert.True(popOut.IsEffectivelyVisible);
        Assert.Equal(LocalizedStrings.Instance["PartyViewer_PopOut"], AutomationProperties.GetName(popOut));
        Assert.Equal(LocalizedStrings.Instance["PartyViewer_PopOut"], ToolTip.GetTip(popOut));

        app.Click(popOut);
        app.Pump();
        Assert.Same(party, app.Windows.ShownTools.Single().ViewModel);
        Assert.True(party.IsDetachedWindowOpen);

        vm.SelectWorkspaceTabCommand.Execute(MainWindowViewModel.TrainerWorkspaceTabIndex);
        party.ReturnToMainWindowCommand.Execute(null);
        Assert.False(party.IsDetachedWindowOpen);
        Assert.Contains(party, app.Windows.ClosedTools);
        Assert.Equal(MainWindowViewModel.PartyWorkspaceTabIndex, vm.SelectedWorkspaceIndex);

        // Closing the window through a save change also clears the dock-back state.
        vm.OpenPartyWorkspaceCommand.Execute(null);
        Assert.True(party.IsDetachedWindowOpen);
        app.LoadSaveInstance(new SAV6XY());
        Assert.False(party.IsDetachedWindowOpen);
    }

    [AvaloniaFact]
    public void WindowMenuListsEveryDetachableWorkspace()
    {
        using var app = new HeadlessAppFixture();
        app.LoadSaveInstance(new SAV6XY());
        var vm = app.ViewModel;
        var menuItems = app.Window.GetLogicalDescendants().OfType<MenuItem>().ToList();

        foreach (var command in new System.Windows.Input.ICommand[]
                 { vm.OpenBoxWorkspaceCommand, vm.OpenPartyWorkspaceCommand, vm.OpenPokemonEditorWorkspaceCommand })
        {
            var item = Assert.Single(menuItems, m => ReferenceEquals(m.Command, command));
            Assert.False(string.IsNullOrWhiteSpace(item.Header as string));
            Assert.True(command.CanExecute(null));
        }

        // The editor window is also part of the shared registry (Tools menu, shortcuts).
        Assert.Contains(vm.ToolMenuGroups.SelectMany(g => g.Items),
            item => ReferenceEquals(item.Command, vm.OpenPokemonEditorWorkspaceCommand) && item.IsAvailable);
    }

    [AvaloniaFact]
    public void DetachedPokemonEditorViewHasANamedDockBackAction()
    {
        using var app = new HeadlessAppFixture();
        app.LoadSaveInstance(new SAV6XY());
        var host = new DetachedPokemonEditorViewModel(app.ViewModel.CurrentPokemonEditor!, _ => { });
        var view = Assert.IsType<DetachedPokemonEditorView>(ViewLocator.Build(host));
        view.DataContext = host;
        var window = new Window { Content = view, Width = 420, Height = 700 };
        window.Show();
        app.Pump();

        var button = view.FindControl<Button>("ReturnToMainWindowButton")!;
        Assert.True(button.IsEffectivelyVisible);
        Assert.True(button.Bounds.Width > 0);
        Assert.Equal(LocalizedStrings.Instance["Workspace_ReturnToMainWindow"], AutomationProperties.GetName(button));
        Assert.Same(host.Editor, view.GetVisualDescendants().OfType<PokemonEditor>().Single().DataContext);
        window.Close();
    }
}
