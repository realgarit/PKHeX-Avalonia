using PKHeX.Core;
using PKHeX.Presentation.ViewModels;

namespace PKHeX.Avalonia.Tests;

public class PlusRecordEditorTests
{
    [Fact]
    public void SaveAndCancel_KeepPlusFlagsSeparateFromTechnicalRecords()
    {
        var pk = new PA9 { Species = 25, Version = GameVersion.ZA, CurrentLevel = 50 };
        pk.SetMoveRecordFlag(0, true);
        var before = pk.Data.ToArray();
        var vm = new PlusRecordEditorViewModel(pk);
        vm.Records[0].IsActive = true;
        vm.Records.Last().IsActive = true;
        vm.CancelCommand.Execute(null);
        Assert.Equal(before, pk.Data);
        vm.SaveCommand.Execute(null);
        Assert.True(pk.GetMovePlusFlag(0));
        Assert.True(pk.GetMovePlusFlag(vm.Records.Last().Index));
        Assert.True(pk.GetMoveRecordFlag(0));
        var reopened = new PlusRecordEditorViewModel(pk);
        Assert.True(reopened.Records[0].IsActive);
        reopened.ClearCommand.Execute(null);
        Assert.True(pk.GetMovePlusFlag(0));
        reopened.SaveCommand.Execute(null);
        Assert.False(pk.GetMovePlusFlagAny());
        Assert.True(pk.GetMoveRecordFlag(0));
    }

    [Fact]
    public void SetLegal_IncludesEncounterAlphaMove()
    {
        var encounter = new EncounterStatic9a(13, 0, 45, 255)
        {
            Location = 55, Gender = 0, Nature = Nature.Naive, IsAlpha = true, FlawlessIVCount = 3,
        };
        var pk = Assert.IsType<PA9>(encounter.ConvertToPKM(new SimpleTrainerInfo(GameVersion.ZA)));
        var permit = (IPermitPlus)pk.PersonalInfo;
        var encounterInfo = PersonalTable.ZA[encounter.Species, encounter.Form];
        var alphaIndex = permit.PlusMoveIndexes.IndexOf(encounterInfo.AlphaMove);
        Assert.True(alphaIndex >= 0);
        pk.ClearPlusFlags(permit.PlusCountTotal);
        Assert.Contains(new LegalityAnalysis(pk).Results,
            result => result.Result == LegalityCheckResultCode.PlusMoveAlphaMissing_0);
        var vm = new PlusRecordEditorViewModel(pk);
        vm.SetLegalCommand.Execute(null);
        Assert.True(vm.Records[alphaIndex].IsActive);
        Assert.False(pk.GetMovePlusFlagAny());
        vm.SaveCommand.Execute(null);
        Assert.True(pk.GetMovePlusFlag(alphaIndex));
        var legality = new LegalityAnalysis(pk);
        Assert.DoesNotContain(legality.Results,
            result => result.Result == LegalityCheckResultCode.PlusMoveAlphaMissing_0);
    }
}
