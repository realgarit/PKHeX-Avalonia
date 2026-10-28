using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;
using PKHeX.Avalonia.Controls;
using PKHeX.Avalonia.Views;
using PKHeX.Core;
using PKHeX.Presentation.Localization;
using PKHeX.Presentation.ViewModels;

namespace PKHeX.Avalonia.Tests;

public sealed class LegacyAbilityPickerTests
{
    [Fact]
    public void Gen3SingleAbility_OffersOnlyRepresentableSlotAndRejectsSecondSlot()
    {
        var pk = CreateEntity(3, Species.Bulbasaur, 0);
        var vm = CreateEditor(pk);
        Assert.Equal(2, vm.AbilityList.Count);
        Assert.Equal(vm.AbilityList[0].Value, vm.AbilityList[1].Value);
        Assert.Equal(0, Assert.Single(vm.AbilityChoices).Value);
        var before = vm.PreparePKM().Data.ToArray();

        vm.SelectedAbilityIndex = 1;

        Assert.Equal(0, vm.SelectedAbilityIndex);
        Assert.Equal(before, vm.PreparePKM().Data.ToArray());
        Assert.Equal(pk.PID, vm.PreparePKM().PID);
    }

    [Theory]
    [InlineData(Species.Bulbasaur)]
    [InlineData(Species.Granbull)]
    [InlineData(Species.Vibrava)]
    [InlineData(Species.Flygon)]
    public void LoadedGen3SecondBit_NoOpPreservesPidAndAllEntityBytes(Species species)
    {
        var pk = CreateEntity(3, species, 1);
        Assert.Equal(2, pk.AbilityNumber);
        var before = pk.Data.ToArray();
        var vm = CreateEditor(pk);

        Assert.Equal(1, vm.SelectedAbilityIndex);
        Assert.Contains(vm.AbilityChoices, entry => entry.Value == 1);
        Assert.Equal(before, vm.PreparePKM().Data.ToArray());
        Assert.Equal(before, vm.PreparePKM().Data.ToArray());
        Assert.Equal(before, pk.Data.ToArray());

        var reopened = RoundTrip(vm.PreparePKM());
        Assert.Equal(2, reopened.AbilityNumber);
        Assert.Equal(pk.PID, reopened.PID);
        var reloaded = CreateEditor(reopened);
        Assert.Equal(1, reloaded.SelectedAbilityIndex);
        Assert.Equal(reopened.Data.ToArray(), reloaded.PreparePKM().Data.ToArray());
    }

    [Theory]
    [InlineData(Species.Granbull, 0)] [InlineData(Species.Granbull, 1)]
    [InlineData(Species.Vibrava, 0)] [InlineData(Species.Vibrava, 1)]
    [InlineData(Species.Flygon, 0)] [InlineData(Species.Flygon, 1)]
    public void Gen3ExceptionSpecies_MainlineSlotChangeKeepsStoredBitAndPidParityAligned(Species species, int slot)
    {
        var pk = CreateEntity(3, species, 1 - slot);
        var original = pk.Data.ToArray();
        var vm = CreateEditor(pk);
        Assert.Equal(new[] { 0, 1 }, vm.AbilityChoices.Select(entry => entry.Value));
        Assert.Equal(vm.AbilityList[0].Value, vm.AbilityList[1].Value);

        vm.SelectedAbilityIndex = slot;

        var prepared = vm.PreparePKM();
        Assert.Equal(slot, vm.SelectedAbilityIndex);
        Assert.Equal(1 << slot, prepared.AbilityNumber);
        Assert.Equal(pk.Ability, prepared.Ability);
        Assert.NotEqual(pk.PID, prepared.PID);
        Assert.Equal(slot, prepared.PIDAbility);
        Assert.Equal(prepared.Data.ToArray(), vm.PreparePKM().Data.ToArray());
        Assert.Equal(original, pk.Data.ToArray());
        var reopened = RoundTrip(prepared);
        Assert.Equal(1 << slot, reopened.AbilityNumber);
        Assert.Equal(prepared.PID, reopened.PID);
        Assert.Equal(slot, CreateEditor(reopened).SelectedAbilityIndex);
    }

    [Theory]
    [InlineData(Species.Granbull, 0)] [InlineData(Species.Granbull, 1)]
    [InlineData(Species.Vibrava, 0)] [InlineData(Species.Vibrava, 1)]
    [InlineData(Species.Flygon, 0)] [InlineData(Species.Flygon, 1)]
    public void Gen3ExceptionSpecies_CxdOriginAllowsEitherStoredBitWithoutPidReroll(Species species, int slot)
    {
        var pk = CreateEntity(3, species, 1 - slot, origin: GameVersion.CXD);
        var vm = CreateEditor(pk);
        Assert.Equal(new[] { 0, 1 }, vm.AbilityChoices.Select(entry => entry.Value));

        vm.SelectedAbilityIndex = slot;

        var prepared = vm.PreparePKM();
        Assert.Equal(1 << slot, prepared.AbilityNumber);
        Assert.Equal(pk.Ability, prepared.Ability);
        Assert.Equal(pk.PID, prepared.PID);
        Assert.Equal(prepared.Data.ToArray(), vm.PreparePKM().Data.ToArray());
        var reopened = RoundTrip(prepared);
        Assert.Equal(GameVersion.CXD, reopened.Version);
        Assert.Equal(1 << slot, reopened.AbilityNumber);
        Assert.Equal(pk.PID, reopened.PID);
        Assert.Equal(slot, CreateEditor(reopened).SelectedAbilityIndex);
    }

    [Theory]
    [InlineData(3, 0, false)] [InlineData(3, 1, false)]
    [InlineData(4, 0, false)] [InlineData(4, 1, false)]
    [InlineData(5, 0, false)] [InlineData(5, 1, false)]
    [InlineData(3, 0, true)] [InlineData(3, 1, true)]
    [InlineData(4, 0, true)] [InlineData(4, 1, true)]
    [InlineData(5, 0, true)] [InlineData(5, 1, true)]
    public void ExplicitLegacyNormalSlotChange_PreservesShinyNatureAndGenderAndDoesNotRerollAgain(int format, int slot, bool shiny)
    {
        var species = format == 3 ? Species.Ralts : Species.Eevee;
        var pk = CreateEntity(format, species, 1 - slot, shiny);
        Assert.NotEqual(pk.PersonalInfo.GetAbilityAtIndex(0), pk.PersonalInfo.GetAbilityAtIndex(1));
        var original = pk.Data.ToArray();
        var vm = CreateEditor(pk);
        Assert.Equal(pk.PID, vm.PreparePKM().PID);
        Assert.Equal(pk.PID.ToString("X8"), vm.Pid);

        vm.SelectedAbilityIndex = slot;

        var prepared = vm.PreparePKM();
        Assert.NotEqual(pk.PID, prepared.PID);
        Assert.Equal(slot, prepared.PIDAbility);
        Assert.Equal(1 << slot, prepared.AbilityNumber);
        Assert.Equal(prepared.PersonalInfo.GetAbilityAtIndex(slot), prepared.Ability);
        Assert.Equal(pk.Nature, prepared.Nature);
        Assert.Equal(pk.Gender, prepared.Gender);
        Assert.Equal(shiny, prepared.IsShiny);
        Assert.Equal(slot, vm.SelectedAbilityIndex);
        Assert.Equal(prepared.PID.ToString("X8"), vm.Pid);
        Assert.Equal(prepared.EncryptionConstant.ToString("X8"), vm.EncryptionConstant);
        Assert.Equal(original, pk.Data.ToArray());

        var stable = prepared.Data.ToArray();
        Assert.Equal(stable, vm.PreparePKM().Data.ToArray());
        Assert.Equal(stable, vm.PreparePKM().Data.ToArray());
        var reopened = RoundTrip(prepared);
        Assert.Equal(prepared.PID, reopened.PID);
        Assert.Equal(1 << slot, reopened.AbilityNumber);
        Assert.Equal(shiny, reopened.IsShiny);
        var reloaded = CreateEditor(reopened);
        Assert.Equal(slot, reloaded.SelectedAbilityIndex);
        Assert.Equal(prepared.PID, reloaded.PreparePKM().PID);
    }

    [Theory]
    [InlineData(0, false)] [InlineData(1, false)]
    [InlineData(0, true)] [InlineData(1, true)]
    public void Gen5HiddenAbilityTransitions_PreservePidWhenEnteringHiddenAndRestoreNormalSlot(int normalSlot, bool shiny)
    {
        var pk = Assert.IsType<PK5>(CreateEntity(5, Species.Eevee, normalSlot, shiny));
        var vm = CreateEditor(pk);

        vm.SelectedAbilityIndex = 2;

        var hidden = Assert.IsType<PK5>(vm.PreparePKM());
        Assert.True(hidden.HiddenAbility);
        Assert.Equal(4, hidden.AbilityNumber);
        Assert.Equal(hidden.PersonalInfo.GetAbilityAtIndex(2), hidden.Ability);
        Assert.Equal(pk.PID, hidden.PID);
        Assert.Equal(shiny, hidden.IsShiny);
        Assert.Equal(hidden.Data.ToArray(), vm.PreparePKM().Data.ToArray());
        var hiddenReload = CreateEditor(RoundTrip(hidden));
        Assert.Equal(2, hiddenReload.SelectedAbilityIndex);
        Assert.Equal(hidden.PID, hiddenReload.PreparePKM().PID);

        // Returning to the original normal slot needs no personality reroll.
        vm.SelectedAbilityIndex = normalSlot;
        var normal = Assert.IsType<PK5>(vm.PreparePKM());
        Assert.False(normal.HiddenAbility);
        Assert.Equal(pk.PID, normal.PID);
        Assert.Equal(1 << normalSlot, normal.AbilityNumber);

        // A hidden Pokemon may also switch to the other normal slot.
        vm.SelectedAbilityIndex = 2;
        vm.SelectedAbilityIndex = 1 - normalSlot;
        var other = Assert.IsType<PK5>(vm.PreparePKM());
        Assert.False(other.HiddenAbility);
        Assert.Equal(1 - normalSlot, other.PIDAbility);
        Assert.Equal(1 << (1 - normalSlot), other.AbilityNumber);
        Assert.Equal(other.PersonalInfo.GetAbilityAtIndex(1 - normalSlot), other.Ability);
        Assert.Equal(shiny, other.IsShiny);
        Assert.Equal(other.PID.ToString("X8"), vm.Pid);
        Assert.Equal(other.Data.ToArray(), vm.PreparePKM().Data.ToArray());
    }

    [Theory]
    [InlineData(3)] [InlineData(4)] [InlineData(5)]
    public void InvalidIndicesAndRepeatedCurrentSelection_DoNotMutateEntity(int format)
    {
        var pk = CreateEntity(format, format == 3 ? Species.Ralts : Species.Eevee, 1);
        var vm = CreateEditor(pk);
        var before = vm.PreparePKM().Data.ToArray();
        foreach (var invalid in new[] { -1, vm.AbilityList.Count, int.MaxValue })
        {
            vm.SelectedAbilityIndex = invalid;
            Assert.Equal(1, vm.SelectedAbilityIndex);
            Assert.Equal(before, vm.PreparePKM().Data.ToArray());
        }
        vm.SelectedAbilityIndex = 1;
        vm.SelectedAbilityIndex = 1;
        Assert.Equal(before, vm.PreparePKM().Data.ToArray());
        Assert.Equal(pk.PID, vm.PreparePKM().PID);
    }

    [Theory]
    [InlineData(4, 2, false)] [InlineData(4, 3, false)]
    [InlineData(5, 2, false)] [InlineData(5, 3, false)]
    [InlineData(4, 2, true)] [InlineData(4, 3, true)]
    [InlineData(5, 2, true)] [InlineData(5, 3, true)]
    public void InvalidDualGenderConstraint_RejectsPidDependentSlotChangeWithoutMutation(int format, int gender, bool rawAbilityApi)
    {
        var pk = CreateEntity(format, Species.Eevee, 0);
        pk.Gender = (byte)gender; pk.RefreshChecksum();
        var vm = CreateEditor(pk);
        var before = vm.PreparePKM().Data.ToArray();

        // Core's PID search cannot satisfy genderless/reserved gender for Eevee.
        // Both the new index contract and the existing raw-ID API must reject
        // this request before entering that search (CI also has a hang gate).
        if (rawAbilityApi) vm.Ability = vm.AbilityList[1].Value;
        else vm.SelectedAbilityIndex = 1;

        Assert.Equal(0, vm.SelectedAbilityIndex);
        Assert.Equal(pk.Ability, vm.Ability);
        Assert.Equal(before, vm.PreparePKM().Data.ToArray());
        Assert.Equal(pk.PID.ToString("X8"), vm.Pid);
    }

    [Theory]
    [InlineData(GameVersion.E, 25, false)] [InlineData(GameVersion.Pt, 255, false)]
    [InlineData(GameVersion.E, 25, true)] [InlineData(GameVersion.Pt, 255, true)]
    public void InvalidTransferredNatureConstraint_RejectsPidDependentSlotChangeWithoutMutation(GameVersion origin, int nature, bool rawAbilityApi)
    {
        var pk = CreateEntity(5, Species.Eevee, 0, origin: origin);
        pk.Nature = (Nature)nature; pk.RefreshChecksum();
        var vm = CreateEditor(pk);
        var before = vm.PreparePKM().Data.ToArray();

        // Gen 3/4-origin PID searches require PID % 25 to match this stored
        // PK5 nature; values outside 0..24 have no solution.
        if (rawAbilityApi) vm.Ability = vm.AbilityList[1].Value;
        else vm.SelectedAbilityIndex = 1;

        Assert.Equal(0, vm.SelectedAbilityIndex);
        Assert.Equal(pk.Ability, vm.Ability);
        Assert.Equal(before, vm.PreparePKM().Data.ToArray());
        Assert.Equal(pk.PID.ToString("X8"), vm.Pid);
    }

    [AvaloniaFact]
    public void ActualGen3Picker_CannotAcceptUnrepresentableSecondSlot()
    {
        var pk = CreateEntity(3, Species.Bulbasaur, 0);
        var vm = CreateEditor(pk);
        var view = new PokemonEditor { DataContext = vm };
        var blur = new Button { Content = "Next" };
        var panel = new DockPanel();
        DockPanel.SetDock(blur, Dock.Bottom);
        panel.Children.Add(blur); panel.Children.Add(view);
        var window = Show(panel);
        try
        {
            var picker = AbilityPicker(view);
            Assert.Equal(0, Assert.Single(picker.ItemsSource!.Cast<ComboItem>()).Value);
            Open(picker, window);
            Press(window, PhysicalKey.ArrowDown);
            Assert.Equal(0, Assert.IsType<ComboItem>(picker.SelectedItem).Value);
            Press(window, PhysicalKey.Enter);
            Assert.Equal(0, vm.SelectedAbilityIndex);

            // The raw compatibility list still contains the unavailable label. Typing it
            // into the real picker must not make its unrepresentable slot selectable.
            Assert.True(blur.Focus()); Pump(window);
            Open(picker, window);
            window.KeyTextInput(vm.AbilityList[1].Text); Pump(window);
            Assert.Null(picker.SelectedItem);
            Press(window, PhysicalKey.Enter);
            Assert.Equal(0, vm.SelectedAbilityIndex);
            Assert.Equal(pk.PID, vm.PreparePKM().PID);
            Assert.Equal(1, vm.PreparePKM().AbilityNumber);
        }
        finally { window.Close(); }
    }

    [AvaloniaTheory]
    [InlineData(0)] [InlineData(1)]
    public void ActualGen4DuplicatePicker_AcceptsSlotIdentityAndPersistsThroughReload(int slot)
    {
        var pk = CreateEntity(4, Species.Mew, 1 - slot);
        var vm = CreateEditor(pk);
        Assert.Equal(2, vm.AbilityList.Count);
        Assert.Single(vm.AbilityList.Select(entry => entry.Value).Distinct());
        var view = new PokemonEditor { DataContext = vm };
        var window = Show(view);
        try
        {
            var picker = AbilityPicker(view);
            Open(picker, window);
            for (var i = 0; i <= slot; i++) Press(window, PhysicalKey.ArrowDown);
            Assert.Equal(slot, Assert.IsType<ComboItem>(picker.SelectedItem).Value);
            Assert.Equal(1 - slot, vm.SelectedAbilityIndex);
            Assert.Equal(pk.PID, vm.PreparePKM().PID);
            Press(window, PhysicalKey.Enter);

            var prepared = vm.PreparePKM();
            Assert.Equal(slot, vm.SelectedAbilityIndex);
            Assert.Equal(1 << slot, prepared.AbilityNumber);
            Assert.Equal(pk.Ability, prepared.Ability);
            Assert.Equal(prepared.PID.ToString("X8"), vm.Pid);
            Assert.Equal(vm.AbilityChoices.Single(entry => entry.Value == slot).Text, picker.Text);
            Assert.Equal(prepared.Data.ToArray(), vm.PreparePKM().Data.ToArray());

            var reopened = RoundTrip(prepared);
            Assert.True(vm.LoadPKM(reopened)); Pump(window);
            Assert.Equal(slot, vm.SelectedAbilityIndex);
            Assert.Equal(1 << slot, vm.PreparePKM().AbilityNumber);
            Assert.Equal(prepared.PID, vm.PreparePKM().PID);
            Assert.Equal(vm.AbilityChoices.Single(entry => entry.Value == slot).Text, picker.Text);
        }
        finally { window.Close(); }
    }

    private static PKM CreateEntity(int format, Species species, int slot, bool shiny = false, GameVersion? origin = null)
    {
        PKM pk = format switch
        {
            3 => new PK3 { Version = GameVersion.E },
            4 => new PK4 { Version = GameVersion.Pt },
            5 => new PK5 { Version = GameVersion.B2 },
            _ => throw new ArgumentOutOfRangeException(nameof(format)),
        };
        if (origin is { } version) pk.Version = version;
        pk.Species = (ushort)species;
        pk.PID = slot == 0 ? 0x12340000u : 0x12350001u;
        pk.Gender = EntityGender.GetFromPID(pk.Species, pk.PID);
        if (format == 5) pk.Nature = Nature.Adamant;
        pk.Language = (int)LanguageID.English;
        pk.TID16 = 0x1111; pk.SID16 = 0x2222;
        pk.OriginalTrainerName = "TEST";
        pk.CurrentLevel = 50;
        pk.MetLevel = 20;
        pk.Ball = (byte)Ball.Poke;
        pk.OriginalTrainerFriendship = pk.PersonalInfo.BaseFriendship;
        pk.ClearNickname();
        pk.RefreshAbility(slot);
        // Core deliberately leaves these Gen 3 bits alone for some species; a
        // single-ability Pokemon can also arrive with its existing second bit set.
        if (pk is G3PKM) pk.AbilityNumber = 1 << slot;
        if (shiny) pk.SetShinySID(Shiny.AlwaysStar);
        Assert.Equal(shiny, pk.IsShiny);
        pk.ResetPartyStats(); pk.RefreshChecksum();
        return pk;
    }

    private static PokemonEditorViewModel CreateEditor(PKM pk)
    {
        SaveFile save = pk.Format switch
        {
            3 => new SAV3E(),
            4 => new SAV4Pt(),
            5 => new SAV5B2W2(),
            _ => throw new ArgumentOutOfRangeException(nameof(pk)),
        };
        GameInfo.FilteredSources = new FilteredGameDataSource(save, GameInfo.Sources);
        return TestHelpers.CreateTestViewModel(pk, save).VM;
    }

    private static PKM RoundTrip(PKM pk)
    {
        var bytes = new byte[pk.SIZE_PARTY];
        pk.WriteEncryptedDataParty(bytes);
        return pk.Format switch
        {
            3 => new PK3(bytes),
            4 => new PK4(bytes),
            5 => new PK5(bytes),
            _ => throw new ArgumentOutOfRangeException(nameof(pk)),
        };
    }

    private static FilterableComboBox AbilityPicker(PokemonEditor view) => view.GetVisualDescendants().OfType<FilterableComboBox>()
        .Single(control => AutomationProperties.GetName(control) == LocalizedStrings.Instance["PokemonEditor_Ability"]);

    private static Window Show(Control content)
    {
        var window = new Window { Content = content, Width = 900, Height = 850 };
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
