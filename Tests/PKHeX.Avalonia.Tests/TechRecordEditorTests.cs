using PKHeX.Core;
using PKHeX.Presentation.ViewModels;
using Xunit;

namespace PKHeX.Avalonia.Tests;

public sealed class TechRecordEditorTests
{
    private static PK9 CreatePokemon() => new()
    {
        Species = (ushort)Species.Pikachu,
        CurrentLevel = 50,
        EncryptionConstant = 0x12345678,
    };

    [Fact]
    public void CancelDoesNotMutateTheOwningPokemon()
    {
        var pokemon = CreatePokemon();
        var records = (ITechRecord)pokemon;
        var vm = new TechRecordEditorViewModel(records, pokemon);
        var item = vm.Records[0];
        var original = records.GetMoveRecordFlag(item.Index);

        item.IsActive = !original;
        vm.CloseCommand.Execute(null);

        Assert.Equal(original, records.GetMoveRecordFlag(item.Index));
    }

    [Fact]
    public void SaveCommitsStagedRecordFlags()
    {
        var pokemon = CreatePokemon();
        var records = (ITechRecord)pokemon;
        var vm = new TechRecordEditorViewModel(records, pokemon);
        var item = vm.Records[0];
        item.IsActive = !item.IsActive;

        vm.SaveCommand.Execute(null);

        Assert.Equal(item.IsActive, records.GetMoveRecordFlag(item.Index));
    }

    [Fact]
    public void LegendsZaUsesZeroBasedStorageAndOneBasedDisplayIndex()
    {
        var pokemon = new PA9 { Species = (ushort)Species.Pikachu, CurrentLevel = 50 };
        var records = (ITechRecord)pokemon;
        var vm = new TechRecordEditorViewModel(records, pokemon);

        var item = vm.Records[0];
        Assert.Equal(0, item.Index);
        Assert.Equal(1, item.DisplayIndex);

        item.IsActive = true;
        vm.SaveCommand.Execute(null);

        Assert.True(records.GetMoveRecordFlag(0));
        Assert.False(records.GetMoveRecordFlag(1));
    }

    [Fact]
    public void ForceAllStagesAllSupportedRecords()
    {
        var pokemon = CreatePokemon();
        var vm = new TechRecordEditorViewModel((ITechRecord)pokemon, pokemon);

        vm.ForceAllCommand.Execute(null);

        Assert.All(vm.Records, item => Assert.True(item.IsActive));
    }
}
