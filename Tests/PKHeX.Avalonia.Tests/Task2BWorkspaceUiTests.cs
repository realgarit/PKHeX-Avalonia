using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text.Json;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.VisualTree;
using PKHeX.Avalonia.Controls;
using PKHeX.Avalonia.Tests.Harness;
using PKHeX.Core;

namespace PKHeX.Avalonia.Tests;

/// <summary>
/// Headless acceptance coverage for the Task 2B workspace affordances. Native desktop tool-window
/// lifetime remains a platform test boundary, but routed header gestures and command wiring are
/// exercised through the fully composed MainWindow.
/// </summary>
public sealed class Task2BWorkspaceUiTests
{
    [Fact]
    public void MainWindow_Offers_TabDoubleClick_AndLocalizedAccessibleWorkspaceCommands()
    {
        var mainWindow = ReadRepositoryFile("PKHeX.Avalonia/Views/MainWindow.axaml");
        var boxViewer = ReadRepositoryFile("PKHeX.Avalonia/Views/BoxViewer.axaml");
        var partyViewer = ReadRepositoryFile("PKHeX.Avalonia/Views/PartyViewer.axaml");

        Assert.Contains("DoubleTapCommandBorder Command=\"{Binding OpenBoxWorkspaceCommand}\"", mainWindow);
        Assert.Contains("DoubleTapCommandBorder Command=\"{Binding OpenPartyWorkspaceCommand}\"", mainWindow);
        Assert.DoesNotContain("OnBoxTabDoubleTapped", mainWindow);
        Assert.DoesNotContain("OnPartyTabDoubleTapped", mainWindow);
        Assert.Contains("Command=\"{Binding OpenBoxWorkspaceCommand}\"", mainWindow);
        Assert.Contains("Command=\"{Binding OpenPartyWorkspaceCommand}\"", mainWindow);
        Assert.Contains("AutomationProperties.Name=\"{loc:Loc Menu_Tools_OpenBoxWorkspace}\"", mainWindow);
        Assert.Contains("AutomationProperties.Name=\"{loc:Loc Menu_Tools_OpenPartyWorkspace}\"", mainWindow);

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

    [AvaloniaFact]
    public void RoutedHeaderDoubleTaps_OpenTheExactLiveBoxAndPartyWorkspaces()
    {
        using var app = new HeadlessAppFixture();
        app.LoadSaveInstance(new SAV6XY());

        var headers = app.Window.GetVisualDescendants().OfType<DoubleTapCommandBorder>().ToArray();
        var boxHeader = Assert.Single(headers, header =>
            ReferenceEquals(header.Command, app.ViewModel.OpenBoxWorkspaceCommand));
        var partyHeader = Assert.Single(headers, header =>
            ReferenceEquals(header.Command, app.ViewModel.OpenPartyWorkspaceCommand));

        boxHeader.RaiseEvent(new TappedEventArgs(InputElement.DoubleTappedEvent, null!));
        partyHeader.RaiseEvent(new TappedEventArgs(InputElement.DoubleTappedEvent, null!));

        Assert.Equal(2, app.Windows.ActiveToolCount);
        Assert.Contains(app.Windows.ShownTools, tool => ReferenceEquals(tool.ViewModel, app.BoxViewer));
        Assert.Contains(app.Windows.ShownTools, tool => ReferenceEquals(tool.ViewModel, app.ViewModel.PartyViewer));
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
