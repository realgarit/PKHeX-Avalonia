using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Avalonia.VisualTree;
using PKHeX.Avalonia.Views;
using PKHeX.Avalonia.Tests.Fixtures;
using PKHeX.Core;
using PKHeX.Presentation.ViewModels;
using Moq;

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

    [Fact]
    public void ChangingGenderForAFormSpecificSpecies_UpdatesItsForm()
    {
        var sav = new SAV6XY();
        var pkm = new PK6
        {
            Species = (ushort)Species.Meowstic,
            Form = 0,
            Gender = 0,
            Language = (int)LanguageID.English,
        };
        var (vm, _, _) = TestHelpers.CreateTestViewModel(pkm, sav);

        vm.Gender = 1;

        Assert.Equal(1, vm.Form);
        Assert.Equal(1, vm.PreparePKM().Form);
    }

    [Fact]
    public void FormSpecificSpeciesCannotBeSetToGenderless()
    {
        var sav = new SAV6XY();
        var pkm = new PK6
        {
            Species = (ushort)Species.Meowstic,
            Form = 1,
            Gender = 1,
            Language = (int)LanguageID.English,
        };
        var (vm, _, _) = TestHelpers.CreateTestViewModel(pkm, sav);

        Assert.DoesNotContain(vm.PokemonGenderList, item => item.Value == (int)Gender.Genderless);

        vm.Gender = (int)Gender.Genderless;

        Assert.Equal(1, vm.Gender);
        Assert.Equal(1, vm.Form);
        Assert.Equal(1, vm.PreparePKM().Gender);
    }

    [Fact]
    public async Task SuggestingCurrentMoves_RequiresConfirmationBeforeReplacingMoves()
    {
        var sav = new SAV6XY();
        var pkm = new PK6
        {
            Species = 25,
            Language = (int)LanguageID.English,
            Move1 = 33,
            Move2 = 45,
            Move1_PP = 5,
            Move2_PP = 10,
        };
        var (vm, _, dialog) = TestHelpers.CreateTestViewModel(pkm, sav);
        dialog.Setup(d => d.ShowConfirmationAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync(false);

        await vm.SuggestCurrentMovesCommand.ExecuteAsync(null);

        Assert.Equal(33, vm.Move1);
        Assert.Equal(45, vm.Move2);
        dialog.Verify(d => d.ShowConfirmationAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()), Times.Once);
    }

    [Fact]
    public void LegendsZaChangingAnIvRefreshesLegality()
    {
        var saveDirectory = SaveFileFixture.FindSaveFilesPath();
        Assert.NotNull(saveDirectory);

        var save = SaveFileFixture.LoadSave(Path.Combine(saveDirectory, "gen9a_legendsza.main")) as SAV9ZA;
        Assert.NotNull(save);

        PA9? sample = null;
        for (var box = 0; box < save.BoxCount && sample is null; box++)
        {
            for (var slot = 0; slot < save.BoxSlotCount; slot++)
            {
                if (save.GetBoxSlotAtIndex(box, slot) is PA9 pa9 && pa9.Species != 0 && new LegalityAnalysis(pa9, save.Personal).Valid)
                {
                    sample = pa9;
                    break;
                }
            }
        }

        Assert.NotNull(sample);

        var (vm, _, _) = TestHelpers.CreateTestViewModel(sample, save);
        Assert.True(vm.IsLegal);

        vm.IvHP = vm.IvHP == 31 ? 30 : vm.IvHP + 1;

        Assert.False(vm.IsLegal);
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
    public void LoadingPokemonWithNonDefaultForm_UpdatesTheFormComboSelection()
    {
        var sav = new SAV6XY();
        var (vm, _, _) = TestHelpers.CreateTestViewModel(new PK6
        {
            Species = 1,
            Language = (int)LanguageID.English,
        }, sav);
        var view = new PokemonEditor { DataContext = vm };
        var window = new Window { Content = view, Width = 700, Height = 600 };
        window.Show();

        Dispatcher.UIThread.RunJobs();
        window.UpdateLayout();

        vm.LoadPKM(new PK6
        {
            Species = 669,
            Form = 3,
            Language = (int)LanguageID.English,
        });

        Dispatcher.UIThread.RunJobs();
        window.UpdateLayout();
        Dispatcher.UIThread.RunJobs();

        var formCombo = view.GetVisualDescendants().OfType<ComboBox>()
            .Single(combo => ReferenceEquals(combo.ItemsSource, vm.FormList));
        var selectedForm = Assert.IsType<ComboItem>(formCombo.SelectedItem);

        Assert.Equal(3, vm.Form);
        Assert.Equal(3, selectedForm.Value);
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

    [Fact]
    public void ChangingAMoveWithADifferentBasePp_RecalculatesThatSlotsCurrentPp()
    {
        var sav = new SAV6XY();
        const int oldMove = 33;
        const int newMove = 76;
        var pkm = new PK6
        {
            Species = 25,
            Language = (int)LanguageID.English,
            Move1 = oldMove,
            Move1_PP = 5,
            Move1_PPUps = 0,
        };
        var oldMax = pkm.GetMovePP((ushort)oldMove, 0);
        var newMax = pkm.GetMovePP((ushort)newMove, 0);
        Assert.NotEqual(oldMax, newMax); // sanity: the two moves must actually differ in base PP
        var (vm, _, _) = TestHelpers.CreateTestViewModel(pkm, sav);

        vm.Move1 = newMove;

        Assert.Equal(newMax, vm.Pp1);
    }

    [Fact]
    public void ChangingPpUps_RecalculatesThatSlotsCurrentPp()
    {
        var sav = new SAV6XY();
        const int move = 45;
        var pkm = new PK6
        {
            Species = 25,
            Language = (int)LanguageID.English,
            Move2 = move,
            Move2_PP = 10,
            Move2_PPUps = 0,
        };
        var expected = pkm.GetMovePP((ushort)move, 3);
        Assert.NotEqual(10, expected); // sanity: raising PP Ups must actually change the max
        var (vm, _, _) = TestHelpers.CreateTestViewModel(pkm, sav);

        vm.PpUps2 = 3;

        Assert.Equal(expected, vm.Pp2);
    }

    [Fact]
    public void LoadingAPokemonWithDamagedPp_KeepsTheReducedPpUntilTheUserChangesSomething()
    {
        var sav = new SAV6XY();
        const int move = 33;
        var pkm = new PK6
        {
            Species = 25,
            Language = (int)LanguageID.English,
            Move1 = move,
            Move1_PPUps = 0,
        };
        var fullPp = pkm.GetMovePP((ushort)move, 0);
        Assert.True(fullPp > 1); // sanity: there must be room to simulate a used PP below the max
        pkm.Move1_PP = fullPp - 1;

        var (vm, _, _) = TestHelpers.CreateTestViewModel(pkm, sav);

        Assert.Equal(fullPp - 1, vm.Pp1);
    }

    [Fact]
    public void ClearingAMoveToNone_ZeroesThatSlotsCurrentPp()
    {
        var sav = new SAV6XY();
        var pkm = new PK6
        {
            Species = 25,
            Language = (int)LanguageID.English,
            Move3 = 45,
            Move3_PP = 10,
            Move3_PPUps = 2,
        };
        var (vm, _, _) = TestHelpers.CreateTestViewModel(pkm, sav);

        vm.Move3 = 0;

        Assert.Equal(0, vm.Pp3);
    }

    [Fact]
    public void ChangingOneMoveSlot_DoesNotAffectTheOtherSlotsCurrentPp()
    {
        var sav = new SAV6XY();
        var pkm = new PK6
        {
            Species = 25,
            Language = (int)LanguageID.English,
            Move1 = 33, Move1_PP = 5, Move1_PPUps = 0,
            Move2 = 45, Move2_PP = 10, Move2_PPUps = 1,
            Move3 = 76, Move3_PP = 15, Move3_PPUps = 0,
            Move4 = 92, Move4_PP = 8, Move4_PPUps = 2,
        };
        var (vm, _, _) = TestHelpers.CreateTestViewModel(pkm, sav);

        vm.Move1 = 10;

        Assert.Equal(pkm.GetMovePP(10, 0), vm.Pp1);
        Assert.Equal(10, vm.Pp2);
        Assert.Equal(15, vm.Pp3);
        Assert.Equal(8, vm.Pp4);
    }
}
