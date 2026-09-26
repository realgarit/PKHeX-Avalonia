using PKHeX.Core;
using PKHeX.Presentation.ViewModels;

namespace PKHeX.Avalonia.Tests;

public class PokebeanTransactionTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void EditBulkResetAndCancel_DoNotMutateSave(bool ultra)
    {
        SAV7 save = ultra ? new SAV7USUM() : new SAV7SM();
        save.ResortSave.GetBeans()[0] = 42;
        var before = save.Data.ToArray();
        var vm = new PokebeanEditorViewModel(save);
        vm.Beans[0].Count = 15;
        Assert.Equal(before, save.Data);
        vm.FillAllCommand.Execute(null);
        Assert.All(vm.Beans, bean => Assert.Equal(255, bean.Count));
        Assert.Equal(before, save.Data);
        vm.ClearAllCommand.Execute(null);
        Assert.All(vm.Beans, bean => Assert.Equal(0, bean.Count));
        Assert.Equal(before, save.Data);
        vm.RefreshCommand.Execute(null);
        Assert.Equal(42, vm.Beans[0].Count);
        vm.FillAllCommand.Execute(null);
        var closed = false;
        vm.CloseRequested = () => closed = true;
        vm.CancelCommand.Execute(null);
        Assert.True(closed);
        Assert.Equal(before, save.Data);
        Assert.Equal(42, vm.Beans[0].Count);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Save_CommitsAllCountsAndMarksEdited(bool ultra)
    {
        SAV7 save = ultra ? new SAV7USUM() : new SAV7SM();
        var vm = new PokebeanEditorViewModel(save);
        vm.FillAllCommand.Execute(null);
        vm.Beans[0].Count = 17;
        vm.SaveCommand.Execute(null);
        var reopened = new PokebeanEditorViewModel(save);
        Assert.Equal(17, reopened.Beans[0].Count);
        Assert.All(reopened.Beans.Skip(1), bean => Assert.Equal(255, bean.Count));
        Assert.True(save.State.Edited);
        reopened.ClearAllCommand.Execute(null);
        reopened.SaveCommand.Execute(null);
        Assert.All(save.ResortSave.GetBeans().ToArray(), value => Assert.Equal(0, value));
    }
}
