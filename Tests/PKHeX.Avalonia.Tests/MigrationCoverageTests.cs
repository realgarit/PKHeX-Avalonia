using System.Reflection;
using System.Text.RegularExpressions;
using PKHeX.Avalonia.ViewModels;
using PKHeX.Core;
using Xunit;
using Xunit.Abstractions;

namespace PKHeX.Avalonia.Tests;

public class MigrationCoverageTests
{
    private readonly ITestOutputHelper _output;

    public MigrationCoverageTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    public void Verify_WinForms_Feature_Parity()
    {
        // 1. Identify Candidate Features (Public Properties of PKM)
        var pkmProperties = typeof(PKM).GetProperties(BindingFlags.Public | BindingFlags.Instance)
                                        .Where(p => p.CanWrite && p.CanRead) // Editable properties
                                        .Select(p => p.Name)
                                        .ToHashSet();

        // 2. Discover "Active" Features in WinForms (Scanning Source Code)
        // Adjust path to point to the repository root's PKHeX.WinForms
        var repoRoot = FindRepoRoot();
        var winFormsPath = Path.Combine(repoRoot, "PKHeX.WinForms"); 
        
        Assert.True(Directory.Exists(winFormsPath), $"Could not find PKHeX.WinForms at {winFormsPath}");

        var usedInWinForms = ScanForPropertyUsage(winFormsPath, pkmProperties);

        // 3. Inspect Avalonia Implementation
        var avaloniaProperties = typeof(PokemonEditorViewModel).GetProperties(BindingFlags.Public | BindingFlags.Instance)
                                                               .Select(p => p.Name)
                                                               .ToHashSet();

        // 4. Gap Analysis
        var missingFeatures = new List<string>();
        foreach (var prop in usedInWinForms)
        {
            // Smart Matching Strategy
            // 1. Direct Match
            if (avaloniaProperties.Contains(prop)) continue;

            // 2. Fuzzy Match (Case-insensitive, ignore underscores)
            // e.g. "IV_HP" -> "ivhp" == "IvHP" -> "ivhp"
            var propNormalized = prop.Replace("_", "").ToLowerInvariant();
            if (avaloniaProperties.Any(p => p.Replace("_", "").ToLowerInvariant() == propNormalized)) 
                continue;

            // 3. Known Aliases (Manual mapping for drastic renames)
            var aliases = new Dictionary<string, string> 
            {
                { "EXP", "Exp" },
                { "CurrentFriendship", "Happiness" },
                { "FatefulEncounter", "IsFatefulEncounter" },
                { "TID16", "TrainerID" }, 
                { "SID16", "Sid" },
                { "EggMetDate", "EggDate" },
                { "MetDate", "MetDate" }, // Already exists but just in case
                { "CurrentLevel", "Level" },
                { "Stat_Level", "Level" },
                { "Move1_PP", "Pp1" }, { "Move2_PP", "Pp2" }, { "Move3_PP", "Pp3" }, { "Move4_PP", "Pp4" },
                { "Move1_PPUps", "PpUps1" }, { "Move2_PPUps", "PpUps2" }, { "Move3_PPUps", "PpUps3" }, { "Move4_PPUps", "PpUps4" },
                { "PokerusStrain", "PkrsStrain" },
                { "PokerusDays", "PkrsDays" },
                { "OriginalTrainerName", "OriginalTrainerName" },
                { "OriginalTrainerGender", "OriginalTrainerGender" },
                { "DisplayTID", "TrainerID" },
                { "DisplaySID", "Sid" },
                // Aggregate properties mapped to individual fields
                { "IVs", "IvHP" }, // Arrays are covered by IV_HP..IV_SPE
                { "Moves", "Move1" } // Arrays are covered by Move1..Move4
            };
            if (aliases.ContainsKey(prop) && avaloniaProperties.Contains(aliases[prop]))
                continue;

            missingFeatures.Add(prop);
        }

        // 5. Reporting
        _output.WriteLine("=== MIGRATION GAP REPORT ===");
        _output.WriteLine($"PKM Properties: {pkmProperties.Count}");
        _output.WriteLine($"Used in WinForms: {usedInWinForms.Count}");
        _output.WriteLine($"Implemented in Avalonia: {avaloniaProperties.Count}");
        _output.WriteLine($"MISSING FEATURES: {missingFeatures.Count}");
        _output.WriteLine("============================");

        foreach (var missing in missingFeatures.OrderBy(x => x))
        {
            _output.WriteLine($"[MISSING] {missing}");
        }

        // Generate a physical report file for the agent to read easily
        var reportPath = Path.Combine(repoRoot, "migration_gap_report.txt");
        File.WriteAllLines(reportPath, missingFeatures.Select(f => $"[MISSING] {f}"));

        // FAIL the test if there are gaps, to drive the "Loop"
        Assert.Empty(missingFeatures);
    }

    private string FindRepoRoot()
    {
        // Start from the location of the test assembly and walk up
        var dir = AppDomain.CurrentDomain.BaseDirectory;
        while (!Directory.Exists(Path.Combine(dir, "PKHeX.WinForms")))
        {
            var parent = Directory.GetParent(dir);
            if (parent == null) throw new DirectoryNotFoundException("Could not find repository root containing PKHeX.WinForms");
            dir = parent.FullName;
        }
        return dir;
    }

    private HashSet<string> ScanForPropertyUsage(string rootPath, HashSet<string> candidates)
    {
        var used = new HashSet<string>();
        var files = Directory.EnumerateFiles(rootPath, "*.cs", SearchOption.AllDirectories);

        // Pre-compile Regexes for performance? Or just simple string contains for speed first.
        // False positives are okay (better to implement too much than too little), 
        // False negatives are bad.
        // "pkm.SoAndSo" or "PKM.SoAndSo" or "sav.SoAndSo"
        
        // This is a slow operation, but fine for a test suite running explicitly
        foreach (var file in files)
        {
            var content = File.ReadAllText(file);
            foreach (var prop in candidates)
            {
                if (used.Contains(prop)) continue;

                // Simple heuristic: Look for ".PropertyName" usage
                // We verify it's a property access, not just the word appearing in a comment or string (mostly)
                // A reliable pattern is `\.{prop}\b` or ` {prop} =` (object initializer)
                if (Regex.IsMatch(content, $@"\.{prop}\b"))
                {
                    used.Add(prop);
                }
            }
        }
        return used;
    }
}
