using PKHeX.Core;
using PKHeX.Presentation.ViewModels;

namespace PKHeX.Avalonia.Tests;

public class Poffin8bTransactionTests
{
    [Fact]
    public void Fixture_RoundTripsTypeSmoothnessFlavorsAndNew()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "PKHeX.sln"))) directory = directory.Parent;
        Assert.NotNull(directory);
        var path = Path.Combine(directory.FullName, "Tests", "savefiles", "gen8b_brilliantdiamond.bin");
        var save = new SAV8BS(File.ReadAllBytes(path));
        var vm = new Poffin8bEditorViewModel(save);
        var before = save.Data.ToArray();
        var item = vm.Poffins[0];
        item.MstID = 0;
        Assert.Equal(Util.GetStringList("poffin8b", "en")[1], item.PoffinName);
        item.Level = 86;
        item.Taste = 37;
        item.Spicy = 1; item.Dry = 86; item.Sweet = 2; item.Bitter = 3; item.Sour = 11;
        item.IsNew = true;
        Assert.Equal(before, save.Data);
        vm.SaveCommand.Execute(null);
        var stored = save.Poffins.GetPoffin(0);
        Assert.Equal(0, stored.MstID);
        Assert.Equal(86, stored.Level);
        Assert.Equal(37, stored.Taste);
        Assert.Equal(1, stored.FlavorSpicy); Assert.Equal(86, stored.FlavorDry);
        Assert.Equal(2, stored.FlavorSweet); Assert.Equal(3, stored.FlavorBitter); Assert.Equal(11, stored.FlavorSour);
        Assert.True(stored.IsNew);
    }

    [Fact]
    public void PresetsAreStagedAndUnknownAndEmptyTypesArePreserved()
    {
        var save = new SAV8BS();
        var empty = save.Poffins.GetPoffin(0); empty.ToNull(); save.Poffins.SetPoffin(0, empty);
        var unknown = save.Poffins.GetPoffin(1); unknown.MstID = 200; save.Poffins.SetPoffin(1, unknown);
        var before = save.Data.ToArray();
        var vm = new Poffin8bEditorViewModel(save);
        Assert.Equal(GameInfo.Strings.Item[0], vm.Poffins[0].PoffinName);
        Assert.Contains(vm.Poffins[1].TypeChoices, item => item.Value == 200);
        vm.SaveCommand.Execute(null);
        Assert.Equal(before, save.Data);
        vm.FillAllCommand.Execute(null);
        Assert.All(vm.Poffins, item => { Assert.Equal(28, item.MstID); Assert.Equal(60, item.Level); Assert.Equal(255, item.Taste); });
        vm.CancelCommand.Execute(null);
        Assert.Equal(before, save.Data);
        vm = new Poffin8bEditorViewModel(save);
        vm.FillAllCommand.Execute(null);
        vm.SaveCommand.Execute(null);
        Assert.All(save.Poffins.GetPoffins(), item => Assert.Equal(255, item.FlavorSweet));
        vm.ClearAllCommand.Execute(null);
        Assert.All(save.Poffins.GetPoffins(), item => Assert.Equal(28, item.MstID));
        vm.SaveCommand.Execute(null);
        Assert.All(save.Poffins.GetPoffins(), item => Assert.True(item.IsNull));
    }
}
