using PKHeX.Avalonia.ViewModels;
using PKHeX.Core;
using Xunit;

namespace PKHeX.Avalonia.Tests;

public class PokedexTests
{
    [Fact]
    public void Pokedex8_LoadAndSave_VerifyFlags()
    {
        // Arrange
        var sav = new SAV8SWSH();
        var zukan = sav.Blocks.Zukan;
        
        // Setup initial state for a species (e.g. Bulbasaur = 1)
        var entryInfo = Zukan8.GetRawIndexes(PersonalTable.SWSH, zukan.GetRevision(), Zukan8Index.TotalCount)
                              .FirstOrDefault(z => z.Species == 1);
        
        Assert.NotEqual(0, entryInfo.Species); // Sanity check
        var entry = entryInfo.Entry;

        zukan.SetCaught(entry, false);
        zukan.SetSeenRegion(entry, 0, 0, false); // Form 0, Seen Male

        // Act
        var vm = new Pokedex8EditorViewModel(sav);
        
        // Select Bulbasaur
        var item = vm.FilteredSpecies.FirstOrDefault(x => x.Text.Contains("Bulbasaur"));
        Assert.NotNull(item);
        vm.SelectedSpecies = item;

        // Verify loaded state
        Assert.False(vm.Caught);
        Assert.False(vm.Forms[0].SeenMale);

        // Modify
        vm.Caught = true;
        vm.Forms[0].SeenMale = true;
        
        // Save
        vm.SaveCurrent();

        // Assert
        Assert.True(zukan.GetCaught(entry));
        Assert.True(zukan.GetSeenRegion(entry, 0, 0));
    }
}
