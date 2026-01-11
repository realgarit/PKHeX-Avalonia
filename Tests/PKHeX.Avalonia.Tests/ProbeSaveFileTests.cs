
using System;
using System.Linq;
using System.Reflection;
using Avalonia.Headless.XUnit;
using PKHeX.Core;
using Xunit;

namespace PKHeX.Avalonia.Tests;

public class ProbeSaveFileTests
{
    [AvaloniaFact]
    public void Inspect_SaveFile_Properties()
    {
        var sav = new SAV3E(); // Using Gen 3 Emerald save as baseline
        var props = typeof(SaveFile).GetProperties(BindingFlags.Public | BindingFlags.Instance);
        
        using var writer = new System.IO.StreamWriter("/Users/realgar/Git/PKHeX-Avalonia/probe_output.txt");
        writer.WriteLine("--- SAVEFILE PROPERTIES ---");
        foreach (var p in props.OrderBy(x => x.Name))
        {
            writer.WriteLine($"{p.Name} ({p.PropertyType.Name})");
        }
        writer.WriteLine("--- END PROPERTIES ---");

        // Specific checks for things we care about
        var subProps = typeof(SAV3E).GetProperties(BindingFlags.Public | BindingFlags.Instance);
        writer.WriteLine("--- SAV3E SPECIFIC ---");
        foreach (var p in subProps.OrderBy(x => x.Name))
        {
            if (props.All(baseP => baseP.Name != p.Name)) // Only show new/derived properties
                writer.WriteLine($"{p.Name} ({p.PropertyType.Name})");
        }
    }
}
