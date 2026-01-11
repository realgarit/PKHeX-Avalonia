using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Moq;
using PKHeX.Avalonia.Services;
using PKHeX.Avalonia.ViewModels;
using PKHeX.Core;
using Xunit;

namespace PKHeX.Avalonia.Tests;

public class SaveFileTests
{
    private readonly Mock<ISpriteRenderer> _spriteRendererMock;
    private readonly Mock<IDialogService> _dialogServiceMock;

    public SaveFileTests()
    {
        _spriteRendererMock = new Mock<ISpriteRenderer>();
        _dialogServiceMock = new Mock<IDialogService>();
    }

    [AvaloniaFact]
    public void Can_Load_Gen3_Variables()
    {
        var sav = new SAV3E();
        // Set some data on the save file
        sav.TID16 = 12345;
        sav.OT = "ASH";

        // Create a PKM from this save
        var pkm = sav.GetPartySlotAtIndex(0);
        
        var vm = new PokemonEditorViewModel(pkm, sav, _spriteRendererMock.Object, _dialogServiceMock.Object);

        // Verify context is preserved
        Assert.Equal(sav.Context, vm.TargetPKM.Context);
    }

    // This test is a placeholder for when the user drops a real save file
    [AvaloniaFact]
    public void Load_Real_Save_File_If_Present()
    {
        string savePath = Path.Combine(Directory.GetCurrentDirectory(), "test_save.sav");
        
        // Skip if file doesn't exist
        if (!File.Exists(savePath))
        {
            return;
        }

        var fileInfo = new FileInfo(savePath);
        byte[] data = File.ReadAllBytes(savePath);
        var sav = SaveUtil.GetSaveFile(data);

        Assert.NotNull(sav);
        Assert.True(sav.ChecksumsValid);

        // Check first pokemon
        var pkm = sav.GetPartySlotAtIndex(0);
        var vm = new PokemonEditorViewModel(pkm, sav, _spriteRendererMock.Object, _dialogServiceMock.Object);

        Assert.NotNull(vm.SpeciesList);
        Assert.True(vm.SpeciesList.Count > 0);
    }
}
