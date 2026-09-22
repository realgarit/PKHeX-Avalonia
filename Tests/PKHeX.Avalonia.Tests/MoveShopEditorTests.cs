using System.Linq;
using PKHeX.Avalonia.Tests.Fixtures;
using PKHeX.Core;
using PKHeX.Presentation.ViewModels;

namespace PKHeX.Avalonia.Tests;

public class MoveShopEditorTests
{
    public MoveShopEditorTests()
    {
        GameInfo.CurrentLanguage = "en";
        GameInfo.Strings = GameInfo.GetStrings("en");
    }

    [Fact]
    public void SaveCommitsMoveShopFlagsAndCloses()
    {
        var source = LoadPokemon();
        var before = source.Data.ToArray();
        var closed = false;
        var vm = new MoveShopEditorViewModel(source)
        {
            CloseRequested = () => closed = true,
        };

        Assert.NotEmpty(vm.Moves);
        var permitted = vm.Moves.First(x => x.IsPermitted);
        permitted.IsPurchased = true;
        vm.SaveCommand.Execute(null);

        Assert.True(((IMoveShop8)source).GetPurchasedRecordFlag(permitted.Index));
        Assert.NotEqual(before, source.Data.ToArray());
        Assert.True(closed);
    }

    [Fact]
    public void CancelDiscardsBulkMoveShopChanges()
    {
        var source = LoadPokemon();
        var before = source.Data.ToArray();
        var vm = new MoveShopEditorViewModel(source);

        vm.SetAllCommand.Execute(null);
        vm.CancelCommand.Execute(null);

        Assert.Equal(before, source.Data.ToArray());
    }

    [Fact]
    public void ForbiddenMoveCannotBePurchased()
    {
        var vm = new MoveShopEditorViewModel(LoadPokemon());
        var forbidden = vm.Moves.First(x => !x.IsPermitted);

        forbidden.IsPurchased = true;

        Assert.False(forbidden.IsPurchased);
        Assert.False(forbidden.IsMastered);
    }

    private static PA8 LoadPokemon()
    {
        var path = Path.Combine(SaveFileFixture.FindSaveFilesPath()!, "gen8a_legendsarceus.main");
        var sav = Assert.IsType<SAV8LA>(SaveFileFixture.LoadSave(path));
        for (var i = 0; i < sav.BoxSlotCount; i++)
        {
            if (sav.GetBoxSlotAtIndex(i) is PA8 { Species: > 0 } pk)
                return pk;
        }

        throw new InvalidOperationException("The Legends: Arceus fixture has no Pokémon for Move Shop tests.");
    }
}
