using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Avalonia.VisualTree;
using PKHeX.Avalonia.Views;
using PKHeX.Core;
using PKHeX.Presentation.ViewModels;

namespace PKHeX.Avalonia.Tests;

public class PokemonEditorReportsTests
{
    [Fact]
    public void ChangingSpecies_UpdatesAnUnnicknamedSpeciesName()
    {
        var sav = new SAV9SV();
        var pkm = new PK9
        {
            Species = 64,
            Language = (int)LanguageID.English,
        };
        pkm.ClearNickname();
        var (vm, _, _) = TestHelpers.CreateTestViewModel(pkm, sav);

        vm.Species = 65;

        Assert.Equal(SpeciesName.GetSpeciesNameGeneration(65, vm.Language, 9), vm.Nickname);
        Assert.False(vm.IsNicknamed);
    }

    [Fact]
    public void ChangingSpecies_PreservesACustomNickname()
    {
        var sav = new SAV9SV();
        var pkm = new PK9
        {
            Species = 64,
            Language = (int)LanguageID.English,
        };
        pkm.SetNickname("CustomAbra");
        var (vm, _, _) = TestHelpers.CreateTestViewModel(pkm, sav);

        vm.Species = 65;

        Assert.Equal("CustomAbra", vm.Nickname);
        Assert.True(vm.IsNicknamed);
    }

    [Fact]
    public void EditingNickname_SetsTheNicknameFlag()
    {
        var sav = new SAV9SV();
        var pkm = new PK9
        {
            Species = 64,
            Language = (int)LanguageID.English,
        };
        pkm.ClearNickname();
        var (vm, _, _) = TestHelpers.CreateTestViewModel(pkm, sav);

        vm.Nickname = "AbraFriend";

        Assert.True(vm.IsNicknamed);
        Assert.True(vm.PreparePKM().IsNicknamed);
    }

    [Fact]
    public void ClearingNickname_RestoresTheSpeciesNameAndClearsTheFlag()
    {
        var sav = new SAV9SV();
        var pkm = new PK9
        {
            Species = 65,
            Language = (int)LanguageID.English,
        };
        pkm.SetNickname("CustomName");
        var (vm, _, _) = TestHelpers.CreateTestViewModel(pkm, sav);

        vm.Nickname = string.Empty;

        Assert.Equal(SpeciesName.GetSpeciesNameGeneration(65, vm.Language, 9), vm.Nickname);
        Assert.False(vm.IsNicknamed);
    }

    [Fact]
    public void HandlingTrainerFields_RoundTripAndMarkThePokemonAsTraded()
    {
        var sav = new SAV6XY();
        var pkm = new PK6
        {
            Species = 64,
            Language = (int)LanguageID.English,
            OriginalTrainerName = "Original OT",
            OriginalTrainerFriendship = 50,
        };
        var (vm, _, _) = TestHelpers.CreateTestViewModel(pkm, sav);

        Assert.True(vm.TargetPKM.Format >= 6);
        vm.HandlingTrainerName = "TradeMate";
        vm.HandlingTrainerGender = 1;
        vm.HandlingTrainerFriendship = 80;
        vm.OriginalTrainerFriendship = 50;
        vm.CurrentHandler = 1;

        var result = vm.PreparePKM();

        Assert.Equal("TradeMate", result.HandlingTrainerName);
        Assert.Equal(1, result.HandlingTrainerGender);
        Assert.Equal(80, result.HandlingTrainerFriendship);
        Assert.Equal(1, result.CurrentHandler);
        Assert.False(result.IsUntraded);
    }

    [Fact]
    public void Flabebe_ExposesItsFiveColorForms()
    {
        var sav = new SAV6XY();
        var pkm = new PK6
        {
            Species = 669,
            Language = (int)LanguageID.English,
        };
        var (vm, _, _) = TestHelpers.CreateTestViewModel(pkm, sav);

        Assert.True(vm.HasForms);
        Assert.Equal(5, vm.FormList.Count);

        vm.Form = 4;
        Assert.Equal(4, vm.PreparePKM().Form);
    }

    [AvaloniaFact]
    public void FlabebeFormCombo_IsVisibleInTheMainEditor()
    {
        var sav = new SAV6XY();
        var pkm = new PK6
        {
            Species = 669,
            Language = (int)LanguageID.English,
        };
        var (vm, _, _) = TestHelpers.CreateTestViewModel(pkm, sav);
        var view = new PokemonEditor { DataContext = vm };
        var window = new Window { Content = view, Width = 700, Height = 600 };
        window.Show();

        Dispatcher.UIThread.RunJobs();
        window.UpdateLayout();
        Dispatcher.UIThread.RunJobs();

        var formCombo = view.GetVisualDescendants().OfType<ComboBox>()
            .Single(combo => ReferenceEquals(combo.ItemsSource, vm.FormList));

        Assert.True(formCombo.IsEffectivelyVisible);
        Assert.Equal(5, formCombo.Items.Count);
    }

    [AvaloniaFact]
    public void PokerusFieldsHaveSeparateLayoutBounds()
    {
        var sav = new SAV9SV();
        var (vm, _, _) = TestHelpers.CreateTestViewModel(new PK9 { Species = 906 }, sav);
        var view = new PokemonEditor { DataContext = vm };
        var window = new Window { Content = view, Width = 760, Height = 800 };
        window.Show();

        Dispatcher.UIThread.RunJobs();
        var tabs = view.GetVisualDescendants().OfType<TabControl>().Single();
        tabs.SelectedIndex = 4;
        Dispatcher.UIThread.RunJobs();
        window.UpdateLayout();
        Dispatcher.UIThread.RunJobs();

        var days = view.GetVisualDescendants().OfType<NumericUpDown>()
            .Single(control => global::Avalonia.Automation.AutomationProperties.GetName(control) == "Pokérus Days Remaining");
        var strain = view.GetVisualDescendants().OfType<NumericUpDown>()
            .Single(control => global::Avalonia.Automation.AutomationProperties.GetName(control) == "Pokérus Strain");

        Assert.True(days.Bounds.Width > 0);
        Assert.True(strain.Bounds.Width > 0);
        Assert.True(days.Bounds.Right < strain.Bounds.Left, $"Pokérus fields overlap: {days.Bounds} / {strain.Bounds}");
    }
}
