using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using System.Windows.Input;
using Avalonia.Headless.XUnit;
using CommunityToolkit.Mvvm.Input;
using PKHeX.Avalonia.Tests.Fixtures;
using PKHeX.Avalonia.Tests.Harness;
using PKHeX.Core;
using PKHeX.Presentation.ViewModels;

namespace PKHeX.Avalonia.Tests;

/// <summary>
/// Issue #255: workspaces, the Tools menu, the launcher and shortcuts share one capability
/// registry. For representative saves from Gen 1 through Legends: Z-A every visible editor must
/// open a working editor, and editors that cannot operate on the save must not be visible.
/// </summary>
public sealed class CapabilityMatrixTests
{
    private static readonly string[] GenerationGroups =
        ["Menu_Gen1", "Menu_Gen2", "Menu_Gen3", "Menu_Gen4", "Menu_Gen5", "Menu_Gen6", "Menu_Gen7", "Menu_Gen8", "Menu_Gen9"];

    // Support flags that the editor ViewModels expose for their "not available" page.
    private static readonly string[] SupportProperties = ["IsSupported", "HasRecords", "HasDaycare", "HasAnySupport"];

    [AvaloniaTheory]
    [InlineData("gen1_red.sav", "Menu_Gen1")]
    [InlineData("gen2_crystal.sav", "Menu_Gen2")]
    [InlineData("gen3_emerald.sav", "Menu_Gen3")]
    [InlineData("gen3_firered.sav", "Menu_Gen3")]
    [InlineData("gen4_platinum.sav", "Menu_Gen4")]
    [InlineData("gen4_heartgold.sav", "Menu_Gen4")]
    [InlineData("gen5_black.sav", "Menu_Gen5")]
    [InlineData("gen5_white2.sav", "Menu_Gen5")]
    [InlineData("gen6_x.main", "Menu_Gen6")]
    [InlineData("gen6_or.main", "Menu_Gen6")]
    [InlineData("gen7_sun.main", "Menu_Gen7")]
    [InlineData("gen7_ultrasun.main", "Menu_Gen7")]
    [InlineData("gen7b_letsgopikachu.bin", "Menu_Gen7")]
    [InlineData("gen8_sword_ct_public.main", "Menu_Gen8")]
    [InlineData("gen8b_brilliantdiamond.bin", "Menu_Gen8")]
    [InlineData("gen8a_legendsarceus.main", "Menu_Gen8")]
    [InlineData("gen9_scarlet.main", "Menu_Gen9")]
    [InlineData("gen9a_legendsza.main", "Menu_Gen9")]
    public async Task EveryVisibleEditorOpensAWorkingEditorAndOnlyTheSaveGenerationIsListed(string file, string generationGroup)
    {
        using var app = new HeadlessAppFixture();
        Load(app, file);
        var vm = app.ViewModel;

        // Only the loaded generation's catalog is an active choice.
        foreach (var group in vm.ToolMenuGroups.Where(g => GenerationGroups.Contains(GroupKey(g))))
            Assert.Equal(GroupKey(group) == generationGroup, group.IsAvailable);

        // Workspaces: Events and Gifts follow the save's storage, the others are always present.
        Assert.Equal(vm.EventFlagsEditor?.IsSupported == true, vm.IsWorkspaceTabAvailable(MainWindowViewModel.EventsWorkspaceTabIndex));
        Assert.Equal(vm.MysteryGiftEditor?.HasAnySupport == true, vm.IsWorkspaceTabAvailable(MainWindowViewModel.GiftsWorkspaceTabIndex));
        foreach (var index in new[] { 0, 1, 2, 3, 6 })
            Assert.True(vm.IsWorkspaceTabAvailable(index));

        // Every applicable save editor opens an editor that reports support: no dead ends,
        // no silent no-ops after a user activates a visible entry.
        var editors = vm.ToolMenuGroups
            .Where(group => GroupKey(group) == generationGroup || GroupKey(group) == "Menu_Tools_SaveEditors")
            .SelectMany(group => group.Items)
            .Where(item => item.IsApplicable)
            .Where(item => !IsNotAnEditor(item.ActionId))
            .ToList();
        Assert.NotEmpty(editors);

        foreach (var item in editors)
        {
            Assert.True(item.IsAvailable, $"{file}: {item.ActionId} is applicable but disabled");
            var dialogs = app.Windows.ShownDialogs.Count;
            var tools = app.Windows.ShownTools.Count;
            var confirmations = app.Dialogs.Confirmations.Count;
            var errors = app.Dialogs.Errors.Count;

            await ExecuteAsync(item.Command);
            app.Pump();

            Assert.True(app.Dialogs.Errors.Count == errors, $"{file}: {item.ActionId} reported an error");
            var opened = app.Windows.ShownDialogs.Count > dialogs || app.Windows.ShownTools.Count > tools
                || app.Dialogs.Confirmations.Count > confirmations;
            Assert.True(opened, $"{file}: {item.ActionId} is visible but did nothing");

            if (app.Windows.ShownDialogs.Count > dialogs)
                AssertSupported(file, item.ActionId, app.Windows.ShownDialogs[^1].ViewModel);
        }
    }

    [AvaloniaTheory]
    [InlineData("gen9a_legendsza.main", false)]
    [InlineData("gen9_scarlet.main", true)]
    public void ZaAndSvListOnlyTheirOwnGen9Editors(string file, bool isSv)
    {
        using var app = new HeadlessAppFixture();
        Load(app, file);
        var vm = app.ViewModel;

        var gen9 = Assert.Single(vm.ToolMenuGroups, group => GroupKey(group) == "Menu_Gen9");
        var visible = gen9.Items.Where(item => item.IsApplicable).Select(item => item.ActionId).ToHashSet();
        string[] expected = isSv
            ? ["OpenMisc9", "OpenPokedex", "OpenRaid9", "OpenRaidSevenStar9", "OpenRaidEventImport", "OpenFashion9"]
            : SaveEditorCapabilities.HasDonuts(vm.CurrentSave!)
                ? ["OpenPokedex", "OpenFashion9", "OpenDonut"]
                : ["OpenPokedex", "OpenFashion9"];
        Assert.Equal(expected.OrderBy(x => x), visible.OrderBy(x => x));

        // The launcher and the shortcut registry never offer an inapplicable editor either.
        Assert.All(vm.FilteredToolLauncherItems, item => Assert.True(item.IsApplicable));
        Assert.DoesNotContain(vm.ToolMenuGroups.SelectMany(g => g.Items),
            item => item.IsAvailable && !item.IsApplicable);

        if (!isSv)
        {
            // Z-A has categorized events but no gift storage.
            Assert.True(vm.IsEventsWorkspace || vm.IsWorkspaceTabAvailable(MainWindowViewModel.EventsWorkspaceTabIndex));
            Assert.False(vm.IsWorkspaceTabAvailable(MainWindowViewModel.GiftsWorkspaceTabIndex));
            vm.SelectWorkspaceTabCommand.Execute(MainWindowViewModel.GiftsWorkspaceTabIndex);
            Assert.NotEqual(MainWindowViewModel.GiftsWorkspaceTabIndex, vm.SelectedWorkspaceIndex);
            vm.SelectWorkspaceTabCommand.Execute(MainWindowViewModel.EventsWorkspaceTabIndex);
            Assert.Equal(MainWindowViewModel.EventsWorkspaceTabIndex, vm.SelectedWorkspaceIndex);
        }
    }

    [AvaloniaFact]
    public void SaveChangeRefreshesTheRegistryAndLeavesAnUnsupportedWorkspace()
    {
        using var app = new HeadlessAppFixture();
        var dir = SaveFileFixture.FindSaveFilesPath()!;
        app.LoadSave(Path.Combine(dir, "gen4_heartgold.sav"));
        var vm = app.ViewModel;
        Assert.True(vm.IsWorkspaceTabAvailable(MainWindowViewModel.EventsWorkspaceTabIndex));
        vm.SelectWorkspaceTabCommand.Execute(MainWindowViewModel.EventsWorkspaceTabIndex);
        Assert.Equal(MainWindowViewModel.EventsWorkspaceTabIndex, vm.SelectedWorkspaceIndex);
        vm.OpenBoxWorkspaceCommand.Execute(null);
        var hgssItem = vm.ToolMenuGroups.SelectMany(g => g.Items).First(item => item.ActionId == "OpenPokeGear4");
        Assert.True(hgssItem.IsApplicable);

        app.LoadSave(Path.Combine(dir, "gen9_scarlet.main"));

        // One save change: tools closed, navigation reset, and every descriptor re-evaluated.
        Assert.True(app.Windows.CloseAllToolsCount > 0);
        Assert.Equal(0, app.Windows.ActiveToolCount);
        Assert.Equal(MainWindowViewModel.BoxWorkspaceTabIndex, vm.SelectedWorkspaceIndex);
        Assert.False(hgssItem.IsApplicable);
        Assert.False(hgssItem.IsAvailable);
        Assert.DoesNotContain(vm.AvailableToolMenuGroups, group => GroupKey(group) == "Menu_Gen4");
    }

    [AvaloniaFact]
    public void ToolsMenuHidesInapplicableEntriesInsteadOfShowingDeadEnds()
    {
        var axaml = File.ReadAllText(FindRepoFile("PKHeX.Avalonia/Views/MainWindow.axaml"));
        Assert.Contains("<Setter Property=\"IsVisible\" Value=\"{Binding IsApplicable}\" />", axaml);
    }

    private static string GroupKey(ToolMenuGroup group) => group.TitleKey;

    // The public Let's Go fixture has no auto-detection metadata, so it uses its typed constructor.
    private static void Load(HeadlessAppFixture app, string file)
    {
        var path = Path.Combine(SaveFileFixture.FindSaveFilesPath()!, file);
        if (file.StartsWith("gen7b_", System.StringComparison.Ordinal))
            app.LoadSaveInstance(new SAV7b(File.ReadAllBytes(path)), path);
        else
            app.LoadSave(path);
    }

    // Entries in the Save Editors group that launch a file workflow or an independent tool instead
    // of an editor for the loaded save. They stay available for every save.
    private static bool IsNotAnEditor(string actionId) => actionId is
        "OpenSaveHandlerTroubleshooter" or "OpenLiveHeX" or "OpenLivingDexGenerator"
        or "OpenBackupManager" or "OpenSaveDiff" or "OpenBoxManip" or "OpenBatchEditor";

    private static async Task ExecuteAsync(ICommand command)
    {
        if (command is IAsyncRelayCommand async)
            await async.ExecuteAsync(null);
        else
            command.Execute(null);
    }

    private static void AssertSupported(string file, string actionId, object viewModel)
    {
        foreach (var name in SupportProperties)
        {
            var property = viewModel.GetType().GetProperty(name, BindingFlags.Instance | BindingFlags.Public);
            if (property?.PropertyType == typeof(bool))
                Assert.True((bool)property.GetValue(viewModel)!, $"{file}: {actionId} opened {viewModel.GetType().Name} with {name} = false");
        }
    }

    private static string FindRepoFile(string relative)
    {
        var dir = new DirectoryInfo(System.AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, relative)))
            dir = dir.Parent;
        return Path.Combine(dir?.FullName ?? throw new FileNotFoundException(relative), relative);
    }
}
