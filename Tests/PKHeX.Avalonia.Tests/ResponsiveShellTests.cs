using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using PKHeX.Avalonia.Tests.Harness;
using PKHeX.Core;

namespace PKHeX.Avalonia.Tests;

public sealed class ResponsiveShellTests
{
    [Fact]
    public void MainWindow_DeclaresCompactResizableEditorAndDistinctWorkspaceTabs()
    {
        var mainWindow = ReadSourceFile("Views", "MainWindow.axaml");
        var theme = ReadSourceFile("Styles", "Theme.axaml");

        Assert.Contains("Width=\"1024\" Height=\"720\"", mainWindow);
        Assert.Contains("Width=\"380\" MinWidth=\"360\" MaxWidth=\"480\"", mainWindow);
        Assert.Contains("x:Name=\"EditorPane\"", mainWindow);
        Assert.Contains("x:Name=\"WorkspacePane\"", mainWindow);
        Assert.Contains("Classes=\"pane-splitter shell-divider\"", mainWindow);
        Assert.Contains("Classes=\"workspace-tabs\"", mainWindow);
        Assert.Equal(7, System.Text.RegularExpressions.Regex.Matches(mainWindow, "Classes=\"workspace-tab\"").Count);

        Assert.Contains("TabControl.editor-tabs TabItem.editor-tab:selected", theme);
        Assert.Contains("BorderThickness\" Value=\"3,0,0,0", theme);
        Assert.Contains("TabControl.workspace-tabs TabItem.workspace-tab:selected", theme);
        Assert.Contains("TabControl.editor-tabs TabItem.editor-tab:focus-visible", theme);
        Assert.Contains("TabControl.workspace-tabs TabItem.workspace-tab:focus-visible", theme);
        var selectedEditorStyle = System.Text.RegularExpressions.Regex.Match(
            theme,
            "<Style Selector=\"TabControl\\.editor-tabs TabItem\\.editor-tab:selected\">(?<body>.*?)</Style>",
            System.Text.RegularExpressions.RegexOptions.Singleline);
        Assert.True(selectedEditorStyle.Success);
        Assert.DoesNotContain("ThemeAccentGlowBrush", selectedEditorStyle.Groups["body"].Value);
        Assert.Contains("ThemeBackgroundElevatedBrush", selectedEditorStyle.Groups["body"].Value);
    }

    [AvaloniaFact]
    public void MainWindow_At1024x720PreservesEditorAndWorkspaceWorkingWidths()
    {
        using var app = new HeadlessAppFixture();
        app.Window.Width = 1024;
        app.Window.Height = 720;
        app.LoadSaveInstance(new SAV6XY(), @"C:\Saves\pokemon-x-main");
        app.Pump();

        var editor = app.FindByName<Border>("EditorPane");
        var workspace = app.FindByName<TabControl>("WorkspacePane");

        Assert.NotNull(editor);
        Assert.NotNull(workspace);
        Assert.InRange(editor!.Bounds.Width, 360, 480);
        Assert.True(workspace!.Bounds.Width >= 560, $"Workspace was only {workspace.Bounds.Width}px wide.");
        Assert.Equal("pokemon-x-main", app.ViewModel.CurrentSaveFileName);
        Assert.Equal(@"C:\Saves\pokemon-x-main", app.ViewModel.CurrentSavePath);
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
            var parent = Directory.GetParent(dir)
                ?? throw new DirectoryNotFoundException("Could not find repository root");
            dir = parent.FullName;
        }

        return dir;
    }
}
