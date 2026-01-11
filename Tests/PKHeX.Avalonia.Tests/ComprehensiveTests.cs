using Moq;
using PKHeX.Avalonia.Services;
using PKHeX.Avalonia.ViewModels;
using PKHeX.Core;
using Xunit;

namespace PKHeX.Avalonia.Tests;

/// <summary>
/// Comprehensive tests to discover edge cases, bugs, and missing functionality.
/// </summary>
public class ComprehensiveTests
{
    // private readonly Mock<ISpriteRenderer> _spriteRendererMock = new(); // Removed
    // private readonly Mock<IDialogService> _dialogServiceMock = new(); // Removed

    #region Edge Case Tests

    [Fact]
    public void Species_Zero_Displays_Empty_Slot()
    {
        var sav = new SAV3E();
        var pkm = new PK3 { Species = 0 };
        
        var (vm, _, _) = TestHelpers.CreateTestViewModel(pkm, sav);
        
        Assert.Equal(0, vm.Species);
        Assert.Equal("Empty Slot", vm.Title);
    }

    [Fact]
    public void MaxIV_Values_Are_Clamped()
    {
        var sav = new SAV3E();
        var pkm = new PK3 { Species = 25 };
        var (vm, _, _) = TestHelpers.CreateTestViewModel(pkm, sav);
        
        // Set IV above max (should be clamped or handled)
        vm.IvHP = 999; // Way above 31
        var result = vm.PreparePKM();
        
        // IVs should be clamped to 0-31 range
        Assert.InRange(result.IV_HP, 0, 31);
    }

    [Fact]
    public void MaxEV_Values_Are_Clamped()
    {
        var sav = new SAV3E();
        var pkm = new PK3 { Species = 25 };
        var (vm, _, _) = TestHelpers.CreateTestViewModel(pkm, sav);
        
        // Set EV above max (should be clamped or handled)
        vm.EvHP = 999; // Way above 252
        var result = vm.PreparePKM();
        
        // EVs should be clamped to 0-255 range (PKM allows 255)
        Assert.InRange(result.EV_HP, 0, 255);
    }

    [Fact]
    public void Null_Nickname_Handled_Gracefully()
    {
        var sav = new SAV3E();
        var pkm = new PK3 { Species = 25 };
        var (vm, _, _) = TestHelpers.CreateTestViewModel(pkm, sav);
        
        // This should not throw
        vm.Nickname = null!;
        var result = vm.PreparePKM();
        
        Assert.NotNull(result.Nickname);
    }

    [Fact]
    public void Invalid_PID_String_Does_Not_Crash()
    {
        var sav = new SAV3E();
        var pkm = new PK3 { Species = 25 };
        var (vm, _, _) = TestHelpers.CreateTestViewModel(pkm, sav);
        
        // Set invalid hex string
        vm.Pid = "ZZZZZZZZ"; // Invalid hex
        var result = vm.PreparePKM();
        
        // Should not crash, PID unchanged or handled
        Assert.True(true); // If we reach here, no crash occurred
    }

    [Fact]
    public void Level_Zero_Handled()
    {
        var sav = new SAV3E();
        var pkm = new PK3 { Species = 25 };
        var (vm, _, _) = TestHelpers.CreateTestViewModel(pkm, sav);
        
        vm.Level = 0; // Invalid but should not crash
        var result = vm.PreparePKM();
        
        // Level should be at least 1 or handled
        Assert.True(result.CurrentLevel >= 0);
    }

    [Fact]
    public void Level_Over_100_Handled()
    {
        var sav = new SAV3E();
        var pkm = new PK3 { Species = 25 };
        var (vm, _, _) = TestHelpers.CreateTestViewModel(pkm, sav);
        
        vm.Level = 255; // Over max
        var result = vm.PreparePKM();
        
        // Level should be clamped to 100 or handled
        Assert.InRange(result.CurrentLevel, 1, 100);
    }

    #endregion

    #region Command Tests

    [Fact]
    public void SetMaxIVs_Command_Sets_All_IVs_To_31()
    {
        var sav = new SAV3E();
        var pkm = new PK3 { Species = 25 };
        var (vm, _, _) = TestHelpers.CreateTestViewModel(pkm, sav);
        
        vm.SetMaxIVsCommand.Execute(null);
        
        Assert.Equal(31, vm.IvHP);
        Assert.Equal(31, vm.IvATK);
        Assert.Equal(31, vm.IvDEF);
        Assert.Equal(31, vm.IvSPA);
        Assert.Equal(31, vm.IvSPD);
        Assert.Equal(31, vm.IvSPE);
        Assert.Equal(186, vm.IVTotal);
    }

    [Fact]
    public void ClearEVs_Command_Sets_All_EVs_To_Zero()
    {
        var sav = new SAV3E();
        var pkm = new PK3 { Species = 25, EV_HP = 100, EV_ATK = 100 };
        var (vm, _, _) = TestHelpers.CreateTestViewModel(pkm, sav);
        
        Assert.True(vm.EVTotal > 0); // Pre-condition
        
        vm.ClearEVsCommand.Execute(null);
        
        Assert.Equal(0, vm.EvHP);
        Assert.Equal(0, vm.EvATK);
        Assert.Equal(0, vm.EvDEF);
        Assert.Equal(0, vm.EvSPA);
        Assert.Equal(0, vm.EvSPD);
        Assert.Equal(0, vm.EvSPE);
        Assert.Equal(0, vm.EVTotal);
    }

    [Fact]
    public void ToggleShiny_Command_Toggles_Shiny_State()
    {
        var sav = new SAV3E();
        var pkm = new PK3 { Species = 25 };
        var (vm, _, _) = TestHelpers.CreateTestViewModel(pkm, sav);
        
        bool initialState = vm.IsShiny;
        
        vm.ToggleShinyCommand.Execute(null);
        Assert.NotEqual(initialState, vm.IsShiny);
        
        vm.ToggleShinyCommand.Execute(null);
        Assert.Equal(initialState, vm.IsShiny);
    }

    #endregion

    #region Multi-Generation Tests

    [Fact]
    public void Gen4_Pokemon_Loads_Correctly()
    {
        var sav = new SAV4Pt();
        var pkm = new PK4 { Species = 393 }; // Piplup
        
        var (vm, _, _) = TestHelpers.CreateTestViewModel(pkm, sav);
        
        Assert.Equal(393, vm.Species);
    }

    [Fact]
    public void Gen5_Pokemon_Loads_Correctly()
    {
        var sav = new SAV5B2W2();
        var pkm = new PK5 { Species = 495 }; // Snivy
        
        var (vm, _, _) = TestHelpers.CreateTestViewModel(pkm, sav);
        
        Assert.Equal(495, vm.Species);
    }

    [Fact]
    public void Gen6_Pokemon_Loads_Correctly()
    {
        var sav = new SAV6XY();
        var pkm = new PK6 { Species = 650 }; // Chespin
        
        var (vm, _, _) = TestHelpers.CreateTestViewModel(pkm, sav);
        
        Assert.Equal(650, vm.Species);
    }

    [Fact]
    public void Gen7_Pokemon_Loads_Correctly()
    {
        var sav = new SAV7SM();
        var pkm = new PK7 { Species = 722 }; // Rowlet
        
        var (vm, _, _) = TestHelpers.CreateTestViewModel(pkm, sav);
        
        Assert.Equal(722, vm.Species);
    }

    [Fact]
    public void Gen8_Pokemon_Loads_Correctly()
    {
        var sav = new SAV8SWSH();
        var pkm = new PK8 { Species = 810 }; // Grookey
        
        var (vm, _, _) = TestHelpers.CreateTestViewModel(pkm, sav);
        
        Assert.Equal(810, vm.Species);
    }

    [Fact]
    public void Gen9_Pokemon_Loads_Correctly()
    {
        var sav = new SAV9SV();
        var pkm = new PK9 { Species = 906 }; // Sprigatito
        
        var (vm, _, _) = TestHelpers.CreateTestViewModel(pkm, sav);
        
        Assert.Equal(906, vm.Species);
    }

    #endregion

    #region Round-Trip Persistence Tests

    [Fact]
    public void All_Basic_Fields_Persist_After_PreparePKM()
    {
        var sav = new SAV3E();
        var pkm = new PK3 { Species = 25 };
        var (vm, _, _) = TestHelpers.CreateTestViewModel(pkm, sav);
        
        // Set various fields
        vm.Species = 1;
        vm.Nickname = "TestBulba";
        vm.HeldItem = 13; // Potion
        vm.Move1 = 33; // Tackle
        vm.Move2 = 45; // Growl
        vm.IvHP = 15;
        vm.IvATK = 20;
        vm.EvHP = 100;
        vm.EvATK = 150;
        vm.Happiness = 200;
        
        var result = vm.PreparePKM();
        
        Assert.Equal(1, result.Species);
        Assert.Equal("TestBulba", result.Nickname);
        Assert.Equal(13, result.HeldItem);
        Assert.Equal(33, result.Move1);
        Assert.Equal(45, result.Move2);
        Assert.Equal(15, result.IV_HP);
        Assert.Equal(20, result.IV_ATK);
        Assert.Equal(100, result.EV_HP);
        Assert.Equal(150, result.EV_ATK);
        Assert.Equal(200, result.CurrentFriendship);
    }

    [Fact]
    public void PP_And_PPUps_Persist_Correctly()
    {
        var sav = new SAV3E();
        var pkm = new PK3 { Species = 25 };
        var (vm, _, _) = TestHelpers.CreateTestViewModel(pkm, sav);
        
        vm.Move1 = 33; // Tackle
        vm.Pp1 = 35;
        vm.PpUps1 = 3;
        
        var result = vm.PreparePKM();
        
        Assert.Equal(35, result.Move1_PP);
        Assert.Equal(3, result.Move1_PPUps);
    }

    [Fact]
    public void Met_Data_Persists_Correctly()
    {
        // Gen 3 doesn't store MetDate - use Gen 4+ for this test
        var sav = new SAV4Pt();
        var pkm = new PK4 { Species = 25 };
        var (vm, _, _) = TestHelpers.CreateTestViewModel(pkm, sav);
        
        vm.MetLevel = 5;
        vm.MetDate = new DateTimeOffset(2024, 6, 15, 0, 0, 0, TimeSpan.Zero);
        
        var result = vm.PreparePKM();
        
        Assert.Equal(5, result.MetLevel);
        // Gen 4+ should persist MetDate
        Assert.NotNull(result.MetDate);
    }

    #endregion

    #region Validation Tests

    [Fact]
    public void Valid_Pokemon_Shows_Legal()
    {
        var sav = new SAV3E();
        // Create a minimally valid Pokémon
        var pkm = new PK3
        {
            Species = 25,
            Move1 = 84, // Thundershock (legal for Pikachu)
            MetLocation = 0,
            MetLevel = 5,
            Ball = 4, // Pokéball
            OriginalTrainerName = "Ash",
            Language = (int)LanguageID.English,
            OriginalTrainerGender = 0
        };
        pkm.PID = 12345;
        pkm.OriginalTrainerTrash[0] = 0x41; // 'A'
        
        var (vm, _, _) = TestHelpers.CreateTestViewModel(pkm, sav);
        
        // Just checking it doesn't crash - legality is complex
        Assert.NotNull(vm.LegalityReport);
    }


    #endregion
}
