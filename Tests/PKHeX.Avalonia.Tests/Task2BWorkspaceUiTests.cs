using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text.Json;

namespace PKHeX.Avalonia.Tests;

/// <summary>
/// Static acceptance coverage for the Task 2B workspace affordances. The headless test backend can
/// verify the XAML/code contract and localization catalogs, while the real desktop double-click and
/// native tool-window lifetime remain an explicit native-platform test boundary.
/// </summary>
public sealed class Task2BWorkspaceUiTests
{
    [Fact]
    public void MainWindow_Offers_TabDoubleClick_AndLocalizedAccessibleWorkspaceCommands()
    {
        var mainWindow = ReadRepositoryFile("PKHeX.Avalonia/Views/MainWindow.axaml");
        var codeBehind = ReadRepositoryFile("PKHeX.Avalonia/Views/MainWindow.axaml.cs");
        var boxViewer = ReadRepositoryFile("PKHeX.Avalonia/Views/BoxViewer.axaml");
        var partyViewer = ReadRepositoryFile("PKHeX.Avalonia/Views/PartyViewer.axaml");

        Assert.Contains("DoubleTapped=\"OnBoxTabDoubleTapped\"", mainWindow);
        Assert.Contains("DoubleTapped=\"OnPartyTabDoubleTapped\"", mainWindow);
        Assert.DoesNotContain(" Tapped=\"OnBoxTabDoubleTapped\"", mainWindow);
        Assert.DoesNotContain(" Tapped=\"OnPartyTabDoubleTapped\"", mainWindow);
        Assert.Contains("Command=\"{Binding OpenBoxWorkspaceCommand}\"", mainWindow);
        Assert.Contains("Command=\"{Binding OpenPartyWorkspaceCommand}\"", mainWindow);
        Assert.Contains("AutomationProperties.Name=\"{loc:Loc Menu_Tools_OpenBoxWorkspace}\"", mainWindow);
        Assert.Contains("AutomationProperties.Name=\"{loc:Loc Menu_Tools_OpenPartyWorkspace}\"", mainWindow);

        Assert.Contains("OpenBoxWorkspaceCommand.Execute(null)", codeBehind);
        Assert.Contains("OpenPartyWorkspaceCommand.Execute(null)", codeBehind);
        Assert.Contains("IsInsideViewer<BoxViewer>(e.Source)", codeBehind);
        Assert.Contains("IsInsideViewer<PartyViewer>(e.Source)", codeBehind);
        Assert.Contains("for (var visual = source as Visual; visual is not null; visual = visual.GetVisualParent())", codeBehind);
        Assert.Contains("if (visual is TViewer)", codeBehind);

        // The tab-level double-tap handlers must not replace the existing slot-level gesture.
        Assert.Contains("DoubleTapped=\"OnSlotDoubleTapped\"", boxViewer);
        Assert.Contains("DoubleTapped=\"OnSlotDoubleTapped\"", partyViewer);

        var localizationDirectory = Path.Combine(FindRepositoryRoot(), "PKHeX.Presentation", "Localization", "Strings");
        var expectedCatalogs = new[]
        {
            "de.json", "en.json", "es.json", "fr.json", "it.json", "ja.json", "ko.json", "zh-Hans.json", "zh-Hant.json",
        };
        var actualCatalogs = Directory.GetFiles(localizationDirectory, "*.json")
            .Select(Path.GetFileName)
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToArray();

        Assert.Equal(expectedCatalogs.OrderBy(name => name, StringComparer.Ordinal), actualCatalogs);

        foreach (var catalog in actualCatalogs)
        {
            using var document = JsonDocument.Parse(File.ReadAllText(Path.Combine(localizationDirectory, catalog!)));
            var root = document.RootElement;
            AssertLocalized(root, "Menu_Tools_OpenBoxWorkspace", catalog!);
            AssertLocalized(root, "Menu_Tools_OpenPartyWorkspace", catalog!);
        }
    }

    private static void AssertLocalized(JsonElement root, string key, string catalog)
    {
        Assert.True(root.TryGetProperty(key, out var value), $"{catalog} is missing {key}");
        Assert.False(string.IsNullOrWhiteSpace(value.GetString()), $"{catalog} has an empty {key}");
    }

    private static string ReadRepositoryFile(string relativePath) =>
        File.ReadAllText(Path.Combine(FindRepositoryRoot(), relativePath));

    private static string FindRepositoryRoot([CallerFilePath] string sourceFilePath = "")
    {
        var testProjectDirectory = Directory.GetParent(sourceFilePath)?.FullName;
        var testsDirectory = testProjectDirectory is null ? null : Directory.GetParent(testProjectDirectory)?.FullName;
        var repoRoot = testsDirectory is null ? null : Directory.GetParent(testsDirectory)?.FullName;
        if (repoRoot is not null
            && File.Exists(Path.Combine(repoRoot, "PKHeX.Avalonia", "Views", "MainWindow.axaml")))
            return repoRoot;

        throw new DirectoryNotFoundException("Could not find repository root.");
    }
}
