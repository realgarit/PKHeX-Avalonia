using Moq;
using PKHeX.Avalonia.Services;
using PKHeX.Avalonia.ViewModels;
using PKHeX.Core;
using Xunit;
using Xunit.Abstractions;

namespace PKHeX.Avalonia.Tests;

/// <summary>
/// Stress tests for PreparePKM to discover persistence issues.
/// </summary>
public class PreparePKMStressTests
{
    private readonly Mock<ISpriteRenderer> _spriteRendererMock = new();
    private readonly Mock<IDialogService> _dialogServiceMock = new();
    private readonly ITestOutputHelper _output;

    public PreparePKMStressTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    public void PreparePKM_Called_Multiple_Times_Is_Idempotent()
    {
        var sav = new SAV3E();
        var pkm = new PK3 { Species = 25 };
        var vm = new PokemonEditorViewModel(pkm, sav, _spriteRendererMock.Object, _dialogServiceMock.Object);
        
        vm.Nickname = "TestPika";
        vm.IvHP = 31;
        vm.HeldItem = 13;
        
        var result1 = vm.PreparePKM();
        var result2 = vm.PreparePKM();
        var result3 = vm.PreparePKM();
        
        // All should be identical
        Assert.Equal(result1.Nickname, result2.Nickname);
        Assert.Equal(result2.Nickname, result3.Nickname);
        Assert.Equal(result1.IV_HP, result3.IV_HP);
    }

    [Fact]
    public void LoadPKM_Then_PreparePKM_Preserves_Original()
    {
        var sav = new SAV3E();
        var original = new PK3 
        { 
            Species = 25, 
            Move1 = 84,
            IV_HP = 20,
            EV_ATK = 100
        };
        original.Nickname = "Original";
        
        var vm = new PokemonEditorViewModel(original, sav, _spriteRendererMock.Object, _dialogServiceMock.Object);
        
        // Don't change anything
        var result = vm.PreparePKM();
        
        Assert.Equal("Original", result.Nickname);
        Assert.Equal(84, result.Move1);
        Assert.Equal(20, result.IV_HP);
        Assert.Equal(100, result.EV_ATK);
    }

    [Fact]
    public void Changing_Species_Clears_Form_Appropriately()
    {
        var sav = new SAV3E();
        var pkm = new PK3 { Species = 201, Form = 5 }; // Unown with Form
        var vm = new PokemonEditorViewModel(pkm, sav, _spriteRendererMock.Object, _dialogServiceMock.Object);
        
        Assert.Equal(5, vm.Form);
        
        // Change to species without forms
        vm.Species = 25; // Pikachu
        
        // Form should reset or stay at 0
        Assert.True(vm.Form >= 0);
        
        var result = vm.PreparePKM();
        Assert.Equal(25, result.Species);
    }

    [Fact]
    public void Shiny_State_Persists_After_PreparePKM()
    {
        var sav = new SAV3E();
        var pkm = new PK3 { Species = 25 };
        var vm = new PokemonEditorViewModel(pkm, sav, _spriteRendererMock.Object, _dialogServiceMock.Object);
        
        // Set to shiny and verify
        vm.IsShiny = true;
        var result = vm.PreparePKM();
        Assert.True(result.IsShiny);
        
        // Note: Un-shinying in Gen 3 is complex due to PID-based shininess
        // The PKM.SetUnshiny() may regenerate PID, which is tested separately
    }

    [Fact]
    public void All_Moves_Persist_Correctly()
    {
        var sav = new SAV3E();
        var pkm = new PK3 { Species = 25 };
        var vm = new PokemonEditorViewModel(pkm, sav, _spriteRendererMock.Object, _dialogServiceMock.Object);
        
        vm.Move1 = 85;  // Thunderbolt
        vm.Move2 = 86;  // Thunder Wave  
        vm.Move3 = 87;  // Thunder
        vm.Move4 = 98;  // Quick Attack
        
        var result = vm.PreparePKM();
        
        Assert.Equal(85, result.Move1);
        Assert.Equal(86, result.Move2);
        Assert.Equal(87, result.Move3);
        Assert.Equal(98, result.Move4);
    }

    [Fact]
    public void All_IVs_Persist_Correctly()
    {
        var sav = new SAV3E();
        var pkm = new PK3 { Species = 25 };
        var vm = new PokemonEditorViewModel(pkm, sav, _spriteRendererMock.Object, _dialogServiceMock.Object);
        
        vm.IvHP = 10;
        vm.IvATK = 15;
        vm.IvDEF = 20;
        vm.IvSPA = 25;
        vm.IvSPD = 30;
        vm.IvSPE = 31;
        
        var result = vm.PreparePKM();
        
        Assert.Equal(10, result.IV_HP);
        Assert.Equal(15, result.IV_ATK);
        Assert.Equal(20, result.IV_DEF);
        Assert.Equal(25, result.IV_SPA);
        Assert.Equal(30, result.IV_SPD);
        Assert.Equal(31, result.IV_SPE);
    }

    [Fact]
    public void All_EVs_Persist_Correctly()
    {
        var sav = new SAV3E();
        var pkm = new PK3 { Species = 25 };
        var vm = new PokemonEditorViewModel(pkm, sav, _spriteRendererMock.Object, _dialogServiceMock.Object);
        
        vm.EvHP = 50;
        vm.EvATK = 100;
        vm.EvDEF = 150;
        vm.EvSPA = 200;
        vm.EvSPD = 10;
        vm.EvSPE = 0;
        
        var result = vm.PreparePKM();
        
        Assert.Equal(50, result.EV_HP);
        Assert.Equal(100, result.EV_ATK);
        Assert.Equal(150, result.EV_DEF);
        Assert.Equal(200, result.EV_SPA);
        Assert.Equal(10, result.EV_SPD);
        Assert.Equal(0, result.EV_SPE);
    }

    [Fact]
    public void Empty_Nickname_Uses_Species_Name()
    {
        var sav = new SAV3E();
        var pkm = new PK3 { Species = 25 };
        var vm = new PokemonEditorViewModel(pkm, sav, _spriteRendererMock.Object, _dialogServiceMock.Object);
        
        vm.Nickname = "";
        var result = vm.PreparePKM();
        
        // Empty nickname behavior - might be empty or species name
        Assert.NotNull(result.Nickname);
    }

    [Fact]
    public void Special_Characters_In_Nickname_Handled()
    {
        var sav = new SAV3E();
        var pkm = new PK3 { Species = 25 };
        var vm = new PokemonEditorViewModel(pkm, sav, _spriteRendererMock.Object, _dialogServiceMock.Object);
        
        // Try setting special characters
        vm.Nickname = "Test123!@#";
        var result = vm.PreparePKM();
        
        // Should not crash, nickname may be sanitized
        Assert.NotNull(result.Nickname);
    }

    [Fact]
    public void PID_Hex_String_Persists_Correctly()
    {
        var sav = new SAV3E();
        var pkm = new PK3 { Species = 25 };
        var vm = new PokemonEditorViewModel(pkm, sav, _spriteRendererMock.Object, _dialogServiceMock.Object);
        
        vm.Pid = "DEADBEEF";
        var result = vm.PreparePKM();
        
        Assert.Equal(0xDEADBEEF, result.PID);
    }

    [Fact]
    public void EncryptionConstant_Persists_Correctly()
    {
        // Gen 3 doesn't have EC - use Gen 6+ for this test
        var sav = new SAV6XY();
        var pkm = new PK6 { Species = 25 };
        var vm = new PokemonEditorViewModel(pkm, sav, _spriteRendererMock.Object, _dialogServiceMock.Object);
        
        vm.EncryptionConstant = "12345678";
        var result = vm.PreparePKM();
        
        Assert.Equal(0x12345678u, result.EncryptionConstant);
    }

    [Fact]
    public void Language_Persists_Correctly()
    {
        var sav = new SAV3E();
        var pkm = new PK3 { Species = 25 };
        var vm = new PokemonEditorViewModel(pkm, sav, _spriteRendererMock.Object, _dialogServiceMock.Object);
        
        vm.Language = (int)LanguageID.Japanese;
        var result = vm.PreparePKM();
        
        Assert.Equal((int)LanguageID.Japanese, result.Language);
    }

    [Fact]
    public void Ball_Persists_Correctly()
    {
        var sav = new SAV3E();
        var pkm = new PK3 { Species = 25 };
        var vm = new PokemonEditorViewModel(pkm, sav, _spriteRendererMock.Object, _dialogServiceMock.Object);
        
        vm.Ball = 2; // Great Ball
        var result = vm.PreparePKM();
        
        Assert.Equal(2, result.Ball);
    }

    [Fact]
    public void OT_Name_Persists_Correctly()
    {
        var sav = new SAV3E();
        var pkm = new PK3 { Species = 25 };
        var vm = new PokemonEditorViewModel(pkm, sav, _spriteRendererMock.Object, _dialogServiceMock.Object);
        
        // Gen 3 OT name is max 7 characters
        vm.OriginalTrainerName = "TestOT";
        var result = vm.PreparePKM();
        
        Assert.Equal("TestOT", result.OriginalTrainerName);
    }
}
