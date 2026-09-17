using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Moq;
using PKHeX.Application.Abstractions;
using PKHeX.Avalonia.Controls;
using PKHeX.Avalonia.Views;
using PKHeX.Core;
using PKHeX.Presentation.ViewModels;
namespace PKHeX.Avalonia.Tests;

public sealed class FilterableComboBoxGeometryTests
{
    private static readonly Thickness FluentTextControlPadding = new(10, 6, 6, 5);

    [AvaloniaFact]
    public void ActualEditorAndEncounterViews_RenderAllFilterableFieldsWithSharedGeometry()
    {
        var save = new SAV3E(new byte[0x20000]);
        var (pokemonEditorVm, _, _) = TestHelpers.CreateTestViewModel(new PK3(), save);
        var pokemonEditor = new PokemonEditor { DataContext = pokemonEditorVm };
        var editorWindow = Show(pokemonEditor, 900, 900);

        var encounterVm = new EncounterDatabaseViewModel(
            save,
            new Mock<ISpriteRenderer>().Object,
            new Mock<IDialogService>().Object,
            _ => { });
        var encounterView = new EncounterDatabaseView { DataContext = encounterVm };
        var encounterWindow = Show(encounterView, 900, 700);

        var editorFields = FindEditorFields(pokemonEditor, editorWindow);
        var encounterFields = encounterView.GetVisualDescendants().OfType<FilterableComboBox>().ToArray();

        Assert.Equal(10, editorFields.Count);
        Assert.Single(encounterFields);

        foreach (var field in editorFields.Concat(encounterFields))
            AssertGeometry(field);

        encounterWindow.Close();
        editorWindow.Close();
    }

    [AvaloniaFact]
    public void ActualEncounterField_PreservesAutomationFocusKeyboardAndSelection()
    {
        var save = new SAV3E(new byte[0x20000]);
        var viewModel = new EncounterDatabaseViewModel(
            save,
            new Mock<ISpriteRenderer>().Object,
            new Mock<IDialogService>().Object,
            _ => { });
        var view = new EncounterDatabaseView { DataContext = viewModel };
        var window = Show(view, 900, 700);
        var field = Assert.Single(view.GetVisualDescendants().OfType<FilterableComboBox>());

        Assert.Equal("Species", AutomationProperties.GetName(field));
        Assert.True(field.Focusable);
        Assert.True(field.Focus());
        Pump(window);
        Assert.True(field.IsDropDownOpen);

        window.KeyPressQwerty(PhysicalKey.Escape, RawInputModifiers.None);
        window.KeyReleaseQwerty(PhysicalKey.Escape, RawInputModifiers.None);
        Pump(window);
        Assert.False(field.IsDropDownOpen);

        var selected = viewModel.SpeciesList.Single(item => item.Value == 25);
        field.SelectedItem = selected;
        Pump(window);

        Assert.Equal(25, field.SelectedValue);
        Assert.Equal((ushort)25, viewModel.SelectedSpecies);
        Assert.Equal(selected.Text, field.Text);

        window.Close();
    }

    [AvaloniaFact]
    public void PlainAutoCompleteBox_DoesNotReceiveFilterableGeometry()
    {
        var field = new AutoCompleteBox { Width = 220 };
        var window = Show(field, 240, 80);

        Assert.Null(field.InnerRightContent);
        Assert.Empty(field.GetVisualDescendants().OfType<PathIcon>());

        window.Close();
    }

    private static void AssertGeometry(FilterableComboBox field)
    {
        field.ApplyTemplate();

        Assert.NotNull(field.Template);
        Assert.Equal(FluentTextControlPadding, field.Padding);
        Assert.Null(field.InnerRightContent);

        var textBox = field.GetVisualDescendants().OfType<TextBox>().Single();
        Assert.Equal(FluentTextControlPadding, textBox.Padding);
        Assert.Equal(TextAlignment.Start, textBox.TextAlignment);
        Assert.Equal(VerticalAlignment.Center, textBox.VerticalContentAlignment);

        var textPresenter = textBox.GetVisualDescendants().OfType<TextPresenter>().Single();
        Assert.Equal(TextAlignment.Start, textPresenter.TextAlignment);
        Assert.Equal(VerticalAlignment.Center, textPresenter.VerticalAlignment);

        Assert.False(string.IsNullOrWhiteSpace(AutomationProperties.GetName(field)));
    }

    private static IReadOnlyCollection<FilterableComboBox> FindEditorFields(PokemonEditor view, Window window)
    {
        var fields = new HashSet<FilterableComboBox>();
        var tabs = view.GetVisualDescendants().OfType<TabControl>().Single();

        foreach (var item in tabs.Items)
        {
            tabs.SelectedItem = item;
            Pump(window);
            fields.UnionWith(view.GetVisualDescendants().OfType<FilterableComboBox>());
        }

        return fields;
    }

    private static Window Show(Control content, double width, double height)
    {
        var window = new Window { Content = content, Width = width, Height = height };
        window.Show();
        Pump(window);
        return window;
    }

    private static void Pump(Window window)
    {
        Dispatcher.UIThread.RunJobs();
        window.UpdateLayout();
        Dispatcher.UIThread.RunJobs();
    }
}
