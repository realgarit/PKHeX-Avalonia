using System.Text.RegularExpressions;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using ChevronPath = Avalonia.Controls.Shapes.Path;
using Avalonia.Headless.XUnit;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;
using PKHeX.Avalonia.Controls;

namespace PKHeX.Avalonia.Tests;

public sealed class FilterableComboBoxGeometryTests
{
    private static readonly Thickness FluentTextControlPadding = new(10, 6, 6, 5);

    [AvaloniaFact]
    public void AllFilterableFields_UseOneSharedFilterableGeometryContract()
    {
        var repoRoot = FindRepoRoot();
        var editorSource = File.ReadAllText(Path.Combine(repoRoot, "PKHeX.Avalonia", "Views", "PokemonEditor.axaml"));
        var encounterSource = File.ReadAllText(Path.Combine(repoRoot, "PKHeX.Avalonia", "Views", "EncounterDatabaseView.axaml"));

        Assert.True(Regex.Matches(editorSource, @"<controls:FilterableComboBox\b").Count == 10,
            "PokemonEditor.axaml must retain its 10 FilterableComboBox fields.");
        Assert.True(Regex.Matches(encounterSource, @"<controls:FilterableComboBox\b").Count == 1,
            "EncounterDatabaseView.axaml must retain its one FilterableComboBox field.");

        var controls = Enumerable.Range(0, 11)
            .Select(_ => new FilterableComboBox { Width = 220 })
            .ToArray();
        var panel = new StackPanel { Spacing = 4 };
        foreach (var control in controls)
            panel.Children.Add(control);

        var window = new Window { Content = panel, Width = 240, Height = 600 };
        window.Show();
        Dispatcher.UIThread.RunJobs();
        window.UpdateLayout();
        Dispatcher.UIThread.RunJobs();

        foreach (var control in controls)
        {
            control.ApplyTemplate();

            Assert.NotNull(control.Template);
            Assert.Equal(FluentTextControlPadding, control.Padding);
            Assert.NotNull(control.InnerRightContent);
            var textBox = control.GetVisualDescendants().OfType<TextBox>().Single();
            Assert.Equal(FluentTextControlPadding, textBox.Padding);

            var textPresenter = textBox.GetVisualDescendants().OfType<TextPresenter>().Single();
            Assert.Equal(TextAlignment.Left, textPresenter.TextAlignment);
            Assert.Equal(VerticalAlignment.Center, textBox.VerticalContentAlignment);
            Assert.Equal(VerticalAlignment.Center, textPresenter.VerticalAlignment);

            var chevron = control.GetVisualDescendants().OfType<ChevronPath>()
                .Single(path => path.Width == 12 && path.Height == 6);
            Assert.Equal(HorizontalAlignment.Right, chevron.HorizontalAlignment);
            Assert.Equal(VerticalAlignment.Center, chevron.VerticalAlignment);
            Assert.Equal(new Thickness(0, 0, 10, 0), chevron.Margin);
            Assert.False(chevron.IsHitTestVisible);
            Assert.False(chevron.Focusable);
            Assert.True(chevron.Bounds.Right <= control.Bounds.Right);
        }

        Assert.DoesNotContain("Padding=\"8,0,30,0\"", editorSource + encounterSource);
        Assert.DoesNotContain("<Path Data=\"M 0,0 L 12,0 L 6,6 Z\"", editorSource + encounterSource);
    }

    private static string FindRepoRoot()
    {
        var directory = AppContext.BaseDirectory;
        while (!Directory.Exists(Path.Combine(directory, "PKHeX.Avalonia")))
        {
            var parent = Directory.GetParent(directory);
            if (parent is null)
                throw new DirectoryNotFoundException("Could not find repository root.");
            directory = parent.FullName;
        }

        return directory;
    }
}
