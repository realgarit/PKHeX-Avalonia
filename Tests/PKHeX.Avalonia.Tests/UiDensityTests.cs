namespace PKHeX.Avalonia.Tests;

public class UiDensityTests
{
    [Fact]
    public void Theme_UsesCompactControlPadding()
    {
        var theme = ReadSourceFile("Styles", "Theme.axaml");

        Assert.Contains("<Setter Property=\"Padding\" Value=\"12,8\" />", theme);
        Assert.Contains("<Setter Property=\"Padding\" Value=\"8\" />", theme);
    }

    [Fact]
    public void MainWindow_UsesCompactEditorColumnWidth()
    {
        var mainWindow = ReadSourceFile("Views", "MainWindow.axaml");

        Assert.Contains("ColumnDefinitions=\"520,*\"", mainWindow);
    }

    [Fact]
    public void PokemonEditor_UsesCompactContentSpacingAndMargin()
    {
        var pokemonEditor = ReadSourceFile("Views", "PokemonEditor.axaml");

        Assert.Contains("<StackPanel Spacing=\"12\" Margin=\"6,6,6,64\">", pokemonEditor);
    }

    private static string ReadSourceFile(params string[] relativePath)
    {
        var path = Path.Combine([FindRepoRoot(), "PKHeX.Avalonia", .. relativePath]);
        Assert.True(File.Exists(path), $"Source file not found: {path}");
        return File.ReadAllText(path);
    }

    private static string FindRepoRoot()
    {
        var dir = AppDomain.CurrentDomain.BaseDirectory;
        while (!Directory.Exists(Path.Combine(dir, "PKHeX.Avalonia")))
        {
            var parent = Directory.GetParent(dir);
            if (parent == null)
                throw new DirectoryNotFoundException("Could not find repository root");
            dir = parent.FullName;
        }

        return dir;
    }
}
