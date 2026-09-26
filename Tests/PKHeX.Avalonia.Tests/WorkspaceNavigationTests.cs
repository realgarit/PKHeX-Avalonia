using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.VisualTree;
using PKHeX.Avalonia.Tests.Harness;
using PKHeX.Core;
using PKHeX.Presentation.ViewModels;

namespace PKHeX.Avalonia.Tests;

public class WorkspaceNavigationTests
{
    [AvaloniaFact]
    public void TopRowButtons_NavigateAndRemainSelectedAfterReports()
    {
        using var app = new HeadlessAppFixture();
        app.Window.Width = 900;
        app.Window.Height = 600;
        app.LoadSaveInstance(new SAV6XY());
        var navigation = app.Window.FindControl<StackPanel>("WorkspaceNavigation")!;
        var buttons = navigation.Children.OfType<Button>().ToArray();
        Assert.Equal(5, buttons.Length);
        void Click(int index) => app.Click(buttons[index]);
        Click(1);
        Assert.True(app.ViewModel.IsTrainerNavigationSelected);
        Click(4);
        Assert.True(app.ViewModel.IsReportsWorkspace);
        Click(1); // The underlying Trainer tab index did not change while Reports was open.
        Assert.True(app.ViewModel.IsTrainerNavigationSelected);
        Click(2);
        Assert.True(app.ViewModel.IsInventoryNavigationSelected);
        Click(3);
        Assert.True(app.ViewModel.IsSaveNavigationSelected);
        Click(0);
        Assert.True(app.ViewModel.IsPokemonWorkspace);
        Assert.Contains("active", buttons[0].Classes);
        Assert.All(buttons.Skip(1), button => Assert.DoesNotContain("active", button.Classes));
        var viewport = navigation.GetVisualAncestors().OfType<ScrollViewer>().First();
        Assert.True(buttons.Sum(button => button.Bounds.Width) + 12 <= viewport.Bounds.Width);
    }
}
