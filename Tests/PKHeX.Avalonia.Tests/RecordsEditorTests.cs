using PKHeX.Presentation.ViewModels;
using PKHeX.Core;
using Xunit.Abstractions;

namespace PKHeX.Avalonia.Tests;

/// <summary>
/// Behavioral tests for RecordsEditorViewModel.
/// Records exist for Gen5-8; other generations have HasRecords=false.
/// Editing a RecordItemViewModel stages values until Save
/// via OnValueChanged partial method.
/// </summary>
public class RecordsEditorTests(ITestOutputHelper output)
{
    [Fact]
    public void RetypingOriginalOutOfRangeValue_DiscardsIntermediateValidEdit()
    {
        var storage = new Moq.Mock<ITrainerStatRecord>();
        storage.Setup(s => s.GetRecordMax(0)).Returns(100);
        var record = new RecordItemViewModel(0, "record", 101, storage.Object);
        record.ValueText = "1";
        Assert.True(record.IsChanged);
        record.ValueText = "101";
        Assert.True(record.CanCommit);
        Assert.False(record.IsChanged);
        Assert.Equal(101, record.Value);
    }

    [Theory]
    [InlineData(GameVersion.W2)]
    [InlineData(GameVersion.X)]
    [InlineData(GameVersion.SN)]
    [InlineData(GameVersion.SW)]
    [InlineData(GameVersion.BD)]
    public void StagingLimitsCancelResetAndCommit(GameVersion version)
    {
        var save = BlankSaveFile.Get(version);
        var before = save.Data.ToArray();
        var vm = new RecordsEditorViewModel(save);
        Assert.Equal(before, save.Data);
        var record = vm.Records.First(r => r.Maximum > 1);
        var id = record.Id;
        var original = record.Value;
        record.ValueText = "1";
        Assert.Equal(before, save.Data);
        record.ValueText = "99999999999999999999999";
        Assert.False(record.IsValid);
        Assert.False(vm.SaveCommand.CanExecute(null));
        record.ValueText = ((long)record.Maximum + 1).ToString();
        Assert.False(record.IsValid);
        record.ValueText = "-1";
        Assert.False(record.IsValid);
        record.ValueText = "1.5";
        Assert.False(record.IsValid);
        vm.RefreshRecordsCommand.Execute(null);
        Assert.Equal(original, vm.Records.Single(r => r.Id == id).Value);
        Assert.Equal(before, save.Data);
        vm.Records.Single(r => r.Id == id).Value = 1;
        vm.CancelCommand.Execute(null);
        Assert.Equal(before, save.Data);
        vm.Records.Single(r => r.Id == id).Value = 1;
        vm.SaveCommand.Execute(null);
        Assert.Equal(1, new RecordsEditorViewModel(save).Records.Single(r => r.Id == id).Value);
        Assert.True(save.State.Edited);
    }

    [Fact]
    public void Gen5_Uses16BitLimitsAndReencryptsAfterCommit()
    {
        var save = new SAV5B2W2();
        save.Records.SetRecord16(0, 123);
        save.Records.EndAccess();
        var before = save.Data.ToArray();
        var vm = new RecordsEditorViewModel(save);
        var record = vm.Records.Single(r => r.Id == Record5.Record32);
        Assert.Equal(ushort.MaxValue, record.Maximum);
        Assert.Equal(123, record.Value);
        Assert.Equal(before, save.Data);
        record.Value = 54321;
        vm.SaveCommand.Execute(null);
        var reread = new RecordsEditorViewModel(save);
        Assert.Equal(54321, reread.Records.Single(r => r.Id == record.Id).Value);
    }

    // -----------------------------------------------------------------------
    // 1. HasRecords is true for Gen5-8 saves
    // -----------------------------------------------------------------------

    [Theory]
    [InlineData(GameVersion.W2, "Gen5-White2")]
    [InlineData(GameVersion.X,  "Gen6-X")]
    [InlineData(GameVersion.SN, "Gen7-Sun")]
    [InlineData(GameVersion.SW, "Gen8-Sword")]
    public void Records_Gen6To8_HasRecords_True(GameVersion version, string label)
    {
        var sav = BlankSaveFile.Get(version);
        var vm = new RecordsEditorViewModel(sav);

        Assert.True(vm.HasRecords, $"{label}: expected HasRecords=true");
        Assert.NotEmpty(vm.Records);
        output.WriteLine($"{label}: HasRecords=true, {vm.Records.Count} records loaded ✓");
    }

    // -----------------------------------------------------------------------
    // 2. HasRecords is false for Gen1-4 and Gen9 saves
    // -----------------------------------------------------------------------

    // Gen5 has a RecordList but SAV5 does not implement ITrainerStatRecord,
    // so HasRecords is false for Gen5 saves.
    [Theory]
    [InlineData(GameVersion.RD, "Gen1-Red")]
    [InlineData(GameVersion.GD, "Gen2-Gold")]
    [InlineData(GameVersion.E,  "Gen3-Emerald")]
    [InlineData(GameVersion.Pt, "Gen4-Platinum")]
    [InlineData(GameVersion.SL, "Gen9-Scarlet")]
    public void Records_OtherGens_HasRecords_False(GameVersion version, string label)
    {
        var sav = BlankSaveFile.Get(version);
        var vm = new RecordsEditorViewModel(sav);

        Assert.False(vm.HasRecords, $"{label}: expected HasRecords=false");
        Assert.Empty(vm.Records);
        output.WriteLine($"{label}: HasRecords=false ✓");
    }

    // -----------------------------------------------------------------------
    // 3. Editing a record value immediately writes through to the save
    // -----------------------------------------------------------------------

    [Fact]
    public void Records_Gen6_EditValue_UpdatesSaveOnlyOnSave()
    {
        var sav = new SAV6XY();
        var vm = new RecordsEditorViewModel(sav);

        Assert.True(vm.HasRecords);
        Assert.NotEmpty(vm.Records);

        var record = vm.Records[0];
        var recordId = record.Id;
        var original = record.Value;

        var storage = (ITrainerStatRecord)sav;
        var newValue = original + 100;
        record.Value = newValue;

        Assert.Equal(original, storage.GetRecord(recordId));
        vm.SaveCommand.Execute(null);
        // Explicit Save commits the staged value.
        Assert.Equal(newValue, storage.GetRecord(recordId));
        output.WriteLine($"Gen6 record[{recordId}]: {original} → {newValue} immediately in save ✓");
    }

    // -----------------------------------------------------------------------
    // 4. Search filter narrows filtered records
    // -----------------------------------------------------------------------

    [Fact]
    public void Records_Gen7_SearchFilter_NarrowsResults()
    {
        var sav = BlankSaveFile.Get(GameVersion.SN);
        var vm = new RecordsEditorViewModel(sav);

        Assert.True(vm.HasRecords);
        var totalCount = vm.FilteredRecords.Count;
        Assert.True(totalCount > 0);

        // Filter to something that won't match anything
        vm.SearchText = "ZZZZNONEXISTENT9999";
        Assert.Empty(vm.FilteredRecords);

        // Clear filter → all records back
        vm.SearchText = string.Empty;
        Assert.Equal(totalCount, vm.FilteredRecords.Count);

        output.WriteLine($"Gen7: SearchFilter works, {totalCount} records total ✓");
    }

    // -----------------------------------------------------------------------
    // 5. RefreshRecordsCommand reloads without throwing
    // -----------------------------------------------------------------------

    [Fact]
    public void Records_Gen8_Refresh_DoesNotThrow()
    {
        var sav = BlankSaveFile.Get(GameVersion.SW);
        var vm = new RecordsEditorViewModel(sav);

        var countBefore = vm.Records.Count;
        var ex = Record.Exception(() => vm.RefreshRecordsCommand.Execute(null));

        Assert.Null(ex);
        Assert.Equal(countBefore, vm.Records.Count);
        output.WriteLine($"Gen8: Refresh OK, {countBefore} records ✓");
    }

    // -----------------------------------------------------------------------
    // 6. FilteredRecords equals Records when search is empty on load
    // -----------------------------------------------------------------------

    [Fact]
    public void Records_Gen6_FilteredRecords_EqualsRecordsOnLoad()
    {
        var sav = BlankSaveFile.Get(GameVersion.X);
        var vm = new RecordsEditorViewModel(sav);

        Assert.True(vm.HasRecords);
        Assert.Equal(vm.Records.Count, vm.FilteredRecords.Count);
        output.WriteLine($"Gen6: FilteredRecords={vm.FilteredRecords.Count} == Records={vm.Records.Count} ✓");
    }
}
