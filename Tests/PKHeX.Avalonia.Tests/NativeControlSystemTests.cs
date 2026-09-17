using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;
using PKHeX.Avalonia.Controls;
using PKHeX.Avalonia.Views;
using PKHeX.Core;
using Xunit;

namespace PKHeX.Avalonia.Tests;

public sealed class NativeControlSystemTests
{
    [AvaloniaFact]
    public void NativeFields_ResolveSharedDensityGeometry()
    {
        var combo = new ComboBox
        {
            ItemsSource = new[]
            {
                new ComboItem("Male", 0),
                new ComboItem("Female", 1),
            },
            SelectedIndex = 0,
        };
        var filterable = new FilterableComboBox
        {
            ItemsSource = new[]
            {
                new ComboItem("Bulbasaur", 1),
                new ComboItem("Ivysaur", 2),
            },
            SelectedValue = 1,
        };
        var text = new TextBox { Text = "PKHeX" };
        var numeric = new NumericUpDown { Value = 25, Minimum = 0, Maximum = 100 };
        var date = new CalendarDatePicker { SelectedDate = new DateTime(2026, 9, 17) };
        var window = new Window
        {
            Content = new StackPanel
            {
                Spacing = 8,
                Children = { combo, filterable, text, numeric, date },
            },
            Width = 420,
            Height = 300,
        };

        window.Show();
        Pump(window);

        Assert.Equal(new Thickness(12, 5, 0, 7), combo.Padding);
        Assert.Equal(new Thickness(10, 6, 6, 5), filterable.Padding);
        Assert.Equal(new Thickness(10, 5), text.Padding);
        Assert.Equal(new Thickness(10, 4), numeric.Padding);
        Assert.Equal(new Thickness(10, 5), date.Padding);
        Assert.Equal(new CornerRadius(7), combo.CornerRadius);
        Assert.Equal(new CornerRadius(7), text.CornerRadius);
        Assert.Equal(new CornerRadius(7), numeric.CornerRadius);

        Assert.Equal(new Color(0xFF, 0x41, 0x53, 0x63), ((SolidColorBrush)combo.BorderBrush!).Color);
        Assert.Equal(new Color(0xFF, 0x41, 0x53, 0x63), ((SolidColorBrush)text.BorderBrush!).Color);

        combo.IsDropDownOpen = true;
        Pump(window);
        var popupBorder = window.GetVisualDescendants()
            .OfType<Border>()
            .FirstOrDefault(border => border.Name == "PopupBorder");
        Assert.NotNull(popupBorder);
        Assert.Equal(new Color(0xFF, 0x17, 0x23, 0x2D), ((SolidColorBrush)popupBorder!.Background!).Color);

        window.Close();
    }

    [AvaloniaFact]
    public void NumericTemplate_RemovesFluentInnerSeam()
    {
        var numeric = new NumericUpDown { Value = 25, Minimum = 0, Maximum = 100 };
        var window = new Window { Content = numeric, Width = 220, Height = 80 };
        window.Show();
        Pump(window);

        var textBox = numeric.GetVisualDescendants()
            .OfType<TextBox>()
            .Single(text => text.Name == "PART_TextBox");
        Assert.Equal(new Thickness(0), textBox.Margin);
        Assert.Equal(0, textBox.BorderThickness.Left);
        Assert.Equal(0, textBox.BorderThickness.Top);

        window.Close();
    }

    [AvaloniaFact]
    public void PokemonEditor_StatusActionsShareOneCenterline()
    {
        var save = new SAV9SV();
        var pk = new PK9 { Species = (ushort)Species.Pikachu, CurrentLevel = 55 };
        var (viewModel, _, _) = TestHelpers.CreateTestViewModel(pk, save);
        var view = new PokemonEditor { DataContext = viewModel };
        var window = new Window { Content = view, Width = 620, Height = 240 };
        window.Show();
        Pump(window);

        var actions = view.GetVisualDescendants()
            .OfType<Button>()
            .Where(button => button.Classes.Contains("entity-status-action"))
            .ToArray();

        Assert.Equal(2, actions.Length);
        Assert.All(actions, action => Assert.Equal(26, action.Bounds.Height));
        var centers = actions.Select(action => action.Bounds.Y + action.Bounds.Height / 2).ToArray();
        Assert.InRange(Math.Abs(centers[0] - centers[1]), 0, 0.01);

        window.Close();
    }

    private static void Pump(Window window)
    {
        Dispatcher.UIThread.RunJobs();
        window.UpdateLayout();
        Dispatcher.UIThread.RunJobs();
    }
}
