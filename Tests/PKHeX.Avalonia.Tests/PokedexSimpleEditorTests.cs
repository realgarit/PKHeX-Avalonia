using PKHeX.Core;
using PKHeX.Presentation.ViewModels;
using Xunit;

namespace PKHeX.Avalonia.Tests;

public sealed class PokedexSimpleEditorTests
{
    [Fact]
    public void Emerald_NationalDex_RoundTripsThroughSave()
    {
        var sav = new SAV3E();
        Assert.False(sav.NationalDex);

        var vm = new PokedexSimpleEditorViewModel(sav);
        Assert.True(vm.IsEmerald);
        Assert.False(vm.NationalDexUnlocked);

        vm.NationalDexUnlocked = true;
        vm.SaveCommand.Execute(null);

        Assert.True(sav.NationalDex);
        Assert.True(new PokedexSimpleEditorViewModel(sav).NationalDexUnlocked);
    }

    [Fact]
    public void NonEmeraldGen3_DoesNotExposeNationalDexControl()
    {
        var vm = new PokedexSimpleEditorViewModel(new SAV3RS());

        Assert.False(vm.IsEmerald);
        Assert.False(vm.NationalDexUnlocked);
    }
}
