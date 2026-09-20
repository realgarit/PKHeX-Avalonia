using PKHeX.Core;
using PKHeX.Presentation.ViewModels;
using Xunit;

namespace PKHeX.Avalonia.Tests;

public sealed class RibbonEditorTests
{
    [Fact]
    public void CancelDiscardsBulkRibbonChanges()
    {
        var pokemon = new PK9 { Species = (ushort)Species.Pikachu, CurrentLevel = 50 };
        var before = pokemon.Data.ToArray();
        var vm = new RibbonEditorViewModel(pokemon);

        Assert.NotEmpty(vm.Ribbons);
        vm.GiveAllCommand.Execute(null);
        vm.CancelCommand.Execute(null);

        Assert.Equal(before, pokemon.Data.ToArray());
    }
}
