using PKHeX.Core;

namespace PKHeX.Avalonia.Tests;

/// <summary>
/// Let's Go Pokémon (PB7) have no EVs; their stat bonus is the six 0-200 Awakening Values. The
/// editor must load and write AVs for them and leave the EV inputs out.
/// </summary>
public class AwakeningValuesEditorTests
{
    private static PB7 CreatePikachu()
    {
        var pk = new PB7 { Species = (ushort)Species.Pikachu, CurrentLevel = 50 };
        pk.AV_HP = 10;
        pk.AV_ATK = 20;
        pk.AV_DEF = 30;
        pk.AV_SPA = 40;
        pk.AV_SPD = 50;
        pk.AV_SPE = 60;
        return pk;
    }

    [Fact]
    public void LetsGoEntity_LoadsAwakeningValues()
    {
        var (vm, _, _) = TestHelpers.CreateTestViewModel(CreatePikachu(), new SAV7b());

        Assert.True(vm.HasAwakeningValues);
        Assert.Equal(AwakeningUtil.AwakeningMax, vm.MaxAV);
        Assert.Equal(new[] { 10, 20, 30, 40, 50, 60 }, new[] { vm.AvHP, vm.AvATK, vm.AvDEF, vm.AvSPA, vm.AvSPD, vm.AvSPE });
        Assert.Equal(210, vm.AVTotal);
    }

    [Fact]
    public void LetsGoEntity_EditedAwakeningValues_AreWrittenAndClamped()
    {
        var (vm, _, _) = TestHelpers.CreateTestViewModel(CreatePikachu(), new SAV7b());

        vm.AvHP = 200;
        vm.AvSPE = 999;
        var pk = (PB7)vm.PreparePKM();

        Assert.Equal(200, pk.AV_HP);
        Assert.Equal(AwakeningUtil.AwakeningMax, pk.AV_SPE);
        Assert.Equal(0, pk.EV_HP);
    }

    [Fact]
    public void LetsGoEntity_ClearAVs_ZeroesAllAwakeningValues()
    {
        var (vm, _, _) = TestHelpers.CreateTestViewModel(CreatePikachu(), new SAV7b());

        vm.ClearAVsCommand.Execute(null);
        var pk = (PB7)vm.PreparePKM();

        Assert.Equal(0, vm.AVTotal);
        Assert.Equal(0, pk.AV_HP + pk.AV_ATK + pk.AV_DEF + pk.AV_SPA + pk.AV_SPD + pk.AV_SPE);
    }

    [Fact]
    public void EvFormat_HasNoAwakeningValues()
    {
        var (vm, _, _) = TestHelpers.CreateTestViewModel(new PK7 { Species = (ushort)Species.Pikachu }, new SAV7SM());

        Assert.False(vm.HasAwakeningValues);
    }
}
