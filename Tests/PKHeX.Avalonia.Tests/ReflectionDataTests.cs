using System.Reflection;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Moq;
using PKHeX.Avalonia.Services;
using PKHeX.Avalonia.ViewModels;
using PKHeX.Core;
using Xunit;

namespace PKHeX.Avalonia.Tests;

public class ReflectionDataTests
{
    private readonly Mock<ISpriteRenderer> _spriteRendererMock;
    private readonly Mock<IDialogService> _dialogServiceMock;
    private readonly SaveFile _saveFile;

    public ReflectionDataTests()
    {
        _spriteRendererMock = new Mock<ISpriteRenderer>();
        _dialogServiceMock = new Mock<IDialogService>();
        _saveFile = new SAV3E(); 
    }

    [AvaloniaFact]
    public void RoundTrip_All_Int_Properties()
    {
        // Setup
        var pkm = new PK3(); 
        var vm = new PokemonEditorViewModel(pkm, _saveFile, _spriteRendererMock.Object, _dialogServiceMock.Object);
        
        // Strategy: 
        // 1. Get all writable public int properties on the ViewModel
        // 2. Set them to a non-default value (e.g. 1, 2, 100)
        // 3. Call PreparePKM()
        // 4. Verify that the PKM has changed (we assume the VM property setup the mapping correctly)
        // 5. Create a NEW VM from that PKM and verify values persist
        
        // This list controls which exact properties we expect to survive a round trip "losslessly" via just the viewmodel
        // Some properties like mapped stats might be ReadOnly, so we filter for CanWrite
        
        var properties = typeof(PokemonEditorViewModel)
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.PropertyType == typeof(int) && p.CanWrite && p.CanRead)
            .ToList();

        // Exclusion list (properties that aren't direct data mappings or have side effects like indexes)
        var exclusions = new HashSet<string> 
        { 
            "SelectedTab", "Stat_HP", "Stat_ATK", "Stat_DEF", "Stat_SPA", "Stat_SPD", "Stat_SPE", // Computed
            "Species", "Form", "Ability", // These trigger structure changes, tested separately
            "Level", // Affects stats, tested separately or handled carefully
            "TargetPKM", // Not an int obviously, but just safety
            "Nature", // Gen 3 Nature is tied to PID, setting it might not persist if PID logic overrides
            "Gender", // Dependent on Species (ratio), can't set arbitrarily
            "EggLocation", "MetLocation", "MetLevel", // Context sensitive (Gen 3 limitations, Egg vs Met)
            "OriginalTrainerGender", // Might have constraints
            "Ball", // Sometimes driven by event constants or limitations
            "RelearnMove1", "RelearnMove2", "RelearnMove3", "RelearnMove4", // Gen 3 doesn't persist these
            // New computed/derived properties (Read-only or reset by PKM)
            "StatHPCurrent", "StatHPMax", "Valid", "Version", "StatNature", "HpType",
            "IsPokerusInfected", "IsPokerusCured", "AbilityNumber", "Id32", "IsNicknamed",
            "StatusCondition", "HandlingTrainerName", "HandlingTrainerGender", 
            "HandlingTrainerFriendship", "CurrentHandler", "OriginalTrainerFriendship"
        };

        foreach (var prop in properties)
        {
            if (exclusions.Contains(prop.Name)) continue;

            // SPECIAL LOGIC: Valid ranges
            int testValue = 1;
            if (prop.Name.StartsWith("Iv")) testValue = 31;
            if (prop.Name.StartsWith("Ev")) testValue = 252;
            if (prop.Name.Contains("PpUps")) testValue = 3;
            if (prop.Name.Contains("Friendship") || prop.Name.Contains("Happiness")) testValue = 200;
            if (prop.Name.Contains("Sid") || prop.Name.Contains("TrainerID")) testValue = 12345;
            if (prop.Name.Contains("Move") && !prop.Name.Contains("Pp")) testValue = 33; // Tackle

            // SET
            try 
            {
                prop.SetValue(vm, testValue);
            }
            catch
            {
                // If setting fails (validation), fail test
                Assert.Fail($"Failed to set property {prop.Name}");
            }
        }

        // COMIT
        var newPkm = vm.PreparePKM();

        // RELOAD
        var newVm = new PokemonEditorViewModel(newPkm, _saveFile, _spriteRendererMock.Object, _dialogServiceMock.Object);

        // VERIFY
        foreach (var prop in properties)
        {
            if (exclusions.Contains(prop.Name)) continue;

             int testValue = 1;
            if (prop.Name.StartsWith("Iv")) testValue = 31;
            if (prop.Name.StartsWith("Ev")) testValue = 252;
            if (prop.Name.Contains("PpUps")) testValue = 3;
            if (prop.Name.Contains("Friendship") || prop.Name.Contains("Happiness")) testValue = 200;
            if (prop.Name.Contains("Sid") || prop.Name.Contains("TrainerID")) testValue = 12345;
            if (prop.Name.Contains("Move") && !prop.Name.Contains("Pp")) testValue = 33; 

            var actual = (int)prop.GetValue(newVm)!;
            
            // Assert
            Assert.True(actual == testValue, 
                $"Property {prop.Name} failed round-trip. Expected {testValue}, got {actual}");
        }
    }
}
