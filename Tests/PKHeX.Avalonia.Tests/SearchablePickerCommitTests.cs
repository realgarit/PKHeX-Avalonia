using System.Collections.ObjectModel;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Data;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Moq;
using PKHeX.Avalonia.Controls;
using PKHeX.Avalonia.Tests.Fixtures;
using PKHeX.Avalonia.Views;
using PKHeX.Core;
using PKHeX.Presentation.Localization;
using PKHeX.Presentation.ViewModels;

namespace PKHeX.Avalonia.Tests;

public sealed class SearchablePickerCommitTests
{
    [AvaloniaTheory]
    [InlineData(0)] [InlineData(1)] [InlineData(2)]
    public void ActualAbilityPicker_DuplicateIdsCarryTheirSlotsAfterAcceptAndBlur(int slot)
    {
        var save = new SAV9SV();
        GameInfo.FilteredSources = new FilteredGameDataSource(save, GameInfo.Sources);
        var pk = new PK9 { Species = (ushort)Species.Mew, CurrentLevel = 50 };
        pk.RefreshAbility(0);
        var (vm, _, _) = TestHelpers.CreateTestViewModel(pk, save);
        Assert.Equal(3, vm.AbilityList.Count);
        Assert.Single(vm.AbilityList.Select(item => item.Value).Distinct());
        var view = new PokemonEditor { DataContext = vm };
        var blur = new Button { Content = "Next" };
        var panel = new DockPanel();
        DockPanel.SetDock(blur, Dock.Bottom);
        panel.Children.Add(blur); panel.Children.Add(view);
        var window = Show(panel, 900, 850);
        try
        {
            var picker = view.GetVisualDescendants().OfType<FilterableComboBox>()
                .Single(control => AutomationProperties.GetName(control) == LocalizedStrings.Instance["PokemonEditor_Ability"]);
            Open(picker, window);
            for (var i = 0; i <= slot; i++) Press(window, PhysicalKey.ArrowDown);
            Assert.Equal(0, vm.SelectedAbilityIndex);
            Press(window, PhysicalKey.Enter);
            Assert.Equal(slot, vm.SelectedAbilityIndex);
            Assert.False(picker.IsDropDownOpen);
            Assert.True(blur.Focus()); Pump(window);
            Assert.Equal(vm.AbilityChoices[slot].Text, picker.Text);
            Assert.Equal(vm.AbilityChoices[slot].Text, vm.AbilityTooltip);
            var prepared = vm.PreparePKM();
            Assert.Equal(pk.Ability, prepared.Ability);
            Assert.Equal(1 << slot, prepared.AbilityNumber);

            // A different Pokemon with the same raw ID must replace the displayed slot.
            var next = (PK9)pk.Clone(); next.RefreshAbility((slot + 1) % 3);
            Open(picker, window); Press(window, PhysicalKey.ArrowDown);
            Assert.True(vm.LoadPKM(next)); Pump(window);
            Press(window, PhysicalKey.Enter);
            Assert.Equal((slot + 1) % 3, vm.SelectedAbilityIndex);
            Assert.Equal(vm.AbilityChoices[(slot + 1) % 3].Text, picker.Text);
            Assert.Equal(next.AbilityNumber, vm.PreparePKM().AbilityNumber);

            // Regenerated labels must come from the new source even if the key is unchanged.
            var selected = vm.SelectedAbilityIndex;
            vm.AbilityList = new ObservableCollection<ComboItem>(vm.AbilityList.Select(item => item with { Text = "Translated " + item.Text }));
            Pump(window);
            Assert.Equal(selected, vm.SelectedAbilityIndex);
            Assert.Equal(vm.AbilityChoices[selected].Text, picker.Text);
            Assert.StartsWith("Translated ", picker.Text);
            vm.AbilityList[selected] = vm.AbilityList[selected] with { Text = "Mutated label" };
            Pump(window);
            Assert.Equal("Mutated label", picker.Text);
        }
        finally { window.Close(); }
    }

    [Fact]
    public void AbilityChoices_KeepRawApiAndHaXAssignments()
    {
        var save = new SAV9SV();
        GameInfo.FilteredSources = new FilteredGameDataSource(save, GameInfo.Sources, HaX: true);
        var pk = new PK9 { Species = (ushort)Species.Mew, CurrentLevel = 50 };
        pk.RefreshAbility(2);
        var (vm, _, _) = TestHelpers.CreateTestViewModel(pk, save, haXMode: true);
        var ability = vm.AbilityList.First(entry => entry.Value != pk.Ability).Value;
        vm.Ability = ability;
        Assert.Equal(ability, vm.PreparePKM().Ability);
        Assert.Equal(ability, vm.AbilityList[vm.SelectedAbilityIndex].Value);
        vm.SelectedAbilityIndex = vm.AbilityList.Count - 1;
        Assert.Equal(vm.AbilityList[^1].Value, vm.PreparePKM().Ability);
        vm.SelectedAbilityIndex = 0;
        Assert.Equal(vm.AbilityList[0].Value, vm.PreparePKM().Ability);
        vm.SelectedAbilityIndex = vm.AbilityList.ToList().FindIndex(entry => entry.Value == pk.Ability);
        Assert.Equal(pk.Ability, vm.PreparePKM().Ability);
        Assert.Equal(1, vm.PreparePKM().AbilityNumber);

        GameInfo.FilteredSources = new FilteredGameDataSource(save, GameInfo.Sources);
        var (normal, _, _) = TestHelpers.CreateTestViewModel(pk, save);
        Assert.Equal(2, normal.SelectedAbilityIndex);
        normal.Species = (int)Species.Eevee;
        normal.Ability = normal.AbilityList[1].Value;
        Assert.Equal(1, normal.SelectedAbilityIndex);
        Assert.Equal(2, normal.PreparePKM().AbilityNumber);
    }

    [AvaloniaTheory]
    [InlineData("Species")] [InlineData("HeldItem")] [InlineData("Move1")]
    public void ExistingPokemonPickers_ArrowEscapeIsADraftAndEnterStillCommits(string property)
    {
        var save = new SAV9SV();
        GameInfo.FilteredSources = new FilteredGameDataSource(save, GameInfo.Sources);
        var pk = new PK9 { Species = (ushort)Species.Mew, HeldItem = 17, Move1 = 33, CurrentLevel = 50 };
        var (vm, _, _) = TestHelpers.CreateTestViewModel(pk, save);
        var view = new PokemonEditor { DataContext = vm };
        var window = Show(view, 900, 850);
        try
        {
            if (property == "Move1")
            {
                view.GetVisualDescendants().OfType<TabControl>().Single().SelectedIndex = 3;
                Pump(window);
            }
            var name = property == "Move1" ? vm.Move1AutomationName : LocalizedStrings.Instance["PokemonEditor_" + property];
            var picker = view.GetVisualDescendants().OfType<FilterableComboBox>().Single(control => AutomationProperties.GetName(control) == name);
            var before = picker.SelectedValue;
            Open(picker, window); Press(window, PhysicalKey.ArrowDown); Press(window, PhysicalKey.ArrowDown);
            var candidate = Assert.IsType<ComboItem>(picker.SelectedItem);
            Assert.NotEqual(before, candidate.Value);
            Assert.Equal(before, picker.SelectedValue);
            Press(window, PhysicalKey.Escape);
            Assert.Equal(before, picker.SelectedValue);
            Press(window, PhysicalKey.ArrowDown); Press(window, PhysicalKey.ArrowDown); Press(window, PhysicalKey.ArrowDown);
            var accepted = Assert.IsType<ComboItem>(picker.SelectedItem).Value;
            Press(window, PhysicalKey.Enter);
            Assert.Equal(accepted, picker.SelectedValue);
            Assert.Equal(accepted, (int)typeof(PokemonEditorViewModel).GetProperty(property)!.GetValue(vm)!);
        }
        finally { window.Close(); }
    }

    [AvaloniaTheory]
    [InlineData(false)] [InlineData(true)]
    public void ActualZaInventory_DownEscapeApplyNeverWritesRowOrFlags(bool noMatch)
    {
        var save = LoadZa();
        SetFlags(save);
        var before = save.Items.Data.ToArray();
        var vm = new InventoryEditorViewModel(save, new Mock<ISpriteRenderer>().Object);
        var row = vm.Pouches.SelectMany(pouch => pouch.Items).Single(item => item.ItemId == 17);
        vm.SelectedPouch = vm.Pouches.Single(pouch => pouch.Items.Contains(row));
        var view = new InventoryEditor { DataContext = vm };
        var window = Show(view, 900, 650);
        try
        {
            var grid = view.GetVisualDescendants().OfType<DataGrid>().Single();
            grid.ScrollIntoView(row, null); Pump(window);
            var picker = view.GetVisualDescendants().OfType<FilterableComboBox>().Single(control => ReferenceEquals(control.DataContext, row));
            var writes = new List<string?>(); row.PropertyChanged += (_, args) => writes.Add(args.PropertyName);
            Open(picker, window);
            Press(window, PhysicalKey.ArrowDown);
            Assert.IsType<ComboItem>(picker.SelectedItem);
            Assert.NotEqual(17, ((ComboItem)picker.SelectedItem!).Value);
            AssertRowUnchanged(row); Assert.Empty(writes);
            if (noMatch)
            {
                window.KeyTextInput("this item does not exist"); Pump(window);
            }
            Press(window, PhysicalKey.Escape);
            Assert.False(picker.IsDropDownOpen);
            AssertRowUnchanged(row); Assert.Empty(writes);
            Assert.Equal(row.ItemList.Single(item => item.Value == 17).Text, picker.Text);
            vm.SaveCommand.Execute(null);
            Assert.Equal(before, save.Items.Data.ToArray());
        }
        finally { window.Close(); }
    }

    [AvaloniaTheory]
    [InlineData(PhysicalKey.Enter)] [InlineData(PhysicalKey.Tab)]
    public void ActualZaInventory_AcceptCommitsExactlyOnceAndEscapeKeepsAcceptedItem(PhysicalKey key)
    {
        var save = LoadZa(); SetFlags(save);
        var vm = new InventoryEditorViewModel(save, new Mock<ISpriteRenderer>().Object);
        var row = vm.Pouches.SelectMany(pouch => pouch.Items).Single(item => item.ItemId == 17);
        vm.SelectedPouch = vm.Pouches.Single(pouch => pouch.Items.Contains(row));
        var view = new InventoryEditor { DataContext = vm };
        var window = Show(view, 900, 650);
        try
        {
            view.GetVisualDescendants().OfType<DataGrid>().Single().ScrollIntoView(row, null); Pump(window);
            var picker = view.GetVisualDescendants().OfType<FilterableComboBox>().Single(control => ReferenceEquals(control.DataContext, row));
            var writes = 0; row.PropertyChanged += (_, args) => { if (args.PropertyName == nameof(row.ItemId)) writes++; };
            Open(picker, window); Press(window, PhysicalKey.ArrowDown);
            var accepted = Assert.IsType<ComboItem>(picker.SelectedItem);
            AssertRowUnchanged(row);
            Press(window, key);
            Assert.Equal(accepted.Value, row.ItemId); Assert.Equal(1, writes);
            Assert.False(row.IsFavorite); Assert.False(row.IsNew); Assert.False(row.IsNewShop); Assert.False(row.IsHeld);
            Press(window, PhysicalKey.Escape);
            Assert.Equal(accepted.Value, row.ItemId); Assert.Equal(1, writes);
            vm.SaveCommand.Execute(null);
            Assert.Equal(0, save.Items.GetItem(17).Count);
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void MouseAcceptance_UsesTheNativePopupAdapter()
    {
        var picker = new FilterableComboBox { ItemsSource = new[] { new ComboItem("First", 1), new ComboItem("Second", 2) }, SelectedValue = 1 };
        var window = Show(picker, 240, 70);
        try
        {
            Open(picker, window);
            var popup = picker.GetVisualDescendants().OfType<Popup>().Single();
            var item = popup.Child!.GetVisualDescendants().OfType<ListBoxItem>().Single(control => control.DataContext is ComboItem entry && entry.Value == 2);
            var top = TopLevel.GetTopLevel(item)!;
            var point = item.TranslatePoint(new Point(item.Bounds.Width / 2, item.Bounds.Height / 2), top)!.Value;
            top.MouseDown(point, MouseButton.Left); top.MouseUp(point, MouseButton.Left); Pump(window);
            Assert.Equal(2, picker.SelectedValue);
            Assert.Equal("Second", picker.Text);
            Assert.False(picker.IsDropDownOpen);
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void SourceAndExternalValueChangesCancelOldHighlightsAndNoMatchBlurRestores()
    {
        var source = new ObservableCollection<ComboItem> { new("One", 1), new("Two", 2) };
        var picker = new FilterableComboBox { ItemsSource = source, SelectedValue = 1 };
        var blur = new Button { Content = "Next" };
        var window = Show(new StackPanel { Children = { picker, blur } }, 240, 120);
        try
        {
            Open(picker, window); Press(window, PhysicalKey.ArrowDown);
            picker.SelectedValue = 2; Pump(window);
            Assert.Equal("Two", picker.Text);
            Press(window, PhysicalKey.Enter);
            Assert.Equal(2, picker.SelectedValue);
            source[1] = new ComboItem("Deux", 2); Pump(window);
            Assert.Equal("Deux", picker.Text);
            Assert.True(blur.Focus()); Pump(window);
            Open(picker, window);
            window.KeyTextInput("missing"); Pump(window);
            Assert.True(blur.Focus()); Pump(window);
            Assert.Equal(2, picker.SelectedValue); Assert.Equal("Deux", picker.Text);
        }
        finally { window.Close(); }
    }

    [AvaloniaTheory]
    [InlineData(PhysicalKey.Enter)] [InlineData(PhysicalKey.Tab)] [InlineData(PhysicalKey.Escape)]
    public void NoMatchAndReopenedPopup_DoNotPublishAnOldHighlight(PhysicalKey key)
    {
        var picker = new FilterableComboBox { ItemsSource = new[] { new ComboItem("One", 1), new ComboItem("Two", 2) }, SelectedValue = 2 };
        var blur = new Button { Content = "Next" };
        var window = Show(new StackPanel { Children = { picker, blur } }, 240, 120);
        try
        {
            Open(picker, window); Press(window, PhysicalKey.ArrowDown); Press(window, PhysicalKey.Escape);
            Assert.Equal(2, picker.SelectedValue);
            // The TextBox keeps focus after Escape. A new search must still be a draft.
            window.KeyTextInput("missing"); Pump(window); Press(window, key);
            Assert.True(blur.Focus()); Pump(window);
            Assert.Equal(2, picker.SelectedValue); Assert.Equal("Two", picker.Text);
            Open(picker, window); Press(window, PhysicalKey.Escape); Press(window, PhysicalKey.ArrowDown);
            Assert.True(picker.IsDropDownOpen);
            Press(window, PhysicalKey.ArrowDown);
            Assert.Equal(1, Assert.IsType<ComboItem>(picker.SelectedItem).Value);
            Assert.Equal(2, picker.SelectedValue);
            Press(window, PhysicalKey.Enter);
            Assert.Equal(1, picker.SelectedValue);
        }
        finally { window.Close(); }
    }

    private static SAV9ZA LoadZa() => Assert.IsType<SAV9ZA>(SaveFileFixture.LoadSave(Path.Combine(SaveFileFixture.FindSaveFilesPath()!, "gen9a_legendsza.main")));
    private static void SetFlags(SAV9ZA save)
    {
        var source = save.Items.GetItem(17); source.Count = 7; source.Flags = 0xA000001B; source.Padding = 0x12345678;
        source.Write(InventoryPouch9a.GetItemSpan(save.Items.Data, 17));
    }
    private static void AssertRowUnchanged(InventoryItemViewModel row)
    {
        Assert.Equal(17, row.ItemId); Assert.Equal(7, row.Count);
        Assert.True(row.IsFavorite); Assert.True(row.IsNew); Assert.True(row.IsNewShop); Assert.True(row.IsHeld);
    }
    private static Window Show(Control content, int width, int height)
    {
        var window = new Window { Content = content, Width = width, Height = height };
        window.Show(); Pump(window); return window;
    }
    private static void Open(FilterableComboBox picker, Window window)
    {
        picker.MinimumPopulateDelay = TimeSpan.Zero;
        // AutoCompleteBox forwards focus to its TextBox; Avalonia 12 Focus() then returns false.
        picker.Focus(); Pump(window);
        Assert.True(picker.IsKeyboardFocusWithin);
        Assert.True(picker.IsDropDownOpen);
    }
    private static void Press(Window window, PhysicalKey key)
    {
        window.KeyPressQwerty(key, RawInputModifiers.None); window.KeyReleaseQwerty(key, RawInputModifiers.None); Pump(window);
    }
    private static void Pump(Window window)
    {
        Dispatcher.UIThread.RunJobs(); window.UpdateLayout(); AvaloniaHeadlessPlatform.ForceRenderTimerTick(); Dispatcher.UIThread.RunJobs();
    }
}
