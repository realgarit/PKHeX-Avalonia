using Xunit;
using PKHeX.Core;
using PKHeX.Avalonia.ViewModels;
using PKHeX.Avalonia.Services;
using Moq;
using System.Linq;
using Avalonia.Media.Imaging;
using CommunityToolkit.Mvvm.Messaging;

namespace PKHeX.Avalonia.Tests;

public class DatabaseTests
{
    [Fact]
    public void Verify_Database_Species_Names()
    {
        // Initialize Core with English
        GameInfo.CurrentLanguage = "en";
        GameInfo.Strings = GameInfo.GetStrings("en");

        var sav = BlankSaveFile.Get(GameVersion.SL); // Scarlet
        var spriteMock = new Mock<ISpriteRenderer>();
        var dialogMock = new Mock<IDialogService>();
        
        var vm = new PKMDatabaseViewModel(sav, spriteMock.Object, dialogMock.Object);
        
        // Create a Gen 9 PKM (Gholdengo - Species 1000)
        var pk = new PK9 { Species = 1000 };
        var entry = new PKMDatabaseEntry(pk, spriteMock.Object);
        
        // Gholdengo is 1000.
        Assert.Equal("Gholdengo", entry.SpeciesName);
        
        // Change language to German
        GameInfo.CurrentLanguage = "de";
        GameInfo.Strings = GameInfo.GetStrings("de");
        
        // Entry should now reflect German name
        Assert.Equal("Monetigo", entry.SpeciesName); // Gholdengo in German is Monetigo
    }

    [Fact]
    public void Verify_Language_Refresh_PokemonEditor()
    {
        var sav = BlankSaveFile.Get(GameVersion.E);
        var spriteMock = new Mock<ISpriteRenderer>();
        var dialogMock = new Mock<IDialogService>();
        
        GameInfo.CurrentLanguage = "en";
        GameInfo.Strings = GameInfo.GetStrings("en");
        GameInfo.FilteredSources = new FilteredGameDataSource(sav, GameInfo.Sources);
        
        var vm = new PokemonEditorViewModel(sav.BlankPKM, sav, spriteMock.Object, dialogMock.Object);
        
        var engName = vm.SpeciesList.First(x => x.Value == 1).Text;
        Assert.Equal("Bulbasaur", engName);
        
        // Change language
        GameInfo.CurrentLanguage = "de"; // German
        GameInfo.Strings = GameInfo.GetStrings("de");
        GameInfo.FilteredSources = new FilteredGameDataSource(sav, GameInfo.Sources);
        
        // Now call RefreshLanguage (which MainWindowViewModel should do)
        vm.RefreshLanguage();
        
        // Current VM list should now be German
        var nameAfterChange = vm.SpeciesList.First(x => x.Value == 1).Text;
        Assert.Equal("Bisasam", nameAfterChange); // Bulbasaur in German is Bisasam
    }

    [Fact]
    public void Verify_Language_Refresh_InventoryEditor()
    {
        var sav = BlankSaveFile.Get(GameVersion.E);
        
        GameInfo.CurrentLanguage = "en";
        GameInfo.Strings = GameInfo.GetStrings("en");
        GameInfo.FilteredSources = new FilteredGameDataSource(sav, GameInfo.Sources);
        
        var vm = new InventoryEditorViewModel(sav);
        var pouch = vm.Pouches.First(p => p.PouchName == "Items");
        
        // Find Potion (ID 13)
        var potionNode = pouch.ItemList.FirstOrDefault(x => x.Value == 13);
        Assert.NotNull(potionNode);
        Assert.Equal("Potion", potionNode.Text);
        
        // Change to German
        GameInfo.CurrentLanguage = "de";
        GameInfo.Strings = GameInfo.GetStrings("de");
        GameInfo.FilteredSources = new FilteredGameDataSource(sav, GameInfo.Sources);
        
        // Refresh
        vm.RefreshLanguage();
        
        // Check Potion again
        var potionNodeDe = pouch.ItemList.FirstOrDefault(x => x.Value == 13);
        Assert.NotNull(potionNodeDe);
        Assert.Equal("Trank", potionNodeDe.Text); // Potion in German is Trank
    }

    [Fact]
    public void Verify_Database_Search_Populates_Correctly()
    {
        var sav = BlankSaveFile.Get(GameVersion.SL); // Scarlet (Gen 9)
        
        // Add a Pokemon to Box 1, Slot 1
        var pkm = sav.BlankPKM;
        pkm.Species = 25; // Pikachu
        pkm.CurrentLevel = 50;
        pkm.Nature = (Nature)3; // Adamant
        sav.SetBoxSlotAtIndex(pkm, 0, 0);
        
        // Slot 1 is EMPTY (Species 0)
        
        // Basic Search Settings
        var settings = new PKHeX.Core.Searching.SearchSettings
        {
            Species = 0, // Any
            Format = sav.Generation
        };
        
        var allPkms = sav.BoxData.Concat(sav.PartyData);
        var matches = settings.Search(allPkms).ToList();
        
        // Should find our Pikachu
        var pikachuMatch = matches.FirstOrDefault(p => p.Species == 25);
        Assert.NotNull(pikachuMatch);
        Assert.Equal(50, pikachuMatch.CurrentLevel);
        Assert.Equal((Nature)3, pikachuMatch.Nature);
        
        // Validate if it picked up empty slots
        var emptyMatches = matches.Where(p => p.Species == 0).ToList();
        
        // Search() raw returns empty slots. The ViewModel filters them matches.Where(p => p.Species != 0).
        // For this test, we verify that invalid slots exist in the raw return, explaining why the filter is needed.
        Assert.NotEmpty(emptyMatches);
        
        // Emulate ViewModel Filter
        var vmMatches = matches.Where(p => p.Species != 0).ToList();
        var emptyVmMatches = vmMatches.Where(p => p.Species == 0).ToList();
        Assert.Empty(emptyVmMatches);
        
        // --- Verify Specific Search ---
        var settingsSpecific = new PKHeX.Core.Searching.SearchSettings
        {
            Species = 25, // Pikachu
            Format = sav.Generation
        };
        var matchesSpecific = settingsSpecific.Search(allPkms).Where(p => p.Species != 0).ToList();
        Assert.Single(matchesSpecific);
        Assert.Equal(25, matchesSpecific[0].Species);

        // --- Verify Mismatch Search ---
        var settingsMismatch = new PKHeX.Core.Searching.SearchSettings
        {
            Species = 268, // Cascoon
            Format = sav.Generation
        };
        var matchesMismatch = settingsMismatch.Search(allPkms).Where(p => p.Species != 0).ToList();
        Assert.Empty(matchesMismatch); // Should not find Cascoon
        
        // Now simulate ViewModel entry creation
        var spriteMock = new Mock<ISpriteRenderer>();
        var entry = new PKMDatabaseEntry(pikachuMatch, spriteMock.Object);
        
        // Verify entry properties
        GameInfo.CurrentLanguage = "en";
        GameInfo.Strings = GameInfo.GetStrings("en");
        
        Assert.Equal("Pikachu", entry.SpeciesName);
        Assert.Equal("50", entry.Level);
        Assert.Contains("Adamant", entry.NatureName);
    }

    [Fact]
    public void Verify_Database_Reacts_To_Language_Message()
    {
        var sav = BlankSaveFile.Get(GameVersion.E);
        var spriteMock = new Mock<ISpriteRenderer>();
        var dialogMock = new Mock<IDialogService>();
        
        // Setup English
        GameInfo.CurrentLanguage = "en";
        GameInfo.Strings = GameInfo.GetStrings("en");
        
        var vm = new PKMDatabaseViewModel(sav, spriteMock.Object, dialogMock.Object);
        string initialText = vm.SpeciesList.First(x => x.Value == 1).Text;
        Assert.Equal("Bulbasaur", initialText);
        
        // Change Global State to German
        GameInfo.CurrentLanguage = "de";
        GameInfo.Strings = GameInfo.GetStrings("de");
        GameInfo.FilteredSources = new FilteredGameDataSource(sav, GameInfo.Sources); // Sources updates internally? 
        // Note: GameInfo.Sources usually uses GameInfo.Strings. So updating Strings is enough for standard Sources access?
        // GameInfo.Sources is a static property returning a GameStringSource using GameInfo.Strings.
        
        // Send Message
        WeakReferenceMessenger.Default.Send(new LanguageChangedMessage("de"));
        
        // Check if VM updated
        string newText = vm.SpeciesList.First(x => x.Value == 1).Text;
        Assert.Equal("Bisasam", newText);
    }

    [Fact]
    public void Verify_Database_Search_Cascoon()
    {
        var sav = BlankSaveFile.Get(GameVersion.SL); // Gen 9 Scarlet
        
        // Add Cascoon (268) to Box 1, Slot 1
        var pkm = sav.BlankPKM;
        pkm.Species = 268; // Cascoon
        pkm.CurrentLevel = 15;
        pkm.Nature = (Nature)1; // Lonely
        
        // Use clone to break reference issues
        var clone = pkm.Clone();
        sav.SetBoxSlotAtIndex(clone, 0, 0); // Box 1, Slot 1
        
        // Verify Persistence immediately
        var loaded = sav.GetBoxSlotAtIndex(0,0);
        Assert.Equal((Nature)1, loaded.Nature);
        
    }

    [Fact]
    public void Verify_Search_Wildcards()
    {
        var sav = BlankSaveFile.Get(GameVersion.SL);
        var pkm = sav.BlankPKM;
        pkm.Species = 268; // Cascoon
        pkm.Nature = (Nature)1; // Lonely
        pkm.Ability = 50; 
        pkm.HeldItem = 10; // Oran Berry
        
        sav.SetBoxSlotAtIndex(pkm, 0, 0);
        var allPkms = sav.BoxData.Concat(sav.PartyData);

        // Test Nature=25 (Random = Any wildcard)
        var setNatureRandom = new PKHeX.Core.Searching.SearchSettings { Species = 0, Format = sav.Generation, Nature = Nature.Random };
        var matchNatureRandom = setNatureRandom.Search(allPkms).Where(p => p.Species != 0).Count();
        
        // Test Ability=-1 (Any wildcard)
        var setAbilityWild = new PKHeX.Core.Searching.SearchSettings { Species = 0, Format = sav.Generation, Ability = -1 };
        var matchAbilityWild = setAbilityWild.Search(allPkms).Where(p => p.Species != 0).Count();
        
        // Test Item=-1 (Any wildcard)
        var setItemWild = new PKHeX.Core.Searching.SearchSettings { Species = 0, Format = sav.Generation, Item = -1 };
        var matchItemWild = setItemWild.Search(allPkms).Where(p => p.Species != 0).Count();

        // All wildcards should match our Cascoon
        Assert.True(matchNatureRandom == 1, $"Nature.Random (25) failed. Count: {matchNatureRandom}");
        Assert.True(matchAbilityWild == 1, $"Ability -1 failed. Count: {matchAbilityWild}");
        Assert.True(matchItemWild == 1, $"Item -1 failed. Count: {matchItemWild}");
    }

    [Fact]
    public void Verify_French_Language()
    {
        // Try to load French strings
        var strings = GameInfo.GetStrings("fr");
        Assert.NotNull(strings);
        Assert.NotEmpty(strings.Species);
        
        // Check "Bulbasaur" in French -> "Bulbizarre"
        // Note: Index 1 is Bulbasaur
        var name = strings.Species[1];
        Assert.Equal("Bulbizarre", name);
    }
}
