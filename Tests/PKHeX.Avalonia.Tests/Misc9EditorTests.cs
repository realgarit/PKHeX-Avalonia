using PKHeX.Core;
using PKHeX.Avalonia.Tests.Fixtures;
using PKHeX.Presentation.ViewModels;

namespace PKHeX.Avalonia.Tests;

public class Misc9EditorTests
{
    [Fact]
    public void CancelDiscardsSvProgressionChanges()
    {
        var source = LoadSave();
        var money = source.Money;
        var vm = new Misc9EditorViewModel(source);

        vm.Money = money + 1;
        vm.MaxMoneyCommand.Execute(null);
        vm.CancelCommand.Execute(null);

        Assert.Equal(money, source.Money);
    }

    [Fact]
    public void SaveCommitsSvProgressionChangesAndCloses()
    {
        var source = LoadSave();
        var closed = false;
        var expectedMoney = 1u;
        var vm = new Misc9EditorViewModel(source)
        {
            CloseRequested = () => closed = true,
            Money = expectedMoney,
        };

        var ex = Record.Exception(() =>
        {
            vm.UnlockAllFlyLocationsCommand.Execute(null);
            vm.CollectAllStakesCommand.Execute(null);
            vm.UnlockAllTMRecipesCommand.Execute(null);
            vm.UnlockBikeUpgradesCommand.Execute(null);
            vm.UnlockBaseClothingCommand.Execute(null);
        });

        vm.SaveCommand.Execute(null);

        Assert.Null(ex);
        Assert.Equal(expectedMoney, source.Money);
        Assert.True(closed);
    }

    private static SAV9SV LoadSave()
    {
        var path = Path.Combine(SaveFileFixture.FindSaveFilesPath()!, "gen9_violet.main");
        return Assert.IsType<SAV9SV>(SaveFileFixture.LoadSave(path));
    }
}
