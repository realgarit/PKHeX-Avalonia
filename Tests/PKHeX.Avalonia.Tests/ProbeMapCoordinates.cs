
using System;
using System.IO;
using System.Linq;
using System.Reflection;
using Avalonia.Headless.XUnit;
using PKHeX.Core;
using Xunit;

namespace PKHeX.Avalonia.Tests;

public class ProbeMapCoordinates
{
    [AvaloniaFact]
    public void Inspect_Coordinates()
    {
        var outputDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "TestResults");
        Directory.CreateDirectory(outputDir);
        using var writer = new System.IO.StreamWriter(Path.Combine(outputDir, "probe_coords.txt"));
        
        var types = new Type[] { typeof(SAV3E), typeof(SAV4), typeof(SAV5), typeof(SAV7), typeof(SAV8SWSH), typeof(SAV9SV) };

        foreach (var type in types)
        {
            writer.WriteLine($"--- {type.Name} ---");
            var props = type.GetProperties(BindingFlags.Public | BindingFlags.Instance);
            
            foreach (var p in props)
            {
                if (IsCoordRelated(p.Name))
                {
                   writer.WriteLine($"{p.Name} ({p.PropertyType.Name})"); 
                }
            }
        }
    }

    private bool IsCoordRelated(string name)
    {
        name = name.ToLower();
        return name.Contains("map") || name.Contains("coord") || name.Contains("spawn") || 
               name == "x" || name == "y" || name == "z";
    }
}
