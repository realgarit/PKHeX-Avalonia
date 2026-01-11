using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Moq;
using PKHeX.Avalonia.Services;
using PKHeX.Avalonia.ViewModels;
using PKHeX.Core;
using Xunit;

namespace PKHeX.Avalonia.Tests;

public class PokemonEditorTests
{
    private readonly Mock<ISpriteRenderer> _spriteRendererMock;
    private readonly Mock<IDialogService> _dialogServiceMock;
    private readonly SaveFile _saveFile;

    public PokemonEditorTests()
    {
        _spriteRendererMock = new Mock<ISpriteRenderer>();
        _dialogServiceMock = new Mock<IDialogService>();
        
        // Setup a basic Gen 3 save file for testing context
        // PKHeX.Core allows creating blank saves
        _saveFile = new SAV3E(); 
    }

    [AvaloniaFact]
    public void ViewModel_Initializes_Correctly()
    {
        var pkm = new PK3(); // Gen 3 Pokemon
        pkm.Species = 25; // Pikachu
        pkm.Language = (int)LanguageID.English;

        var vm = new PokemonEditorViewModel(pkm, _saveFile, _spriteRendererMock.Object, _dialogServiceMock.Object);

        Assert.Equal(25, vm.Species);
        Assert.Equal("Pikachu", GameInfo.Strings.Species[25]);
    }

    [AvaloniaFact]
    public void Stat_Recalculation_Updates_On_IV_Change()
    {
        var pkm = new PK3 { Species = 1 }; // Bulbasaur
        pkm.CurrentLevel = 50;
        
        var vm = new PokemonEditorViewModel(pkm, _saveFile, _spriteRendererMock.Object, _dialogServiceMock.Object);
        
        // Capture initial stats
        var initialHP = vm.Stat_HP;

        // Change IV
        vm.IvHP = 31;
        
        // Stats are computed properties that call RecalculateStats, so accessing them should trigger it if setup correctly in the getter
        // In the VM code: public int Stat_HP { get { RecalculateStats(); return _pk.Stat_HPMax; } }
        
        Assert.True(vm.Stat_HP >= initialHP, "HP should increase or stay same when setting IV to max");
        Assert.Equal(31, vm.TargetPKM.IV_HP);
    }

    [AvaloniaFact]
    public void PreparePKM_Updates_Internal_PKM()
    {
        var pkm = new PK3();
        var vm = new PokemonEditorViewModel(pkm, _saveFile, _spriteRendererMock.Object, _dialogServiceMock.Object);

        vm.Nickname = "Sparky";
        vm.Level = 99;
        vm.Exp = 1000000; // Required for CurrentLevel to reflect 99 (ish)
        
        var resultPkm = vm.PreparePKM();

        Assert.Equal("Sparky", resultPkm.Nickname);
        Assert.True(resultPkm.CurrentLevel >= 99);
    }

    [AvaloniaFact]
    public void Shiny_Toggle_Updates_Property_And_Sprite()
    {
        var pkm = new PK3();
        var vm = new PokemonEditorViewModel(pkm, _saveFile, _spriteRendererMock.Object, _dialogServiceMock.Object);

        bool initialShiny = vm.IsShiny;
        
        vm.ToggleShinyCommand.Execute(null);

        Assert.NotEqual(initialShiny, vm.IsShiny);
        _spriteRendererMock.Verify(x => x.GetSprite(It.IsAny<PKM>()), Times.AtLeastOnce);
    }
}
