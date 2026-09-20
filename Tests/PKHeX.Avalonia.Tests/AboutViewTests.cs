using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Avalonia.VisualTree;
using PKHeX.Avalonia.Views;
using PKHeX.Presentation.Localization;
using PKHeX.Presentation.ViewModels;

namespace PKHeX.Avalonia.Tests;

public sealed class AboutViewTests
{
    [AvaloniaFact]
    public void AboutActionsShareNativeGeometryAndKeepBottomInset()
    {
        var view = new AboutView { DataContext = new AboutViewModel() };
        var window = new Window { Content = view, Width = 400, Height = 344 };
        window.Show();

        try
        {
            Dispatcher.UIThread.RunJobs();
            window.UpdateLayout();
            AvaloniaHeadlessPlatform.ForceRenderTimerTick();
            Dispatcher.UIThread.RunJobs();

            var check = view.GetVisualDescendants()
                .OfType<Button>()
                .Single(button => Equals(button.Content, LocalizedStrings.Instance["Update_CheckNow"]));
            var close = view.GetVisualDescendants()
                .OfType<Button>()
                .Single(button => Equals(button.Content, LocalizedStrings.Instance["Common_Close"]));

            Assert.Equal(check.Bounds.Height, close.Bounds.Height);
            Assert.Contains("compact-secondary", check.Classes);

            var closeBottom = close.TranslatePoint(new Point(0, close.Bounds.Height), view)!.Value.Y;
            Assert.True(view.Bounds.Height - closeBottom >= 8,
                $"About actions have only {view.Bounds.Height - closeBottom:0.##}px of bottom inset.");
        }
        finally
        {
            window.Close();
        }
    }
}
