using Moq;
using PKHeX.Core;
using PKHeX.Presentation.ViewModels;

namespace PKHeX.Avalonia.Tests;

public class PokemonMetadataTests
{
    [Theory]
    [InlineData(GameVersion.ZA)]
    [InlineData(GameVersion.PLA)]
    public void SizeMetadata_RoundTripsWithoutNormalization(GameVersion version)
    {
        var save = BlankSaveFile.Get(version);
        var pk = save.BlankPKM;
        pk.Species = 25;
        pk.Version = version;
        var alpha = Assert.IsAssignableFrom<IAlpha>(pk);
        var size = Assert.IsAssignableFrom<IScaledSize>(pk);
        alpha.IsAlpha = true;
        size.HeightScalar = 37;
        size.WeightScalar = 129;
        if (pk is IScaledSize3 scale) scale.Scale = 42;
        var vm = Create(pk, save);
        Assert.True(vm.HasAlpha);
        Assert.True(vm.IsAlpha);
        Assert.Equal(37, vm.HeightScalar);
        var prepared = vm.PreparePKM();
        Assert.Equal(37, ((IScaledSize)prepared).HeightScalar);
        Assert.Equal(129, ((IScaledSize)prepared).WeightScalar);
        vm.IsAlpha = false;
        vm.HeightScalar = 255;
        vm.WeightScalar = 0;
        vm.Scale = 254;
        prepared = vm.PreparePKM();
        Assert.False(((IAlpha)prepared).IsAlpha);
        Assert.Equal(255, ((IScaledSize)prepared).HeightScalar);
        Assert.Equal(0, ((IScaledSize)prepared).WeightScalar);
        if (prepared is IScaledSize3 scaled) Assert.Equal(254, scaled.Scale);
        Assert.NotEmpty(vm.LegalityReport);
    }

    [Theory]
    [InlineData(0UL)]
    [InlineData(0xFEDCBA9876543210UL)]
    public void ZaMetadata_PreservesAndEditsIndependentFields(ulong tracker)
    {
        var save = BlankSaveFile.Get(GameVersion.ZA);
        var pk = new PA9 { Species = 25, Version = GameVersion.ZA, MetLevel = 12,
            ObedienceLevel = 43, BattleVersion = GameVersion.SW, HandlingTrainerLanguage = 3, Tracker = tracker };
        var vm = Create(pk, save);
        Assert.True(vm.HasBattleVersion && vm.HasObedienceLevel && vm.HasHandlerLanguage && vm.HasHomeTracker);
        Assert.Equal(43, vm.ObedienceLevel);
        Assert.Equal((int)GameVersion.SW, vm.BattleVersion);
        Assert.Contains(vm.BattleVersionList, item => item.Value == (int)GameVersion.SW);
        Assert.Equal(tracker.ToString("X16"), vm.HomeTracker);
        var prepared = Assert.IsType<PA9>(vm.PreparePKM());
        Assert.Equal(tracker, prepared.Tracker);
        Assert.Equal(3, prepared.HandlingTrainerLanguage);
        Assert.Equal(43, prepared.ObedienceLevel);
        vm.BattleVersion = (int)GameVersion.SH;
        vm.ObedienceLevel = 67;
        vm.HandlingTrainerLanguage = 7;
        vm.HomeTracker = "0123456789ABCDEF";
        prepared = Assert.IsType<PA9>(vm.PreparePKM());
        Assert.Equal(GameVersion.SH, prepared.BattleVersion);
        Assert.Equal(GameVersion.ZA, prepared.Version);
        Assert.Equal(12, prepared.MetLevel);
        Assert.Equal(67, prepared.ObedienceLevel);
        Assert.Equal(7, prepared.HandlingTrainerLanguage);
        Assert.Equal(0x0123456789ABCDEFUL, prepared.Tracker);
        vm.HomeTracker = "invalid";
        Assert.False(vm.IsHomeTrackerValid);
        Assert.Equal(0x0123456789ABCDEFUL, ((PA9)vm.PreparePKM()).Tracker);
        vm.HomeTracker = "0000000000000000";
        Assert.True(vm.IsHomeTrackerValid);
        Assert.Equal(0UL, ((PA9)vm.PreparePKM()).Tracker);
    }

    [Fact]
    public void EarlierFormat_HidesUnsupportedMetadata()
    {
        var vm = Create(new PK6(), new SAV6XY());
        Assert.False(vm.HasAlpha || vm.HasSizeScalars || vm.HasScale || vm.HasBattleVersion ||
                     vm.HasObedienceLevel || vm.HasHandlerLanguage || vm.HasHomeTracker);
    }

    private static PokemonEditorViewModel Create(PKM pk, SaveFile save)
    {
        GameInfo.FilteredSources = new FilteredGameDataSource(save, GameInfo.Sources);
        return new PokemonEditorViewModel(pk, save, Mock.Of<ISpriteRenderer>(), Mock.Of<IDialogService>(), Mock.Of<IWindowService>());
    }
}
