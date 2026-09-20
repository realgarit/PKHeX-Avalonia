using PKHeX.Core;
using PKHeX.Presentation.ViewModels;
using Xunit;

namespace PKHeX.Avalonia.Tests;

public sealed class Pokedex8bEditorTests
{
    [Fact]
    public void CancelDiscardsEntryAndGlobalChanges()
    {
        var sav = new SAV8BS();
        var vm = new Pokedex8bEditorViewModel(sav);

        vm.State = 3;
        vm.HasNationalDex = true;
        vm.CancelCommand.Execute(null);

        Assert.Equal(ZukanState8b.None, sav.Zukan.GetState(1));
        Assert.False(sav.Zukan.HasNationalDex);
        Assert.False(sav.State.Edited);
    }

    [Fact]
    public void SaveCommitsCurrentEntryAndGlobalChanges()
    {
        var sav = new SAV8BS();
        var vm = new Pokedex8bEditorViewModel(sav);

        vm.State = 3;
        vm.HasNationalDex = true;
        vm.SaveCommand.Execute(null);

        Assert.Equal(ZukanState8b.Caught, sav.Zukan.GetState(1));
        Assert.True(sav.Zukan.HasNationalDex);
        Assert.True(sav.State.Edited);
    }
}
