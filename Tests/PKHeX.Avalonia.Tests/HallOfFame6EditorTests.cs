using Moq;
using PKHeX.Core;
using PKHeX.Presentation.ViewModels;

namespace PKHeX.Avalonia.Tests;

/// <summary>
/// Gen 6 Hall of Fame editing: edits land in a clone, Save copies them to the save, Cancel discards
/// them, and Delete Entry shifts later entries down like the game.
/// </summary>
public class HallOfFame6EditorTests
{
    private static SAV6XY CreateSaveWithEntries(params ushort[] firstSpeciesPerEntry)
    {
        var sav = new SAV6XY();
        var fame = sav.HallOfFame;
        for (int i = 0; i < firstSpeciesPerEntry.Length; i++)
        {
            var index = new HallFame6Index(fame.GetEntry(i)[^HallFame6Index.SIZE..]);
            index.ClearIndex = (uint)(i + 1);
            index.Year = 14;
            index.Month = 10;
            index.Day = 12;
            index.HasData = true;

            var member = new HallFame6Entity(fame.GetEntity(i, 0), sav.Language);
            member.Species = firstSpeciesPerEntry[i];
            member.Level = 50;
        }
        return sav;
    }

    private static HallOfFameEditorViewModel CreateViewModel(SaveFile sav)
        => new(sav, Mock.Of<ISpriteRenderer>());

    private static HallFame6Entity ReadMember(SAV6XY sav, int entry, int member)
        => new(sav.HallOfFame.GetEntity(entry, member), sav.Language);

    [Fact]
    public void LoadsEntriesWithDataAndTheirMembers()
    {
        var sav = CreateSaveWithEntries((ushort)Species.Chespin, (ushort)Species.Froakie);
        var vm = CreateViewModel(sav);

        Assert.Equal(2, vm.Entries.Count);
        var entry = Assert.Single(vm.Entries, e => e.Index == 0);
        Assert.Equal(1, entry.ClearNumber);
        Assert.Equal(new DateTime(2014, 10, 12), entry.Date?.Date);
        var member = Assert.Single(entry.Members);
        Assert.Equal((int)Species.Chespin, member.Species);
        Assert.Equal(50, member.Level);
    }

    [Fact]
    public void Save_WritesMemberAndEntryEditsToTheSave()
    {
        var sav = CreateSaveWithEntries((ushort)Species.Chespin);
        var vm = CreateViewModel(sav);
        var entry = vm.Entries[0];
        var member = entry.Members[0];

        member.Level = 64;
        member.IsShiny = true;
        member.Move1 = (int)Move.Tackle;
        member.EncryptionConstant = "12345678";
        entry.ClearNumber = 7;
        vm.SaveCommand.Execute(null);

        var written = ReadMember(sav, 0, 0);
        Assert.Equal(64u, written.Level);
        Assert.True(written.IsShiny);
        Assert.Equal((ushort)Move.Tackle, written.Move1);
        Assert.Equal(0x12345678u, written.EncryptionConstant);
        Assert.Equal(7u, new HallFame6Index(sav.HallOfFame.GetEntry(0)[^HallFame6Index.SIZE..]).ClearIndex);
    }

    [Fact]
    public void Cancel_LeavesTheSaveUnchanged()
    {
        var sav = CreateSaveWithEntries((ushort)Species.Chespin);
        var vm = CreateViewModel(sav);

        vm.Entries[0].Members[0].Level = 99;
        vm.CancelCommand.Execute(null);

        Assert.Equal(50u, ReadMember(sav, 0, 0).Level);
    }

    [Fact]
    public void DeleteEntry_ShiftsLaterEntriesDown()
    {
        var sav = CreateSaveWithEntries((ushort)Species.Chespin, (ushort)Species.Froakie);
        var vm = CreateViewModel(sav);

        vm.SelectedEntry = vm.Entries[0];
        vm.DeleteEntryCommand.Execute(null);

        var remaining = Assert.Single(vm.Entries);
        Assert.Equal(0, remaining.Index);
        Assert.Equal((int)Species.Froakie, remaining.Members[0].Species);
    }
}
