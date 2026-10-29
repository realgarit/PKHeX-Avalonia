using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.Text.Json;
using System.Windows.Input;
using Avalonia.Headless.XUnit;
using Microsoft.Extensions.DependencyInjection;
using PKHeX.Application.Shortcuts;
using PKHeX.Avalonia.Input;
using PKHeX.Avalonia.Tests.Harness;
using PKHeX.Core;
using PKHeX.Presentation.Localization;
using PKHeX.Presentation.ViewModels;

namespace PKHeX.Avalonia.Tests.Shortcuts;

/// <summary>
/// The shortcut action catalog of the main window (issue #224): stable action IDs, one ID per
/// command, localized titles, conflict-free defaults, and the view model wiring that publishes the
/// resolved shortcuts to the menus, the launcher and the host's key bindings.
/// </summary>
public sealed class ShortcutCatalogTests
{
    private const ShortcutModifiers Ctrl = ShortcutModifiers.Control;
    private const ShortcutModifiers Shift = ShortcutModifiers.Shift;
    private const ShortcutModifiers Meta = ShortcutModifiers.Meta;

    private static readonly string[] Languages = ["en", "ja", "fr", "it", "de", "es", "ko", "zh-Hans", "zh-Hant", "pt-BR"];

    // ---------------------------------------------------------------------
    // Action identity
    // ---------------------------------------------------------------------

    [Theory]
    [InlineData("OpenEncounterDatabaseCommand", "OpenEncounterDatabase")]
    [InlineData("vm.OpenPokedexCommand", "OpenPokedex")]
    [InlineData(" this.UndoCommand ", "Undo")]
    [InlineData("OpenRTC3Command", "OpenRTC3")]
    [InlineData("OpenFile", "OpenFile")]
    public void FromExpression_UsesThePropertyNameWithoutCommand(string expression, string expected)
    {
        Assert.Equal(expected, ShortcutActionId.FromExpression(expression));
    }

    [Theory]
    [InlineData("")]
    [InlineData("command")]
    [InlineData("Command")]
    [InlineData("vm.Open_ThingCommand")]
    [InlineData("GetCommand()")]
    public void FromExpression_RejectsTextThatIsNotACommandProperty(string expression)
    {
        Assert.Throws<ArgumentException>(() => ShortcutActionId.FromExpression(expression));
    }

    [Fact]
    public void EveryActionId_HasTheDocumentedForm()
    {
        var vm = CreateViewModel();

        Assert.All(vm.Shortcuts.All, action => Assert.True(ShortcutActionId.IsValid(action.Id), action.Id));
        Assert.All(vm.ToolLauncherItems, item => Assert.True(ShortcutActionId.IsValid(item.ActionId), item.ActionId));
        Assert.Equal(vm.Shortcuts.All.Count, vm.Shortcuts.All.Select(action => action.Id).Distinct(StringComparer.Ordinal).Count());
    }

    [Fact]
    public void RegistryDescriptors_ShareOneIdPerCommand()
    {
        var vm = CreateViewModel();
        var registry = vm.ToolLauncherItems;

        // Same ID always means the same command, and the same command always means the same ID.
        foreach (var group in registry.GroupBy(item => item.ActionId, StringComparer.Ordinal))
            Assert.Single(group.Select(item => item.Command).Distinct(ReferenceEqualityComparer.Instance));
        foreach (var group in registry.GroupBy(item => item.Command, ReferenceEqualityComparer.Instance))
            Assert.Single(group.Select(item => item.ActionId).Distinct(StringComparer.Ordinal));

        Assert.Equal(6, registry.Count(item => item.ActionId == "OpenPokedex"));
        Assert.Equal(2, registry.Count(item => item.ActionId == "OpenBoxLayout"));
        Assert.Equal(2, registry.Count(item => item.ActionId == "OpenRaidEventImport"));

        // Shared title keys with different commands stay separate actions.
        Assert.Equal(new[] { "OpenMisc3", "OpenMisc4", "OpenMisc5", "OpenMisc7", "OpenMisc9" },
            IdsWithTitle(registry, "Menu_Misc"));
        Assert.Equal(new[] { "OpenHallOfFame", "OpenHallOfFame1", "OpenHallOfFame3" },
            IdsWithTitle(registry, "Menu_Save_HallOfFame"));
        Assert.Equal(new[] { "OpenRoamer3", "OpenRoamer6" }, IdsWithTitle(registry, "Menu_Roamer"));

        // One catalog action per registry command, carrying every descriptor of that command.
        var registryActions = vm.Shortcuts.All.Where(action => action.Descriptors.Count > 0).ToList();
        Assert.Equal(registry.Select(item => item.ActionId).Distinct(StringComparer.Ordinal).Count(), registryActions.Count);
        Assert.Equal(registry.Count, registryActions.Sum(action => action.Descriptors.Count));
        var pokedex = vm.Shortcuts["OpenPokedex"];
        Assert.Same(vm.OpenPokedexCommand, pokedex.Command);
        Assert.Equal(new[] { "Menu_Gen4", "Menu_Gen5", "Menu_Gen6", "Menu_Gen7", "Menu_Gen8", "Menu_Gen9" }, pokedex.CategoryKeys);
        Assert.Equal(new[] { "Menu_Tools_SaveEditors", "Menu_Gen4" }, vm.Shortcuts["OpenBoxLayout"].CategoryKeys);
        Assert.Equal("Menu_Save_BoxLayout", vm.Shortcuts["OpenBoxLayout"].TitleKey);

        static string[] IdsWithTitle(IEnumerable<ToolLauncherItem> items, string titleKey) =>
            items.Where(item => item.TitleKey == titleKey).Select(item => item.ActionId)
                .Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray();
    }

    [Fact]
    public void ShellActions_RunDistinctCommandsOutsideTheRegistry()
    {
        var vm = CreateViewModel();
        var shell = vm.Shortcuts.All.Where(action => action.Descriptors.Count == 0).ToList();
        var registryCommands = vm.ToolLauncherItems.Select(item => item.Command).ToHashSet(ReferenceEqualityComparer.Instance);

        Assert.Equal(20, shell.Count);
        Assert.Equal(shell.Count, shell.Select(action => (action.Command, action.CommandParameter)).Distinct().Count());
        Assert.All(shell, action => Assert.DoesNotContain(action.Command, registryCommands));

        Assert.Same(vm.SelectWorkspaceCommand, vm.Shortcuts.WorkspaceSave.Command);
        Assert.Equal(MainWorkspace.Save, vm.Shortcuts.WorkspaceSave.CommandParameter);
        Assert.Same(vm.SelectWorkspaceTabCommand, vm.Shortcuts.TabBatch.Command);
        Assert.Equal(MainWindowViewModel.BatchWorkspaceTabIndex, vm.Shortcuts.TabBatch.CommandParameter);
        Assert.Same(vm.OpenKeyboardShortcutsCommand, vm.Shortcuts.OpenKeyboardShortcuts.Command);

        // Shell actions come first, in menu order: File, Edit, Window, Help.
        Assert.Equal(shell, vm.Shortcuts.All.Take(shell.Count));
        Assert.Equal(new[] { "Menu_File", "Menu_Edit", "Menu_Window", "Menu_Help" },
            shell.Select(action => action.CategoryKeys.Single()).Distinct().ToArray());
    }

    [Fact]
    public void ActionIds_MatchTheGoldenList()
    {
        var vm = CreateViewModel();
        var path = Path.Combine(FindRepoRoot(), "Tests", "PKHeX.Avalonia.Tests", "Shortcuts", "shortcut-action-ids.txt");
        var expected = File.ReadAllLines(path)
            .Select(line => line.Trim())
            .Where(line => line.Length > 0 && !line.StartsWith('#'))
            .ToArray();

        var actual = vm.Shortcuts.All.Select(action => action.Id).Order(StringComparer.Ordinal).ToArray();

        Assert.True(expected.SequenceEqual(actual),
            "Shortcut action IDs changed. IDs are the keys of stored overrides; a rename orphans them. " +
            $"Update shortcut-action-ids.txt only on purpose.\nMissing: {string.Join(", ", expected.Except(actual))}\n" +
            $"New: {string.Join(", ", actual.Except(expected))}");
    }

    // ---------------------------------------------------------------------
    // Titles
    // ---------------------------------------------------------------------

    [Fact]
    public void TitleAndCategoryKeys_ExistInEnglish()
    {
        var vm = CreateViewModel();
        var english = LoadEnglishKeys();

        Assert.All(vm.Shortcuts.All, action =>
        {
            Assert.True(english.Contains(action.TitleKey), $"{action.Id}: missing title key {action.TitleKey}");
            Assert.NotEmpty(action.CategoryKeys);
            Assert.All(action.CategoryKeys, key => Assert.True(english.Contains(key), $"{action.Id}: missing category key {key}"));
        });
    }

    [AvaloniaFact]
    public void Titles_HaveNoAccessKeyMarker_InAnyLanguage()
    {
        var vm = CreateViewModel();
        var previous = LocalizedStrings.Instance.CurrentLanguage;
        try
        {
            foreach (var language in Languages)
            {
                LocalizedStrings.Instance.SetLanguage(language);
                Assert.All(vm.Shortcuts.All, action =>
                {
                    Assert.False(string.IsNullOrWhiteSpace(action.Title), $"{language} {action.Id}: empty title");
                    Assert.DoesNotContain("_", action.Title);
                    Assert.False(string.IsNullOrWhiteSpace(action.CategoryTitle), $"{language} {action.Id}: empty category");
                    Assert.DoesNotContain("_", action.CategoryTitle);
                });
            }

            LocalizedStrings.Instance.SetLanguage("de");
            Assert.Equal("Öffnen...", vm.Shortcuts.OpenFile.Title);
            Assert.Equal("Datei", vm.Shortcuts.OpenFile.CategoryTitle);
        }
        finally
        {
            LocalizedStrings.Instance.SetLanguage(previous);
        }
    }

    [Theory]
    [InlineData("_Open...", "Open...")]
    [InlineData("Ö_ffnen...", "Öffnen...")]
    [InlineData("Save _As...", "Save As...")]
    [InlineData("Snake__case", "Snake_case")]
    [InlineData("Trailing_", "Trailing")]
    [InlineData("No marker", "No marker")]
    [InlineData("", "")]
    public void AccessKeyText_RemovesMarkersAndKeepsDoubledUnderscores(string text, string expected)
    {
        Assert.Equal(expected, AccessKeyText.Strip(text));
    }

    [AvaloniaFact]
    public void RegistryAction_CategoryTitleListsEveryMenu()
    {
        var vm = CreateViewModel();
        var previous = LocalizedStrings.Instance.CurrentLanguage;
        try
        {
            LocalizedStrings.Instance.SetLanguage("en");
            var loc = LocalizedStrings.Instance;
            Assert.Equal(string.Join(", ", new[] { "Menu_Gen4", "Menu_Gen5", "Menu_Gen6", "Menu_Gen7", "Menu_Gen8", "Menu_Gen9" }.Select(key => loc[key])),
                vm.Shortcuts["OpenPokedex"].CategoryTitle);
        }
        finally
        {
            LocalizedStrings.Instance.SetLanguage(previous);
        }
    }

    // ---------------------------------------------------------------------
    // Defaults
    // ---------------------------------------------------------------------

    public static TheoryData<string> Platforms => new() { "fallback", "standard-desktop", "apple", "host", "host-apple" };

    private static IShortcutPlatform CreatePlatform(string name) => name switch
    {
        "fallback" => StandardShortcutPlatform.Instance,
        "standard-desktop" => FakeShortcutPlatform.StandardDesktop(),
        "apple" => FakeShortcutPlatform.Apple(),
        // The real host platform (headless reports the Control command modifier) and the host with
        // the macOS command modifier injected.
        "host" => new AvaloniaShortcutPlatform(),
        "host-apple" => AvaloniaShortcutPlatformTests.AppleHost(),
        _ => throw new ArgumentOutOfRangeException(nameof(name)),
    };

    [AvaloniaTheory]
    [MemberData(nameof(Platforms))]
    public void Defaults_AreUsableAndConflictFree(string platformName)
    {
        var platform = CreatePlatform(platformName);
        var vm = CreateViewModel();
        var definitions = vm.Shortcuts.Definitions;

        var table = ShortcutResolver.Resolve(definitions, null, platform);

        foreach (var definition in definitions)
        {
            var resolved = table[definition.Id];
            Assert.Null(resolved.Issue);
            // A default must never hold Primary next to the literal modifier it stands for.
            foreach (var flavor in new[] { ShortcutPlatformFlavor.Standard, ShortcutPlatformFlavor.Apple })
            {
                if (ShortcutChord.TryParse(definition.DefaultFor(flavor), out var portable))
                    Assert.False(portable.IsAmbiguousOn(flavor), $"{definition.Id} default {portable} on {flavor}");
            }
            Assert.Equal(resolved.Default, resolved.Effective);
            if (resolved.Default is { } chord)
            {
                var usable = ShortcutPolicy.CheckUsable(definition, chord, platform);
                Assert.True(usable.Kind == ShortcutValidationKind.Ok, $"{platformName}: {definition.Id} default {chord} is {usable.Kind} ({usable.ReservedFor})");
            }
        }

        var chords = table.Ordered.Where(entry => entry.Effective is not null).Select(entry => entry.Effective!.Value).ToList();
        Assert.Equal(chords.Count, chords.Distinct().Count());
    }

    [Fact]
    public void DefaultIds_NameCatalogActions()
    {
        var vm = CreateViewModel();
        Assert.All(ShortcutDefaults.Ids, id => Assert.True(vm.Shortcuts.TryGet(id, out _), $"Default for unknown action {id}"));
    }

    [Fact]
    public void Defaults_FollowThePlatformConvention()
    {
        var vm = CreateViewModel();
        var standard = ShortcutResolver.Resolve(vm.Shortcuts.Definitions, null, StandardShortcutPlatform.Instance);
        var apple = ShortcutResolver.Resolve(vm.Shortcuts.Definitions, null, FakeShortcutPlatform.Apple());

        var expected = new Dictionary<string, (ShortcutChord? Standard, ShortcutChord? Apple)>
        {
            [ShortcutActionId.OpenFile] = (new(Ctrl, "O"), new(Meta, "O")),
            [ShortcutActionId.SaveFile] = (new(Ctrl, "S"), new(Meta, "S")),
            [ShortcutActionId.SaveFileAs] = (new(Ctrl | Shift, "S"), new(Meta | Shift, "S")),
            [ShortcutActionId.OpenToolLauncher] = (new(Ctrl, "K"), new(Meta, "K")),
            [ShortcutActionId.WorkspacePokemon] = (new(Ctrl, "D1"), new(Meta, "D1")),
            [ShortcutActionId.WorkspaceSave] = (new(Ctrl, "D2"), new(Meta, "D2")),
            [ShortcutActionId.WorkspaceReports] = (new(Ctrl, "D3"), new(Meta, "D3")),
            [ShortcutActionId.Undo] = (new(Ctrl, "Z"), new(Meta, "Z")),
            [ShortcutActionId.Redo] = (new(Ctrl, "Y"), new(Meta | Shift, "Z")),
            ["OpenEncounterDatabase"] = (new(Ctrl, "N"), new(Meta, "N")),
            ["OpenPKMDatabase"] = (new(Ctrl, "D"), new(Meta, "D")),
            ["OpenMysteryGiftDatabase"] = (new(Ctrl, "G"), new(Meta, "G")),
            ["OpenBoxReport"] = (new(Ctrl, "R"), new(Meta, "R")),
            ["OpenBatchEditor"] = (new(Ctrl, "M"), null),
        };

        foreach (var action in vm.Shortcuts.All)
        {
            var (expectedStandard, expectedApple) = expected.TryGetValue(action.Id, out var chords) ? chords : (null, null);
            Assert.True(expectedStandard == standard.EffectiveFor(action.Id), $"Standard {action.Id}");
            Assert.True(expectedApple == apple.EffectiveFor(action.Id), $"Apple {action.Id}");
        }

        Assert.Equal(ShortcutTextRole.Undo, vm.Shortcuts.Undo.Definition.TextRole);
        Assert.Equal(ShortcutTextRole.Redo, vm.Shortcuts.Redo.Definition.TextRole);
        Assert.All(vm.Shortcuts.All.Where(action => action != vm.Shortcuts.Undo && action != vm.Shortcuts.Redo),
            action => Assert.Equal(ShortcutTextRole.None, action.Definition.TextRole));
    }

    // ---------------------------------------------------------------------
    // View model wiring
    // ---------------------------------------------------------------------

    [Fact]
    public void Bindings_PublishEveryEffectiveDefault()
    {
        var vm = CreateViewModel();

        var bindings = vm.ShortcutBindings.ToDictionary(entry => entry.ActionId, StringComparer.Ordinal);
        Assert.Equal(14, bindings.Count);
        Assert.Equal(new ShortcutChord(Ctrl, "O"), bindings[ShortcutActionId.OpenFile].Chord);
        Assert.Equal(new ShortcutChord(Ctrl, "D2"), bindings[ShortcutActionId.WorkspaceSave].Chord);
        Assert.Equal(new ShortcutChord(Ctrl, "N"), bindings["OpenEncounterDatabase"].Chord);
        Assert.Equal(ShortcutTextRole.Undo, bindings[ShortcutActionId.Undo].TextRole);
        Assert.Equal(ShortcutTextRole.Redo, bindings[ShortcutActionId.Redo].TextRole);
        Assert.Equal(ShortcutTextRole.None, bindings["OpenEncounterDatabase"].TextRole);

        Assert.Equal("Ctrl+1", vm.Shortcuts.WorkspacePokemon.ShortcutText);
        Assert.Equal("Ctrl+Shift+S", vm.Shortcuts.SaveFileAs.ShortcutText);
        Assert.False(vm.Shortcuts.CloseFile.HasShortcut);
        Assert.Equal(string.Empty, vm.Shortcuts.CloseFile.ShortcutText);

        // Registry descriptors show their action's shortcut in the Tools menu and the launcher.
        var encounter = Assert.Single(vm.ToolLauncherItems, item => item.ActionId == "OpenEncounterDatabase");
        Assert.True(encounter.HasShortcut);
        Assert.Equal(new ShortcutChord(Ctrl, "N"), encounter.Shortcut);
        Assert.Equal("Ctrl+N", encounter.ShortcutText);
        Assert.All(vm.ToolLauncherItems.Where(item => item.ActionId == "OpenPokedex"), item => Assert.False(item.HasShortcut));
    }

    [Fact]
    public void Apply_RebindsOnceAndUpdatesEveryDescriptor()
    {
        var settings = new AppSettings();
        var store = new FakeSettingsStore();
        var service = new KeyboardShortcutService(settings, store, StandardShortcutPlatform.Instance);
        var vm = CreateViewModel(settings, store, service);
        var rebinds = 0;
        vm.ShortcutBindingsChanged += (_, _) => rebinds++;
        var encounter = Assert.Single(vm.ToolLauncherItems, item => item.ActionId == "OpenEncounterDatabase");
        var encounterChanges = new List<string?>();
        encounter.PropertyChanged += (_, e) => encounterChanges.Add(e.PropertyName);

        var changed = service.Apply(vm.Shortcuts.Definitions, new Dictionary<string, ShortcutChord?>
        {
            ["OpenEncounterDatabase"] = new ShortcutChord(Ctrl | Shift, "N"),
            ["OpenPokedex"] = new ShortcutChord(Ctrl | Shift, "P"),
            [ShortcutActionId.OpenFile] = null,
        });

        Assert.True(changed);
        Assert.Equal(1, rebinds);
        Assert.Equal("Primary+Shift+N", settings.KeyboardShortcuts["OpenEncounterDatabase"]);
        Assert.Equal(string.Empty, settings.KeyboardShortcuts[ShortcutActionId.OpenFile]);

        var bindings = vm.ShortcutBindings.ToDictionary(entry => entry.ActionId, StringComparer.Ordinal);
        Assert.Equal(new ShortcutChord(Ctrl | Shift, "N"), bindings["OpenEncounterDatabase"].Chord);
        Assert.Equal(new ShortcutChord(Ctrl | Shift, "P"), bindings["OpenPokedex"].Chord);
        Assert.False(bindings.ContainsKey(ShortcutActionId.OpenFile));
        Assert.False(vm.Shortcuts.OpenFile.HasShortcut);
        Assert.Null(vm.Shortcuts.OpenFile.Shortcut);

        Assert.Equal("Ctrl+Shift+N", encounter.ShortcutText);
        Assert.Contains(nameof(ToolLauncherItem.Shortcut), encounterChanges);
        Assert.Contains(nameof(ToolLauncherItem.ShortcutText), encounterChanges);
        Assert.All(vm.ToolLauncherItems.Where(item => item.ActionId == "OpenPokedex"), item =>
        {
            Assert.Equal(new ShortcutChord(Ctrl | Shift, "P"), item.Shortcut);
            Assert.Equal("Ctrl+Shift+P", item.ShortcutText);
        });
    }

    [Fact]
    public void Resolution_ReportsStoredProblemsOnTheAction()
    {
        var settings = new AppSettings();
        settings.KeyboardShortcuts["OpenBoxReport"] = "Ctrl+Bogus";
        settings.KeyboardShortcuts["OpenMisc3"] = "Primary+N";
        var vm = CreateViewModel(settings);

        var boxReport = vm.Shortcuts["OpenBoxReport"];
        Assert.Equal(ShortcutIssueKind.InvalidStored, boxReport.Issue?.Kind);
        Assert.Equal(new ShortcutChord(Ctrl, "R"), boxReport.Shortcut);

        // The stored override wins over the Encounter Database default; the default is suppressed.
        Assert.Equal(new ShortcutChord(Ctrl, "N"), vm.Shortcuts["OpenMisc3"].Shortcut);
        var encounter = vm.Shortcuts["OpenEncounterDatabase"];
        Assert.Null(encounter.Shortcut);
        Assert.Equal(ShortcutIssueKind.SuppressedDefault, encounter.Issue?.Kind);
        Assert.Equal("OpenMisc3", encounter.Issue?.OtherActionId);
        Assert.DoesNotContain(vm.ShortcutBindings, entry => entry.ActionId == "OpenEncounterDatabase");
    }

    [Fact]
    public void Bindings_CheckAvailabilityWhenPressed()
    {
        var settings = new AppSettings();
        var store = new FakeSettingsStore();
        var service = new KeyboardShortcutService(settings, store, StandardShortcutPlatform.Instance);
        var vm = CreateViewModel(settings, store, service);
        var bindings = vm.ShortcutBindings.ToDictionary(entry => entry.ActionId, StringComparer.Ordinal);

        // No save: file open works, save-bound tools do not, and the gated tabs stay closed.
        Assert.True(bindings[ShortcutActionId.OpenFile].CanInvoke());
        Assert.False(bindings[ShortcutActionId.SaveFile].CanInvoke());
        Assert.False(bindings["OpenEncounterDatabase"].CanInvoke());
        Assert.False(bindings[ShortcutActionId.Undo].CanInvoke());
        Assert.False(vm.Shortcuts.TabEvents.CanInvoke());
        Assert.False(vm.Shortcuts.TabGifts.CanInvoke());
        Assert.True(vm.Shortcuts.TabTrainer.CanInvoke());
        Assert.True(vm.Shortcuts.OpenKeyboardShortcuts.CanInvoke());

        // While the shortcut editor records keys, no shortcut runs.
        service.IsEditorOpen = true;
        Assert.False(bindings[ShortcutActionId.OpenFile].CanInvoke());
        Assert.False(bindings[ShortcutActionId.WorkspaceSave].CanInvoke());
        bindings[ShortcutActionId.WorkspaceSave].Invoke();
        Assert.Equal(MainWorkspace.Pokemon, vm.ActiveWorkspace);
    }

    [Fact]
    public void Invoke_ClosesTheLauncherLikeALauncherClick()
    {
        var vm = CreateViewModel();
        var bindings = vm.ShortcutBindings.ToDictionary(entry => entry.ActionId, StringComparer.Ordinal);

        bindings[ShortcutActionId.OpenToolLauncher].Invoke();
        Assert.True(vm.IsToolLauncherOpen);

        // Pressing the launcher shortcut again leaves it open.
        bindings[ShortcutActionId.OpenToolLauncher].Invoke();
        Assert.True(vm.IsToolLauncherOpen);

        vm.ToolSearchText = "box";
        bindings[ShortcutActionId.WorkspaceReports].Invoke();
        Assert.False(vm.IsToolLauncherOpen);
        Assert.Equal(string.Empty, vm.ToolSearchText);
        Assert.Equal(MainWorkspace.Reports, vm.ActiveWorkspace);

        bindings[ShortcutActionId.WorkspaceSave].Invoke();
        Assert.Equal(MainWorkspace.Save, vm.ActiveWorkspace);
        bindings[ShortcutActionId.WorkspacePokemon].Invoke();
        Assert.Equal(MainWorkspace.Pokemon, vm.ActiveWorkspace);
    }

    [AvaloniaFact]
    public void LanguageChange_RefreshesActionTitles()
    {
        using var state = new LocalizationState();
        var languages = new LanguageService();
        var vm = CreateViewModel(languageService: languages);
        var open = vm.Shortcuts.OpenFile;
        var changes = new List<string?>();
        open.PropertyChanged += (_, e) => changes.Add(e.PropertyName);

        languages.SetLanguage("de");

        Assert.Equal("Öffnen...", open.Title);
        Assert.Contains(nameof(ShortcutAction.Title), changes);
        Assert.Contains(nameof(ShortcutAction.CategoryTitle), changes);
    }

    [AvaloniaFact]
    public void ComposedApp_GatesRegistryActionsBySaveType()
    {
        using var app = new HeadlessAppFixture();
        var vm = app.ViewModel;
        var service = app.Services.GetRequiredService<KeyboardShortcutService>();

        Assert.False(vm.Shortcuts["OpenPokedex"].CanInvoke());
        Assert.False(vm.Shortcuts["OpenMisc3"].CanInvoke());
        Assert.True(vm.Shortcuts["OpenFolderList"].CanInvoke());

        app.LoadSaveInstance(new SAV6XY());
        Assert.True(vm.Shortcuts["OpenPokedex"].CanInvoke());
        Assert.False(vm.Shortcuts["OpenMisc3"].CanInvoke());
        Assert.True(vm.Shortcuts["OpenEncounterDatabase"].CanInvoke());
        Assert.True(vm.Shortcuts.TabGifts.CanInvoke());

        app.LoadSaveInstance(new SAV3E());
        Assert.False(vm.Shortcuts["OpenPokedex"].CanInvoke());
        Assert.True(vm.Shortcuts["OpenMisc3"].CanInvoke());

        // The view model uses the container's shortcut service: an applied change rebinds it.
        var rebinds = 0;
        vm.ShortcutBindingsChanged += (_, _) => rebinds++;
        service.Apply(vm.Shortcuts.Definitions, new Dictionary<string, ShortcutChord?>
        {
            ["OpenMisc3"] = new ShortcutChord(Ctrl | Shift, "M"),
        });
        Assert.Equal(1, rebinds);
        var misc3 = Assert.Single(vm.ShortcutBindings, entry => entry.ActionId == "OpenMisc3");
        Assert.Equal(new ShortcutChord(Ctrl | Shift, "M"), misc3.Chord);
        Assert.True(misc3.CanInvoke());
    }

    [AvaloniaFact]
    public void ComposedApp_KeepsEveryCapabilityInOneMenuGroup()
    {
        using var app = new HeadlessAppFixture();
        app.LoadSaveInstance(new SAV6XY());
        var menuEntries = app.ViewModel.ToolMenuGroups.SelectMany(group => group.Items).ToList();

        Assert.Equal(app.ViewModel.ToolLauncherItems.Count, menuEntries.Count);
        Assert.Equal(menuEntries.Count, app.ViewModel.Shortcuts.All.Sum(action => action.Descriptors.Count));
    }

    // ---------------------------------------------------------------------
    // Helpers
    // ---------------------------------------------------------------------

    private static MainWindowViewModel CreateViewModel(
        AppSettings? settings = null,
        FakeSettingsStore? store = null,
        KeyboardShortcutService? service = null,
        LanguageService? languageService = null) =>
        ShortcutTestViewModels.CreateMainViewModel(settings, store, service, languageService);

    private static HashSet<string> LoadEnglishKeys()
    {
        var path = Path.Combine(FindRepoRoot(), "PKHeX.Presentation", "Localization", "Strings", "en.json");
        using var document = JsonDocument.Parse(File.ReadAllText(path));
        return document.RootElement.EnumerateObject().Select(property => property.Name).ToHashSet(StringComparer.Ordinal);
    }

    private static string FindRepoRoot()
    {
        var dir = AppDomain.CurrentDomain.BaseDirectory;
        while (!Directory.Exists(Path.Combine(dir, "PKHeX.Avalonia")))
        {
            var parent = Directory.GetParent(dir) ?? throw new DirectoryNotFoundException("Could not find repository root");
            dir = parent.FullName;
        }
        return dir;
    }

    /// <summary>Restores the UI language, game-data language and culture a language switch changes.</summary>
    private sealed class LocalizationState : IDisposable
    {
        private readonly string _uiLanguage = LocalizedStrings.Instance.CurrentLanguage;
        private readonly string _dataLanguage = GameInfo.CurrentLanguage;
        private readonly GameStrings _strings = GameInfo.Strings;
        private readonly FilteredGameDataSource _filteredSources = GameInfo.FilteredSources;
        private readonly CultureInfo _culture = CultureInfo.CurrentCulture;
        private readonly CultureInfo _uiCulture = CultureInfo.CurrentUICulture;
        private readonly CultureInfo? _defaultCulture = CultureInfo.DefaultThreadCurrentCulture;
        private readonly CultureInfo? _defaultUiCulture = CultureInfo.DefaultThreadCurrentUICulture;

        public void Dispose()
        {
            GameInfo.CurrentLanguage = _dataLanguage;
            GameInfo.Strings = _strings;
            GameInfo.FilteredSources = _filteredSources;
            LocalizedStrings.Instance.SetLanguage(_uiLanguage);
            CultureInfo.DefaultThreadCurrentCulture = _defaultCulture;
            CultureInfo.DefaultThreadCurrentUICulture = _defaultUiCulture;
            CultureInfo.CurrentCulture = _culture;
            CultureInfo.CurrentUICulture = _uiCulture;
        }
    }
}
