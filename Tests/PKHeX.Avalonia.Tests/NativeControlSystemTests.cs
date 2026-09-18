using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless.XUnit;
using Avalonia.Layout;
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

        Assert.Equal(new Color(0xFF, 0x4D, 0x51, 0x60), ((SolidColorBrush)combo.BorderBrush!).Color);
        Assert.Equal(new Color(0xFF, 0x4D, 0x51, 0x60), ((SolidColorBrush)text.BorderBrush!).Color);

        combo.IsDropDownOpen = true;
        Pump(window);
        var popupBorder = window.GetVisualDescendants()
            .OfType<Border>()
            .FirstOrDefault(border => border.Name == "PopupBorder");
        Assert.NotNull(popupBorder);
        Assert.Equal(new Color(0xFF, 0x22, 0x25, 0x2E), ((SolidColorBrush)popupBorder!.Background!).Color);

        window.Close();
    }

    [AvaloniaFact]
    public void UnqualifiedTextControls_ResolveVisibleForeground()
    {
        var text = new TextBlock { Text = "Default text" };
        var radio = new RadioButton { Content = "Choice" };
        var overlay = new TextBlock { Text = "Overlay text" };
        overlay.Classes.Add("overlay-text");
        var window = new Window
        {
            Content = new StackPanel { Children = { text, radio, overlay } },
            RequestedThemeVariant = global::Avalonia.Styling.ThemeVariant.Dark,
            Width = 240,
            Height = 120,
        };

        window.Show();
        Pump(window);

        AssertVisibleForeground((SolidColorBrush)text.Foreground!);
        AssertVisibleForeground((SolidColorBrush)radio.Foreground!);
        AssertVisibleForeground((SolidColorBrush)overlay.Foreground!);

        window.Close();
    }

    [AvaloniaFact]
    public void ActionControls_CenterContentOnOneSharedVerticalLine()
    {
        var button = new Button { Content = "Save" };
        var toggle = new ToggleButton { Content = "Workspace" };
        var checkBox = new CheckBox { Content = "Enabled" };
        var radio = new RadioButton { Content = "Option" };
        var window = new Window
        {
            Content = new StackPanel { Children = { button, toggle, checkBox, radio } },
            Width = 260,
            Height = 160,
        };

        window.Show();
        Pump(window);

        Assert.Equal(HorizontalAlignment.Center, button.HorizontalContentAlignment);
        Assert.Equal(VerticalAlignment.Center, button.VerticalContentAlignment);
        Assert.Equal(VerticalAlignment.Center, toggle.VerticalContentAlignment);
        Assert.Equal(VerticalAlignment.Center, checkBox.VerticalContentAlignment);
        Assert.Equal(VerticalAlignment.Center, radio.VerticalContentAlignment);

        window.Close();
    }

    private static void AssertVisibleForeground(SolidColorBrush brush)
    {
        Assert.Equal(255, brush.Color.A);
        static double Linear(byte value)
        {
            var channel = value / 255d;
            return channel <= 0.04045 ? channel / 12.92 : Math.Pow((channel + 0.055) / 1.055, 2.4);
        }
        static double Luminance(Color color) => 0.2126 * Linear(color.R) + 0.7152 * Linear(color.G) + 0.0722 * Linear(color.B);
        var contrast = (Luminance(brush.Color) + 0.05) / (Luminance(Color.Parse("#191B22")) + 0.05);
        Assert.True(contrast >= 4.5, $"Actual control text contrast was only {contrast:F2}:1.");
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
            .Where(button => button.Name is "LegalityPill" or "ShinyToggle")
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
