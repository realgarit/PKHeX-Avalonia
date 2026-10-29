using Avalonia.Headless.XUnit;
using System.Collections.Generic;
using System.Threading.Tasks;
using Moq;
using PKHeX.Application.Shortcuts;
using PKHeX.Avalonia.Tests.Harness;
using PKHeX.Presentation.Localization;
using PKHeX.Presentation.ViewModels;

namespace PKHeX.Avalonia.Tests.Shortcuts;

/// <summary>
/// The shortcut editor dialog view model (issue #224): every recorded chord is validated against
/// the platform rules and the whole draft, nothing is stored before Save, and conflicts move a
/// shortcut only after the user confirms.
/// </summary>
public sealed class KeyboardShortcutsViewModelTests
{
    private const ShortcutModifiers Ctrl = ShortcutModifiers.Control;
    private const ShortcutModifiers Shift = ShortcutModifiers.Shift;
    private const ShortcutModifiers Alt = ShortcutModifiers.Alt;
    private const string Encounter = "OpenEncounterDatabase";

    private static LocalizedStrings L => LocalizedStrings.Instance;

    // ---------------------------------------------------------------------
    // Conflicts
    // ---------------------------------------------------------------------

    [Fact]
    public void Conflict_KeepsTheDraft_UntilReassignMovesTheShortcut()
    {
        var settings = new AppSettings();
        var store = new FakeSettingsStore();
        var (main, editor, _) = ShortcutTestViewModels.CreateEditor(settings, store);
        var encounter = Row(editor, Encounter);
        var launcher = Row(editor, ShortcutActionId.OpenToolLauncher);
        var ctrlK = new ShortcutChord(Ctrl, "K");

        encounter.RecordCommand.Execute(ctrlK);

        // The conflict names the action that holds the chord and changes nothing yet.
        Assert.Equal(new ShortcutChord(Ctrl, "N"), encounter.Chord);
        Assert.Equal(ctrlK, launcher.Chord);
        Assert.True(encounter.HasPendingConflict);
        Assert.True(encounter.ReassignCommand.CanExecute(null));
        Assert.Equal(L.Format("Shortcuts_Conflict", "Ctrl+K", launcher.Title), encounter.Message);
        Assert.Contains(AccessKeyText.Strip(L["Launcher_Title"]), encounter.Message);
        Assert.False(editor.HasChanges);

        encounter.ReassignCommand.Execute(null);

        Assert.Equal(ctrlK, encounter.Chord);
        Assert.Equal(L.Format("Shortcuts_Assigned", "Ctrl+K"), encounter.Message);
        Assert.False(encounter.HasPendingConflict);
        Assert.False(encounter.ReassignCommand.CanExecute(null));
        Assert.Null(launcher.Chord);
        Assert.Equal(L.Format("Shortcuts_Moved", encounter.Title), launcher.Message);
        Assert.Null(store.Saved);

        editor.SaveCommand.Execute(null);

        Assert.Equal(1, store.SaveCount);
        Assert.Equal("Primary+K", store.Saved!.KeyboardShortcuts[Encounter]);
        Assert.Equal(string.Empty, store.Saved.KeyboardShortcuts[ShortcutActionId.OpenToolLauncher]);
        // The main window rebinds from the service's Changed event.
        Assert.Equal(ctrlK, main.Shortcuts[Encounter].Shortcut);
        Assert.Null(main.Shortcuts.OpenToolLauncher.Shortcut);
    }

    [Fact]
    public void ResetRow_RestoresTheDefault_ThroughTheSameConflictCheck()
    {
        var (_, editor, _) = ShortcutTestViewModels.CreateEditor(new AppSettings(), new FakeSettingsStore());
        var encounter = Row(editor, Encounter);
        var database = Row(editor, "OpenPKMDatabase");
        var ctrlN = new ShortcutChord(Ctrl, "N");

        encounter.RecordCommand.Execute(new ShortcutChord(ShortcutModifiers.None, "F5"));
        Assert.True(encounter.IsCustomized);
        Assert.True(encounter.ResetCommand.CanExecute(null));
        encounter.ResetCommand.Execute(null);
        Assert.Equal(ctrlN, encounter.Chord);
        Assert.False(encounter.IsCustomized);
        Assert.False(encounter.ResetCommand.CanExecute(null));

        // Move Ctrl+N away, then restore it: the default is held by another action now.
        database.RecordCommand.Execute(ctrlN);
        database.ReassignCommand.Execute(null);
        Assert.Null(encounter.Chord);
        Assert.Equal(ctrlN, database.Chord);

        encounter.ResetCommand.Execute(null);
        Assert.Null(encounter.Chord);
        Assert.True(encounter.HasPendingConflict);
        Assert.Equal(L.Format("Shortcuts_Conflict", "Ctrl+N", database.Title), encounter.Message);

        encounter.ReassignCommand.Execute(null);
        Assert.Equal(ctrlN, encounter.Chord);
        Assert.Null(database.Chord);
    }

    // ---------------------------------------------------------------------
    // Rejected chords
    // ---------------------------------------------------------------------

    [Fact]
    public void UnusableChords_ExplainWhy_AndLeaveTheRowUnchanged()
    {
        var (_, editor, _) = ShortcutTestViewModels.CreateEditor(new AppSettings(), new FakeSettingsStore());
        var encounter = Row(editor, Encounter);
        var ctrlN = new ShortcutChord(Ctrl, "N");

        encounter.RecordCommand.Execute(new ShortcutChord(Ctrl, "C"));
        Assert.Equal(L.Format("Shortcuts_Error_Reserved", "Ctrl+C", L["Shortcuts_Reserved_TextEditing"]), encounter.Message);
        Assert.Equal(ctrlN, encounter.Chord);

        encounter.RecordCommand.Execute(new ShortcutChord(ShortcutModifiers.None, "N"));
        Assert.Equal(L.Format("Shortcuts_Error_NeedsModifier", "Ctrl"), encounter.Message);

        encounter.RecordCommand.Execute(new ShortcutChord(Ctrl | Alt, "E"));
        Assert.Equal(L["Shortcuts_Error_AltGr"], encounter.Message);

        // A key outside the vocabulary (as the recorder reports it by enum name).
        encounter.RecordCommand.Execute(new ShortcutChord(Ctrl, "OemPlus"));
        Assert.Equal(L["Shortcuts_Error_UnsupportedKey"], encounter.Message);

        Assert.Equal(ctrlN, encounter.Chord);
        Assert.False(encounter.HasPendingConflict);
        Assert.False(editor.HasChanges);

        // Recording the current chord again clears the message without a change.
        encounter.RecordCommand.Execute(ctrlN);
        Assert.False(encounter.HasMessage);
        Assert.False(editor.HasChanges);

        // A bare function key needs no modifier.
        encounter.RecordCommand.Execute(new ShortcutChord(ShortcutModifiers.None, "F5"));
        Assert.Equal(new ShortcutChord(ShortcutModifiers.None, "F5"), encounter.Chord);
        Assert.True(editor.HasChanges);
    }

    [Fact]
    public void Clear_RemovesTheShortcut_AndSaveStoresTheRemovedDefault()
    {
        var store = new FakeSettingsStore();
        var (_, editor, _) = ShortcutTestViewModels.CreateEditor(new AppSettings(), store);
        var encounter = Row(editor, Encounter);
        Assert.Equal(L.Format("Shortcuts_CustomizedCount", 0), editor.CustomizedSummary);
        Assert.True(encounter.ClearCommand.CanExecute(null));

        encounter.ClearCommand.Execute(null);

        Assert.Null(encounter.Chord);
        Assert.False(encounter.HasShortcut);
        Assert.Equal(L["Shortcuts_None"], encounter.ShortcutText);
        Assert.False(encounter.ClearCommand.CanExecute(null));
        Assert.True(encounter.IsCustomized);
        Assert.Equal(L.Format("Shortcuts_CustomizedCount", 1), editor.CustomizedSummary);

        editor.SaveCommand.Execute(null);
        Assert.Equal(string.Empty, store.Saved!.KeyboardShortcuts[Encounter]);
    }

    // ---------------------------------------------------------------------
    // Cancel, Save and the editor session
    // ---------------------------------------------------------------------

    [Fact]
    public void Cancel_DiscardsTheDraft()
    {
        var settings = new AppSettings();
        var store = new FakeSettingsStore();
        var (main, editor, service) = ShortcutTestViewModels.CreateEditor(settings, store);
        var closed = 0;
        editor.CloseRequested = () => closed++;
        Assert.True(service.IsEditorOpen);

        Row(editor, Encounter).RecordCommand.Execute(new ShortcutChord(ShortcutModifiers.None, "F5"));
        editor.CancelCommand.Execute(null);

        Assert.Equal(1, closed);
        Assert.Null(store.Saved);
        Assert.Empty(settings.KeyboardShortcuts);
        Assert.False(service.IsEditorOpen);
        Assert.Equal(new ShortcutChord(Ctrl, "N"), main.Shortcuts[Encounter].Shortcut);
    }

    [Fact]
    public void Save_WithoutChanges_StoresNothing_AndCloses()
    {
        var store = new FakeSettingsStore();
        var (_, editor, service) = ShortcutTestViewModels.CreateEditor(new AppSettings(), store);
        var closed = false;
        editor.CloseRequested = () => closed = true;

        editor.SaveCommand.Execute(null);

        Assert.True(closed);
        Assert.Equal(0, store.SaveCount);
        Assert.False(service.IsEditorOpen);
    }

    [Fact]
    public void Cancel_IsUnavailableWhileARowRecords()
    {
        var (_, editor, _) = ShortcutTestViewModels.CreateEditor(new AppSettings(), new FakeSettingsStore());
        var row = Row(editor, Encounter);
        var changes = new List<string?>();
        editor.PropertyChanged += (_, e) => changes.Add(e.PropertyName);
        Assert.True(editor.CancelCommand.CanExecute(null));

        row.IsRecording = true;
        Assert.True(editor.IsRecording);
        Assert.False(editor.CancelCommand.CanExecute(null));
        Assert.Contains(nameof(KeyboardShortcutsViewModel.IsRecording), changes);

        row.IsRecording = false;
        Assert.False(editor.IsRecording);
        Assert.True(editor.CancelCommand.CanExecute(null));
    }

    [Fact]
    public void Dispose_ReleasesTheEditorFlag()
    {
        var (_, editor, service) = ShortcutTestViewModels.CreateEditor(new AppSettings(), new FakeSettingsStore());
        Assert.True(service.IsEditorOpen);

        editor.Dispose();
        Assert.False(service.IsEditorOpen);

        editor.Dispose();
        Assert.False(service.IsEditorOpen);
    }

    // ---------------------------------------------------------------------
    // Reset all
    // ---------------------------------------------------------------------

    [Fact]
    public async Task ResetAll_AsksFirst_ThenRestoresEveryDefault()
    {
        var settings = new AppSettings();
        settings.KeyboardShortcuts[Encounter] = "Primary+Shift+N";
        settings.KeyboardShortcuts["OpenBoxReport"] = string.Empty;
        settings.KeyboardShortcuts["FutureAction"] = "Primary+J";
        var store = new FakeSettingsStore();
        var dialogs = new RecordingDialogService();
        var (_, editor, _) = ShortcutTestViewModels.CreateEditor(settings, store, dialogs);
        var encounter = Row(editor, Encounter);
        var boxReport = Row(editor, "OpenBoxReport");
        Assert.Equal(new ShortcutChord(Ctrl | Shift, "N"), encounter.Chord);
        Assert.Null(boxReport.Chord);
        Assert.Equal(L.Format("Shortcuts_CustomizedCount", 2), editor.CustomizedSummary);

        dialogs.ConfirmResult = false;
        await editor.ResetAllCommand.ExecuteAsync(null);
        var (title, message) = Assert.Single(dialogs.Confirmations);
        Assert.Equal(L["Shortcuts_ResetAll"], title);
        Assert.Equal(L["Shortcuts_ResetAll_Confirm"], message);
        Assert.Equal(new ShortcutChord(Ctrl | Shift, "N"), encounter.Chord);
        Assert.False(editor.HasChanges);

        dialogs.ConfirmResult = true;
        await editor.ResetAllCommand.ExecuteAsync(null);
        Assert.Equal(new ShortcutChord(Ctrl, "N"), encounter.Chord);
        Assert.Equal(new ShortcutChord(Ctrl, "R"), boxReport.Chord);
        Assert.All(editor.Rows, row => Assert.False(row.IsCustomized));
        Assert.Equal(L.Format("Shortcuts_CustomizedCount", 0), editor.CustomizedSummary);

        editor.SaveCommand.Execute(null);
        var stored = store.Saved!.KeyboardShortcuts;
        Assert.DoesNotContain(Encounter, stored.Keys);
        Assert.DoesNotContain("OpenBoxReport", stored.Keys);
        Assert.Equal("Primary+J", stored["FutureAction"]);
    }

    // ---------------------------------------------------------------------
    // Search and display
    // ---------------------------------------------------------------------

    [Fact]
    public void Search_MatchesTitleMenuOrShortcut()
    {
        var (_, editor, _) = ShortcutTestViewModels.CreateEditor(new AppSettings(), new FakeSettingsStore());
        var encounter = Row(editor, Encounter);
        var open = Row(editor, ShortcutActionId.OpenFile);
        Assert.Equal(editor.Rows.Count, editor.FilteredRows.Count);

        editor.SearchText = encounter.Title.ToUpperInvariant();
        Assert.Contains(encounter, editor.FilteredRows);
        Assert.DoesNotContain(open, editor.FilteredRows);

        editor.SearchText = "Ctrl+N";
        Assert.Contains(encounter, editor.FilteredRows);
        Assert.DoesNotContain(open, editor.FilteredRows);

        editor.SearchText = AccessKeyText.Strip(L["Menu_File"]);
        Assert.Contains(open, editor.FilteredRows);

        // A tool listed under two names is found by either: Box Layout is Block Layout in Gen 4.
        var boxLayout = Row(editor, "OpenBoxLayout");
        Assert.Equal(AccessKeyText.Strip(L["Menu_Save_BoxLayout"]), boxLayout.Title);
        editor.SearchText = AccessKeyText.Strip(L["Menu_Gen4_BlockLayout"]);
        Assert.Contains(boxLayout, editor.FilteredRows);
        editor.SearchText = "Block";
        Assert.Contains(boxLayout, editor.FilteredRows);

        editor.SearchText = "no action has this text";
        Assert.Empty(editor.FilteredRows);

        editor.SearchText = string.Empty;
        Assert.Equal(editor.Rows, editor.FilteredRows);
    }

    [Fact]
    public void Rows_FollowTheCatalogOrder_AndDescribeThemselves()
    {
        var (main, editor, _) = ShortcutTestViewModels.CreateEditor(new AppSettings(), new FakeSettingsStore());

        Assert.Equal(main.Shortcuts.All.Select(action => action.Id), editor.Rows.Select(row => row.ActionId));

        var encounter = Row(editor, Encounter);
        Assert.Equal("Ctrl+N", encounter.ShortcutText);
        Assert.Equal(L.Format("Shortcuts_Default", "Ctrl+N"), encounter.DefaultText);
        Assert.Equal(L.Format("Shortcuts_RecorderName", encounter.Title, "Ctrl+N"), encounter.RecorderAutomationName);
        Assert.Equal(L.Format("Shortcuts_ClearFor", encounter.Title), encounter.ClearAutomationName);
        Assert.Equal(L.Format("Shortcuts_ResetFor", encounter.Title), encounter.ResetAutomationName);

        var close = Row(editor, ShortcutActionId.CloseFile);
        Assert.Null(close.Default);
        Assert.Equal(L["Shortcuts_None"], close.ShortcutText);
        Assert.Equal(L.Format("Shortcuts_Default", L["Shortcuts_None"]), close.DefaultText);
        Assert.False(close.ResetCommand.CanExecute(null));
    }

    [Fact]
    public void StoredIssues_AreShownWhenTheEditorOpens()
    {
        var settings = new AppSettings();
        settings.KeyboardShortcuts["OpenBoxReport"] = "Hyper+R";
        settings.KeyboardShortcuts["OpenMisc3"] = "Primary+N";
        var store = new FakeSettingsStore();
        var (_, editor, _) = ShortcutTestViewModels.CreateEditor(settings, store);
        var boxReport = Row(editor, "OpenBoxReport");
        var encounter = Row(editor, Encounter);
        var misc3 = Row(editor, "OpenMisc3");

        Assert.Equal(L.Format("Shortcuts_Issue_InvalidStored", "Hyper+R"), boxReport.Message);
        Assert.Equal(new ShortcutChord(Ctrl, "R"), boxReport.Chord);
        Assert.Equal(L.Format("Shortcuts_Issue_SuppressedDefault", "Ctrl+N", misc3.QualifiedTitle), encounter.Message);
        Assert.Null(encounter.Chord);
        Assert.Equal(new ShortcutChord(Ctrl, "N"), misc3.Chord);

        // Save stores what the editor shows: the suppressed default stays off, and the stored text
        // the editor could not read is kept for a build that can.
        editor.SaveCommand.Execute(null);
        Assert.Equal(string.Empty, store.Saved!.KeyboardShortcuts[Encounter]);
        Assert.Equal("Hyper+R", store.Saved.KeyboardShortcuts["OpenBoxReport"]);
        Assert.Equal("Primary+N", store.Saved.KeyboardShortcuts["OpenMisc3"]);
    }

    [Fact]
    public void DuplicateStoredShortcuts_AreShownInactive_AndSaveResolvesThem()
    {
        var settings = new AppSettings();
        settings.KeyboardShortcuts["OpenMisc3"] = "Primary+Shift+J";
        settings.KeyboardShortcuts["OpenMisc4"] = "Primary+Shift+J";
        var store = new FakeSettingsStore();
        var (main, editor, _) = ShortcutTestViewModels.CreateEditor(settings, store);
        var misc3 = Row(editor, "OpenMisc3");
        var misc4 = Row(editor, "OpenMisc4");

        Assert.Null(misc3.Chord);
        Assert.Null(misc4.Chord);
        Assert.Equal(L.Format("Shortcuts_Issue_DuplicateStored", "Ctrl+Shift+J", misc4.QualifiedTitle), misc3.Message);
        Assert.Equal(L.Format("Shortcuts_Issue_DuplicateStored", "Ctrl+Shift+J", misc3.QualifiedTitle), misc4.Message);

        // Keep one of them; Save removes the other so the stored state matches the editor.
        misc3.RecordCommand.Execute(new ShortcutChord(Ctrl | Shift, "J"));
        Assert.Equal(new ShortcutChord(Ctrl | Shift, "J"), misc3.Chord);
        // The other row no longer says that neither action uses the chord.
        Assert.Equal(L.Format("Shortcuts_Issue_UsedBy", "Ctrl+Shift+J", misc3.QualifiedTitle), misc4.Message);
        editor.SaveCommand.Execute(null);

        Assert.Equal("Primary+Shift+J", store.Saved!.KeyboardShortcuts["OpenMisc3"]);
        Assert.DoesNotContain("OpenMisc4", store.Saved.KeyboardShortcuts.Keys);
        Assert.Equal(new ShortcutChord(Ctrl | Shift, "J"), main.Shortcuts["OpenMisc3"].Shortcut);
        Assert.Null(main.Shortcuts["OpenMisc4"].Shortcut);
    }

    // ---------------------------------------------------------------------
    // Stored shortcuts that cannot be used
    // ---------------------------------------------------------------------

    [Fact]
    public void UnusableStoredShortcut_IsShownInDisplayForm_AndOnlyClaimsADefaultWhenThereIsOne()
    {
        var settings = new AppSettings();
        // Cmd+Option+J from macOS is Ctrl+Alt+J (AltGr) here; Gen 3 Misc has no default.
        settings.KeyboardShortcuts["OpenMisc3"] = "Primary+Alt+J";
        // Reserved for text editing; Undo keeps its default.
        settings.KeyboardShortcuts[ShortcutActionId.Undo] = "Primary+C";
        // Hand-edited text that does not parse; the Box Report keeps its default.
        settings.KeyboardShortcuts["OpenBoxReport"] = "Hyper+R";
        // Does not parse, and Close has no default.
        settings.KeyboardShortcuts[ShortcutActionId.CloseFile] = "Hyper+Q";
        var (_, editor, _) = ShortcutTestViewModels.CreateEditor(settings, new FakeSettingsStore());
        var misc3 = Row(editor, "OpenMisc3");

        Assert.Null(misc3.Chord);
        Assert.Equal(L.Format("Shortcuts_Issue_InvalidStored_NoDefault", "Ctrl+Alt+J"), misc3.Message);
        Assert.DoesNotContain("Primary", misc3.Message);

        var undo = Row(editor, ShortcutActionId.Undo);
        Assert.Equal(new ShortcutChord(Ctrl, "Z"), undo.Chord);
        Assert.Equal(L.Format("Shortcuts_Issue_InvalidStored", "Ctrl+C"), undo.Message);

        Assert.Equal(L.Format("Shortcuts_Issue_InvalidStored", "Hyper+R"), Row(editor, "OpenBoxReport").Message);
        Assert.Equal(L.Format("Shortcuts_Issue_InvalidStored_NoDefault", "Hyper+Q"), Row(editor, ShortcutActionId.CloseFile).Message);
    }

    // ---------------------------------------------------------------------
    // Messages about another row follow that row
    // ---------------------------------------------------------------------

    [Fact]
    public void SuppressedDefaultMessage_FollowsTheRowThatHoldsTheChord()
    {
        var settings = new AppSettings();
        settings.KeyboardShortcuts["OpenMisc3"] = "Primary+N";
        var store = new FakeSettingsStore();
        var (_, editor, _) = ShortcutTestViewModels.CreateEditor(settings, store);
        var encounter = Row(editor, Encounter);
        var misc3 = Row(editor, "OpenMisc3");
        var boxReport = Row(editor, "OpenBoxReport");
        var ctrlN = new ShortcutChord(Ctrl, "N");
        Assert.Equal(L.Format("Shortcuts_Issue_SuppressedDefault", "Ctrl+N", misc3.QualifiedTitle), encounter.Message);

        // Another row takes Ctrl+N from Gen 3 Misc: the message names the new holder.
        boxReport.RecordCommand.Execute(ctrlN);
        boxReport.ReassignCommand.Execute(null);
        Assert.Equal(ctrlN, boxReport.Chord);
        Assert.Equal(L.Format("Shortcuts_Issue_SuppressedDefault", "Ctrl+N", boxReport.QualifiedTitle), encounter.Message);
        Assert.Equal(L.Format("Shortcuts_Moved", boxReport.QualifiedTitle), misc3.Message);

        // The holder lets it go: the default is free, and the message no longer blames anyone.
        boxReport.ClearCommand.Execute(null);
        Assert.Equal(L["Shortcuts_Removed"], boxReport.Message);
        Assert.Equal(L.Format("Shortcuts_Issue_DefaultFree", "Ctrl+N"), encounter.Message);
        Assert.Equal(L.Format("Shortcuts_Issue_Free", "Ctrl+N"), misc3.Message);
        Assert.True(encounter.ResetCommand.CanExecute(null));

        encounter.ResetCommand.Execute(null);
        Assert.Equal(ctrlN, encounter.Chord);
        Assert.Equal(L.Format("Shortcuts_Assigned", "Ctrl+N"), encounter.Message);
        Assert.Equal(L.Format("Shortcuts_Issue_UsedBy", "Ctrl+N", encounter.QualifiedTitle), misc3.Message);

        editor.SaveCommand.Execute(null);
        Assert.DoesNotContain(Encounter, store.Saved!.KeyboardShortcuts.Keys);
    }

    [Fact]
    public void DuplicateMessage_BecomesFree_WhenThePartnerTakesAnotherShortcut()
    {
        var settings = new AppSettings();
        settings.KeyboardShortcuts["OpenMisc3"] = "Primary+Shift+J";
        settings.KeyboardShortcuts["OpenMisc4"] = "Primary+Shift+J";
        var (_, editor, _) = ShortcutTestViewModels.CreateEditor(settings, new FakeSettingsStore());
        var misc3 = Row(editor, "OpenMisc3");
        var misc4 = Row(editor, "OpenMisc4");

        misc3.RecordCommand.Execute(new ShortcutChord(ShortcutModifiers.None, "F9"));

        Assert.Equal(L.Format("Shortcuts_Assigned", "F9"), misc3.Message);
        Assert.Equal(L.Format("Shortcuts_Issue_Free", "Ctrl+Shift+J"), misc4.Message);
    }

    [Fact]
    public void DefaultBlockedByUnresolvedDuplicates_NamesThemUntilOneMovesOn()
    {
        var settings = new AppSettings();
        settings.KeyboardShortcuts["OpenMisc3"] = "Primary+N";
        settings.KeyboardShortcuts["OpenMisc4"] = "Primary+N";
        var (_, editor, _) = ShortcutTestViewModels.CreateEditor(settings, new FakeSettingsStore());
        var encounter = Row(editor, Encounter);
        var misc3 = Row(editor, "OpenMisc3");
        var misc4 = Row(editor, "OpenMisc4");
        Assert.Equal(L.Format("Shortcuts_Issue_SuppressedDefault", "Ctrl+N", misc3.QualifiedTitle), encounter.Message);
        Assert.Equal(L.Format("Shortcuts_Issue_DuplicateStored", "Ctrl+N", misc4.QualifiedTitle), misc3.Message);

        misc4.RecordCommand.Execute(new ShortcutChord(ShortcutModifiers.None, "F9"));

        Assert.Equal(L.Format("Shortcuts_Issue_Free", "Ctrl+N"), misc3.Message);
        Assert.Equal(L.Format("Shortcuts_Issue_DefaultFree", "Ctrl+N"), encounter.Message);
    }

    [Fact]
    public void MovedMessage_FollowsTheShortcut()
    {
        var (_, editor, _) = ShortcutTestViewModels.CreateEditor(new AppSettings(), new FakeSettingsStore());
        var encounter = Row(editor, Encounter);
        var launcher = Row(editor, ShortcutActionId.OpenToolLauncher);

        encounter.RecordCommand.Execute(new ShortcutChord(Ctrl, "K"));
        encounter.ReassignCommand.Execute(null);
        Assert.Equal(L.Format("Shortcuts_Moved", encounter.QualifiedTitle), launcher.Message);

        encounter.RecordCommand.Execute(new ShortcutChord(ShortcutModifiers.None, "F9"));
        Assert.Equal(L.Format("Shortcuts_Issue_Free", "Ctrl+K"), launcher.Message);

        // The launcher's own edit replaces the tracked message.
        launcher.ResetCommand.Execute(null);
        Assert.Equal(L.Format("Shortcuts_Assigned", "Ctrl+K"), launcher.Message);
        encounter.ClearCommand.Execute(null);
        Assert.Equal(L.Format("Shortcuts_Assigned", "Ctrl+K"), launcher.Message);
    }

    // ---------------------------------------------------------------------
    // Actions that share a title
    // ---------------------------------------------------------------------

    public static IEnumerable<object[]> Languages => LocalizedStrings.SupportedLanguages.Select(language => new object[] { language });

    [AvaloniaTheory]
    [MemberData(nameof(Languages))]
    public void SharedTitles_AreQualifiedByTheirMenus_InMessagesAndAccessibleNames(string language)
    {
        var previous = L.CurrentLanguage;
        KeyboardShortcutsViewModel? editor = null;
        try
        {
            var settings = new AppSettings();
            var store = new FakeSettingsStore();
            var service = new KeyboardShortcutService(settings, store, StandardShortcutPlatform.Instance);
            // The main view model applies its own startup language, so switch after building it.
            var main = ShortcutTestViewModels.CreateMainViewModel(settings, store, service);
            L.SetLanguage(language);
            editor = new KeyboardShortcutsViewModel(main.Shortcuts, service, new Mock<IDialogService>().Object);

            // Every action is named once, in messages and in accessible names alike.
            AssertDistinct(editor.Rows.Select(row => row.QualifiedTitle));
            AssertDistinct(editor.Rows.Select(row => row.RecorderAutomationName));
            AssertDistinct(editor.Rows.Select(row => row.ClearAutomationName));
            AssertDistinct(editor.Rows.Select(row => row.ResetAutomationName));
            AssertDistinct(editor.Rows.Select(row => row.ReassignAutomationName));

            var misc3 = Row(editor, "OpenMisc3");
            var misc4 = Row(editor, "OpenMisc4");
            Assert.Equal(misc3.Title, misc4.Title);
            Assert.Equal(L.Format("Shortcuts_TitleWithMenu", misc3.Title, misc3.CategoryTitle), misc3.QualifiedTitle);
            Assert.Contains(misc4.CategoryTitle, misc4.QualifiedTitle);
            Assert.Contains(misc3.QualifiedTitle, misc3.ClearAutomationName);

            // The File menu's Save and the Window menu's Save workspace both show "Save" in English.
            if (language == "en")
                Assert.NotEqual(Row(editor, ShortcutActionId.SaveFile).QualifiedTitle, Row(editor, ShortcutActionId.WorkspaceSave).QualifiedTitle);

            // A title no other action has stays as it is.
            var encounter = Row(editor, Encounter);
            Assert.Equal(encounter.Title, encounter.QualifiedTitle);

            // The conflict names the Gen 3 editor, never just "Misc".
            misc3.RecordCommand.Execute(new ShortcutChord(Ctrl | Shift, "J"));
            misc4.RecordCommand.Execute(new ShortcutChord(Ctrl | Shift, "J"));
            Assert.Equal(L.Format("Shortcuts_Conflict", "Ctrl+Shift+J", misc3.QualifiedTitle), misc4.Message);
            Assert.Contains(L["Shortcuts_Reassign"], misc4.ReassignAutomationName);
        }
        finally
        {
            editor?.Dispose();
            L.SetLanguage(previous);
        }
    }

    // ---------------------------------------------------------------------
    // Screen readers
    // ---------------------------------------------------------------------

    [Fact]
    public void RecorderName_CarriesTheCurrentShortcut_AndRecordingAnnouncesTheResult()
    {
        var (_, editor, _) = ShortcutTestViewModels.CreateEditor(new AppSettings(), new FakeSettingsStore());
        var encounter = Row(editor, Encounter);
        var changes = new List<string?>();
        encounter.PropertyChanged += (_, e) => changes.Add(e.PropertyName);
        Assert.Equal(L.Format("Shortcuts_RecorderName", encounter.Title, "Ctrl+N"), encounter.RecorderAutomationName);

        encounter.IsRecording = true;
        Assert.Equal(L.Format("Shortcuts_RecorderName", encounter.Title, L["Shortcuts_Recording"]), encounter.RecorderAutomationName);
        Assert.Contains(nameof(KeyboardShortcutRowViewModel.RecorderAutomationName), changes);

        changes.Clear();
        encounter.IsRecording = false;
        encounter.RecordCommand.Execute(new ShortcutChord(Ctrl | Shift, "E"));
        Assert.Equal(L.Format("Shortcuts_RecorderName", encounter.Title, "Ctrl+Shift+E"), encounter.RecorderAutomationName);
        Assert.Contains(nameof(KeyboardShortcutRowViewModel.RecorderAutomationName), changes);
        // The row's message is a polite live region: it now says what was set.
        Assert.Equal(L.Format("Shortcuts_Assigned", "Ctrl+Shift+E"), encounter.Message);

        encounter.ClearCommand.Execute(null);
        Assert.Equal(L.Format("Shortcuts_RecorderName", encounter.Title, L["Shortcuts_None"]), encounter.RecorderAutomationName);
        Assert.Equal(L["Shortcuts_Removed"], encounter.Message);
    }

    // ---------------------------------------------------------------------
    // Entry points
    // ---------------------------------------------------------------------

    [Fact]
    public async Task HelpCommand_OpensOneEditorAtATime()
    {
        var settings = new AppSettings();
        var store = new FakeSettingsStore();
        var service = new KeyboardShortcutService(settings, store, StandardShortcutPlatform.Instance);
        var windows = new NoopWindowService();
        var main = ShortcutTestViewModels.CreateMainViewModel(settings, store, service, windowService: windows);
        Assert.True(main.OpenKeyboardShortcutsCommand.CanExecute(null));

        await main.OpenKeyboardShortcutsCommand.ExecuteAsync(null);

        var (viewModel, title) = Assert.Single(windows.ShownDialogs);
        var editor = Assert.IsType<KeyboardShortcutsViewModel>(viewModel);
        Assert.Equal(L["Dialog_KeyboardShortcuts"], title);
        Assert.True(service.IsEditorOpen);

        // While it is open, a second request (for example its own shortcut) does not stack another.
        await main.OpenKeyboardShortcutsCommand.ExecuteAsync(null);
        Assert.Single(windows.ShownDialogs);

        editor.Dispose();
        await main.OpenKeyboardShortcutsCommand.ExecuteAsync(null);
        Assert.Equal(2, windows.ShownDialogs.Count);
    }

    [Fact]
    public async Task SettingsSection_OpensTheEditorThroughTheMainWindow()
    {
        var settings = new AppSettings();
        var store = new FakeSettingsStore();
        var service = new KeyboardShortcutService(settings, store, StandardShortcutPlatform.Instance);
        var windows = new NoopWindowService();
        var main = ShortcutTestViewModels.CreateMainViewModel(settings, store, service, windowService: windows);

        await main.OpenSettingsCommand.ExecuteAsync(null);
        var settingsVm = Assert.IsType<SettingsViewModel>(Assert.Single(windows.ShownDialogs).ViewModel);
        Assert.True(settingsVm.CanOpenKeyboardShortcuts);

        await settingsVm.OpenKeyboardShortcutsCommand.ExecuteAsync(null);

        Assert.IsType<KeyboardShortcutsViewModel>(windows.ShownDialogs[^1].ViewModel);
        Assert.Null(store.Saved);
    }

    [Fact]
    public void SettingsSection_IsHiddenWithoutAnEditor()
    {
        var settings = new AppSettings();
        var store = new FakeSettingsStore();
        var vm = new SettingsViewModel(settings, store, new Mock<IThemeService>().Object, new Mock<IUiDensityService>().Object,
            new LanguageService(), UpdateTestDoubles.Coordinator());

        Assert.False(vm.CanOpenKeyboardShortcuts);
        Assert.False(vm.OpenKeyboardShortcutsCommand.CanExecute(null));
    }

    private static KeyboardShortcutRowViewModel Row(KeyboardShortcutsViewModel editor, string id) =>
        Assert.Single(editor.Rows, row => row.ActionId == id);

    private static void AssertDistinct(IEnumerable<string> values)
    {
        var duplicates = values
            .GroupBy(value => value, StringComparer.CurrentCultureIgnoreCase)
            .Where(group => group.Count() > 1)
            .Select(group => group.Key)
            .ToArray();
        Assert.True(duplicates.Length == 0, "Not unique: " + string.Join(" | ", duplicates));
    }
}
