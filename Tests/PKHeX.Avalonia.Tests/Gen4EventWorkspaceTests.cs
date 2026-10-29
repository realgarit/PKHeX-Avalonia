using System.Buffers.Binary;
using System.Globalization;
using System.Security.Cryptography;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.VisualTree;
using CommunityToolkit.Mvvm.Messaging;
using PKHeX.Application.Models.Events;
using PKHeX.Application.Services;
using PKHeX.Avalonia.Tests.Fixtures;
using PKHeX.Avalonia.Tests.Harness;
using PKHeX.Avalonia.Views;
using PKHeX.Core;
using PKHeX.Presentation.Localization;
using PKHeX.Presentation.ViewModels;

namespace PKHeX.Avalonia.Tests;

public sealed class Gen4EventWorkspaceTests
{
    private static readonly string[] Files = ["gen4_diamond.sav", "gen4_platinum.sav", "gen4_heartgold.sav"];
    public static IEnumerable<object[]> Games => Files.Select(file => new object[] { file });
    public static IEnumerable<object[]> GameLanguages => from file in Files from language in LocalizedStrings.SupportedLanguages select new object[] { file, language };

    [Theory, MemberData(nameof(Games))]
    public void CatalogExposesEveryFlagAndUshortWorkWithItsOwnCoreGameMetadata(string file)
    {
        using var language = new LanguageScope("en");
        var save = Load(file);
        var before = Snapshot(save);
        var session = Assert.IsType<EventDataSession>(EventDataSession.Create(save));
        Assert.Equal(2912, session.Fields.Count(field => field.Kind == EventDataKind.Flag));
        Assert.Equal(save is SAV4HGSS ? 368 : 288, session.Fields.Count(field => field.Kind == EventDataKind.Work));
        Assert.DoesNotContain(session.Fields, field => field.Kind == EventDataKind.SystemFlag || field.SupportsFloatInterpretation);
        Assert.Equal(Enumerable.Range(0, save.EventFlagCount), session.Fields.Where(field => field.Kind == EventDataKind.Flag).Select(field => field.Index));
        Assert.Equal(Enumerable.Range(0, save.EventWorkCount), session.Fields.Where(field => field.Kind == EventDataKind.Work).Select(field => field.Index));
        var labels = Labels(save);
        foreach (var label in labels.Flag)
        {
            var field = Field(session, EventDataKind.Flag, label.Index);
            Assert.Equal(label.Name, field.Name);
            Assert.Equal(label.Type.ToString(), field.Category);
        }
        foreach (var label in labels.Work)
        {
            var field = Field(session, EventDataKind.Work, label.Index);
            Assert.Equal(label.Name, field.Name);
            Assert.Equal(label.PredefinedValues.Where(option => !option.IsCustom).Select(option => (option.Name, (long)option.Value)), field.Options.Select(option => (option.Name, option.Value)));
            Assert.Equal(0, field.Minimum);
            Assert.Equal(ushort.MaxValue, field.Maximum);
        }
        AssertSnapshot(save, before);
        Assert.False(save.State.Edited);
    }

    [Theory]
    [InlineData("gen4_diamond.sav", "Game Corner Bonus Rounds Streak (Non-Korean)", "Stark Mountain Quest Cleared")]
    [InlineData("gen4_platinum.sav", "Game Corner Bonus Rounds Streak (Japan and USA only)", "Stark Mountain Quest Cleared/Elite Four Rematch Unlocked")]
    public void NewDpAndPlatinumDescriptionsAreVisibleAtTheUpstreamIndices(string file, string workName, string starkName)
    {
        using var language = new LanguageScope("en");
        using var vm = new CategorizedEventEditorViewModel(Load(file), new RecordingDialogService());
        var work = Row(vm, EventDataKind.Work, 64);
        Assert.Equal(workName, work.Name);
        Assert.Equal("Statistic", work.CategoryKey);
        Assert.Equal(starkName, Row(vm, EventDataKind.Flag, 214).Name);
        Assert.Equal("Received TM64", Row(vm, EventDataKind.Flag, 304).Name);
        vm.SelectedBank = vm.Banks.Single(bank => bank.Kind == EventDataKind.Work);
        vm.SearchText = "Bonus Rounds";
        Assert.Contains(work, vm.VisibleRows);
        vm.SelectedCategory = vm.Categories.Single(category => category.Key == "Statistic");
        Assert.Contains(work, vm.VisibleRows);
        vm.SearchText = "0x0040";
        Assert.Contains(work, vm.VisibleRows);
    }

    [Theory, MemberData(nameof(Games))]
    public void ApplyChangesOnlyTheSelectedFlagBitsAndWorkWordThenNormalParserReopensThem(string file)
    {
        var path = SavePath(file);
        var fixtureHash = SHA256.HashData(File.ReadAllBytes(path));
        var save = Load(file);
        var before = Snapshot(save);
        using var vm = new CategorizedEventEditorViewModel(save, new RecordingDialogService());
        var stark = Row(vm, EventDataKind.Flag, 214);
        var tm = Row(vm, EventDataKind.Flag, 304);
        var work = Row(vm, EventDataKind.Work, 64);
        stark.IsSet = !stark.IsSet;
        tm.IsSet = !tm.IsSet;
        var targetWork = (ushort)(save.GetWork(64) ^ ushort.MaxValue);
        work.ValueText = targetWork.ToString(CultureInfo.InvariantCulture);
        AssertSnapshot(save, before);
        Assert.False(save.State.Edited);

        var expected = before.Select(buffer => buffer.ToArray()).ToArray();
        var (workOffset, flagOffset) = Offsets(save);
        expected[1][flagOffset + (214 >> 3)] ^= 1 << (214 & 7);
        expected[1][flagOffset + (304 >> 3)] ^= 1 << (304 & 7);
        BinaryPrimitives.WriteUInt16LittleEndian(expected[1].AsSpan(workOffset + (64 * 2)), targetWork);
        Assert.True(save.Data.Overlaps(save.General, out int generalOffset));
        expected[1].CopyTo(expected[0].AsSpan(generalOffset));
        vm.ApplyCommand.Execute(null);
        AssertSnapshot(save, expected);
        Assert.True(save.State.Edited);
        Assert.Equal(targetWork, save.GetWork(64));

        save.State.Edited = false;
        vm.ApplyCommand.Execute(null);
        AssertSnapshot(save, expected);
        Assert.False(save.State.Edited);

        var reopened = Assert.IsAssignableFrom<SAV4>(SaveUtil.GetSaveFile(save.Write(BinaryExportSetting.None)));
        Assert.Equal(save.GetType(), reopened.GetType());
        Assert.Equal(stark.IsSet, reopened.GetEventFlag(214));
        Assert.Equal(tm.IsSet, reopened.GetEventFlag(304));
        Assert.Equal(targetWork, reopened.GetWork(64));
        Assert.True(EventDataSession.TryCompare(Load(file), reopened, out var changed));
        Assert.Equal(3, changed.Count);
        Assert.Contains(changed, field => field.Kind == EventDataKind.Work && field.Index == 64 && field.After == targetWork);
        Assert.Equal(fixtureHash, SHA256.HashData(File.ReadAllBytes(path)));
    }

    [Theory, MemberData(nameof(Games))]
    public void ResetCancelAndUntouchedApplyPreserveDataGeneralPartyStorageAndEditedState(string file)
    {
        foreach (var initiallyEdited in new[] { false, true })
        {
            var save = Load(file);
            save.State.Edited = initiallyEdited;
            var before = Snapshot(save);
            using var vm = new CategorizedEventEditorViewModel(save, new RecordingDialogService());
            AssertSnapshot(save, before);
            Assert.Equal(initiallyEdited, save.State.Edited);
            vm.ApplyCommand.Execute(null);
            AssertSnapshot(save, before);
            Assert.Equal(initiallyEdited, save.State.Edited);

            Row(vm, EventDataKind.Flag, 214).IsSet = !save.GetEventFlag(214);
            Row(vm, EventDataKind.Work, 64).ValueText = (save.GetWork(64) ^ ushort.MaxValue).ToString(CultureInfo.InvariantCulture);
            AssertSnapshot(save, before);
            vm.ResetCommand.Execute(null);
            vm.ApplyCommand.Execute(null);
            AssertSnapshot(save, before);
            Assert.Equal(initiallyEdited, save.State.Edited);

            Row(vm, EventDataKind.Flag, 304).IsSet = !save.GetEventFlag(304);
            Row(vm, EventDataKind.Work, 64).ValueText = "65536";
            Assert.False(vm.CanApply);
            vm.ApplyCommand.Execute(null);
            AssertSnapshot(save, before);
            bool closed = false;
            vm.CloseRequested = () => closed = true;
            vm.CancelCommand.Execute(null);
            Assert.True(closed);
            AssertSnapshot(save, before);
            Assert.Equal(initiallyEdited, save.State.Edited);
        }
    }

    [Theory, MemberData(nameof(Games))]
    public void RawEntriesStayReachableAndUnsignedBoundsCannotOverflowIntoOtherFields(string file)
    {
        var save = Load(file);
        var before = Snapshot(save);
        using var vm = new CategorizedEventEditorViewModel(save, new RecordingDialogService());
        vm.SelectedBank = vm.Banks.Single(bank => bank.Kind == EventDataKind.Work);
        var raw = vm.Rows.Last(row => row.Kind == EventDataKind.Work && row.CategoryKey == "None");
        vm.SearchText = "no matching field";
        vm.SelectedIndex = raw.Index;
        Assert.Same(raw, vm.SelectedRow);
        Assert.Contains(raw, vm.VisibleRows);
        Assert.Equal(LocalizedStrings.Instance.Format("Events_RawEntry", raw.Index), raw.Name);
        foreach (var invalid in new[] { "-1", "65536", "0x10000", "9223372036854775808", "not a number" })
        {
            raw.ValueText = invalid;
            Assert.True(raw.HasErrors);
            Assert.False(vm.ApplyCommand.CanExecute(null));
            vm.ApplyCommand.Execute(null);
            AssertSnapshot(save, before);
        }
        raw.ValueText = save.GetWork(raw.Index) == ushort.MaxValue ? "0" : "0xFFFF";
        Assert.False(raw.HasErrors);
        var expected = raw.DisplayValue;
        vm.ApplyCommand.Execute(null);
        var reopened = Assert.IsAssignableFrom<SAV4>(SaveUtil.GetSaveFile(save.Write(BinaryExportSetting.None)));
        Assert.Equal(expected, reopened.GetWork(raw.Index).ToString(CultureInfo.InvariantCulture));
        vm.SelectedIndex = int.MaxValue;
        Assert.Equal(save.EventWorkCount - 1, vm.SelectedIndex);
        vm.SelectedIndex = -1;
        Assert.Equal(0, vm.SelectedIndex);
    }

    [Theory, MemberData(nameof(Games))]
    public void CommitPreservesIndependentLiveChangesAndUnlabeledValues(string file)
    {
        var save = Load(file);
        var session = Assert.IsType<EventDataSession>(EventDataSession.Create(save));
        var flag = Field(session, EventDataKind.Flag, 214);
        flag.TrySetValue(save.GetEventFlag(214) ? 0 : 1);
        var raw = session.Fields.Last(field => field.Kind == EventDataKind.Work && field.Name.Length == 0);
        var rawValue = (ushort)(raw.Value ^ ushort.MaxValue);
        save.SetWork(raw.Index, rawValue); // Independent source edit while the workspace is open.
        save.Money = save.Money == 12345 ? 54321u : 12345u;
        var expected = Snapshot(save);
        var (_, flagOffset) = Offsets(save);
        expected[1][flagOffset + (214 >> 3)] ^= 1 << (214 & 7);
        Assert.True(save.Data.Overlaps(save.General, out int generalOffset));
        expected[1].CopyTo(expected[0].AsSpan(generalOffset));
        Assert.Equal(1, session.Commit());
        AssertSnapshot(save, expected);
        Assert.Equal(rawValue, save.GetWork(raw.Index));
    }

    [AvaloniaTheory, MemberData(nameof(GameLanguages))]
    public void LanguageRefreshUsesTheGameResourcesAndRetainsSelectionDraftsAndValidation(string file, string language)
    {
        using var scope = new LanguageScope("en");
        var save = Load(file);
        var before = Snapshot(save);
        using var vm = new CategorizedEventEditorViewModel(save, new RecordingDialogService());
        vm.SelectedBank = vm.Banks.Single(bank => bank.Kind == EventDataKind.Work);
        vm.SelectedIndex = 64;
        var row = vm.SelectedRow!;
        row.ValueText = "65536";
        var starter = Row(vm, EventDataKind.Work, 48);
        starter.ValueText = "65535";
        vm.SelectedCategory = vm.Categories.Single(category => category.Key == row.CategoryKey);
        vm.SearchText = row.HexIndex;
        var category = vm.SelectedCategory.Key;
        scope.SetLanguage(language);
        Assert.Same(row, vm.SelectedRow);
        Assert.Equal(64, vm.SelectedIndex);
        Assert.Equal(EventDataKind.Work, vm.SelectedBank.Kind);
        Assert.Equal(category, vm.SelectedCategory!.Key);
        Assert.Equal(row.HexIndex, vm.SearchText);
        Assert.Equal("65536", row.ValueText);
        Assert.Equal("65535", starter.ValueText);
        Assert.True(row.HasErrors);
        Assert.Equal(LocalizedStrings.Instance["Events_InvalidValue"], row.ErrorText);
        Assert.False(vm.CanApply);
        var labels = Labels(save);
        Assert.Equal(labels.Work.Single(label => label.Index == 48).Name, starter.Name);
        var workLabel = labels.Work.SingleOrDefault(label => label.Index == 64);
        Assert.Equal(workLabel?.Name ?? LocalizedStrings.Instance.Format("Events_RawEntry", 64), row.Name);
        Assert.Equal(LocalizedStrings.Instance[$"Events_Category_{row.CategoryKey}"], row.Category);
        AssertSnapshot(save, before);
        Assert.False(save.State.Edited);
        vm.ResetCommand.Execute(null);
        Assert.False(row.HasErrors);
        vm.ApplyCommand.Execute(null);
        AssertSnapshot(save, before);
        Assert.False(save.State.Edited);
        vm.Dispose();
        Assert.False(WeakReferenceMessenger.Default.IsRegistered<LanguageChangedMessage>(vm));
    }

    [AvaloniaTheory, MemberData(nameof(Games))]
    public void MainShellUsesTheExistingCategorizedEventsWorkspaceForOriginalDsSaves(string file)
    {
        using var app = new HeadlessAppFixture();
        app.LoadSaveInstance(Load(file));
        app.ViewModel.SelectSaveNavigationCommand.Execute(null);
        app.Pump();
        var vm = Assert.IsType<CategorizedEventEditorViewModel>(app.ViewModel.EventFlagsEditor);
        Assert.True(app.ViewModel.IsEventsWorkspace);
        Assert.Contains(app.Window.GetVisualDescendants(), control => control is CategorizedEventEditor && control.IsEffectivelyVisible);
        vm.SelectedBank = vm.Banks.Single(bank => bank.Kind == EventDataKind.Work);
        vm.SelectedIndex = 64;
        app.Pump();
        var view = app.Window.GetVisualDescendants().OfType<CategorizedEventEditor>().Single(control => control.IsEffectivelyVisible);
        Assert.True(view.FindControl<DataGrid>("EventGrid")!.Bounds.Height > 60);
        app.LoadSaveInstance(new SAV6XY());
        Assert.IsType<EventFlagsEditorViewModel>(app.ViewModel.EventFlagsEditor);
        Assert.False(WeakReferenceMessenger.Default.IsRegistered<LanguageChangedMessage>(vm));
    }

    [AvaloniaTheory]
    [InlineData(GameVersion.D)]
    [InlineData(GameVersion.Pt)]
    [InlineData(GameVersion.HG)]
    public void BlankGen4SaveSemanticBuffersRemainUntouchedWithoutApply(GameVersion version)
    {
        var save = Assert.IsAssignableFrom<SAV4>(BlankSaveFile.Get(version));
        save.SetWork(64, ushort.MaxValue);
        save.State.Edited = false;
        var before = Snapshot(save);
        using var vm = new CategorizedEventEditorViewModel(save, new RecordingDialogService());
        AssertSnapshot(save, before);
        Row(vm, EventDataKind.Work, 64).ValueText = "1";
        vm.CancelCommand.Execute(null);
        AssertSnapshot(save, before);
        Assert.False(save.State.Edited);
    }

    private static EventDataField Field(EventDataSession session, EventDataKind kind, int index) => session.Fields.Single(field => field.Kind == kind && field.Index == index);
    private static EventDataRow Row(CategorizedEventEditorViewModel vm, EventDataKind kind, int index) => vm.Rows.Single(row => row.Kind == kind && row.Index == index);
    private static EventLabelCollection Labels(SAV4 save) => new(save switch { SAV4DP => "dp", SAV4Pt => "pt", SAV4HGSS => "hgss", _ => throw new ArgumentException(nameof(save)) }, save.EventFlagCount, save.EventWorkCount);
    private static (int Work, int Flag) Offsets(SAV4 save) => save switch { SAV4DP => (0xD9C, 0xFDC), SAV4Pt => (0xDAC, 0xFEC), SAV4HGSS => (0xDE4, 0x10C4), _ => throw new ArgumentException(nameof(save)) };
    private static string SavePath(string file) => Path.Combine(Assert.IsType<string>(SaveFileFixture.FindSaveFilesPath()), file);
    private static SAV4 Load(string file)
    {
        var save = Assert.IsAssignableFrom<SAV4>(SaveFileFixture.LoadSave(SavePath(file)));
        save.State.Edited = false;
        return save;
    }
    private static byte[][] Snapshot(SAV4 save) => [save.Data.ToArray(), save.General.ToArray(), save.GetPCBinary(), .. save.PartyData.Select(pkm => pkm.Data.ToArray())];
    private static void AssertSnapshot(SAV4 save, byte[][] expected)
    {
        var actual = Snapshot(save);
        Assert.Equal(expected.Length, actual.Length);
        for (int index = 0; index < expected.Length; index++) Assert.Equal(expected[index], actual[index]);
    }
    private sealed class LanguageScope : IDisposable
    {
        private readonly string _ui = LocalizedStrings.Instance.CurrentLanguage;
        private readonly string _data = GameInfo.CurrentLanguage;
        private readonly GameStrings _strings = GameInfo.Strings;
        private readonly FilteredGameDataSource _filtered = GameInfo.FilteredSources;
        private readonly CultureInfo _culture = CultureInfo.CurrentCulture;
        private readonly CultureInfo _uiCulture = CultureInfo.CurrentUICulture;
        private readonly CultureInfo? _defaultCulture = CultureInfo.DefaultThreadCurrentCulture;
        private readonly CultureInfo? _defaultUiCulture = CultureInfo.DefaultThreadCurrentUICulture;
        public LanguageScope(string language) => SetLanguage(language);
        public void SetLanguage(string language)
        {
            new LanguageService().SetLanguage(language);
            LocalizedStrings.Instance.SetLanguage(language);
            WeakReferenceMessenger.Default.Send(new LanguageChangedMessage(language));
        }
        public void Dispose()
        {
            LocalizedStrings.Instance.SetLanguage(_ui);
            GameInfo.CurrentLanguage = _data;
            GameInfo.Strings = _strings;
            GameInfo.FilteredSources = _filtered;
            CultureInfo.CurrentCulture = _culture;
            CultureInfo.CurrentUICulture = _uiCulture;
            CultureInfo.DefaultThreadCurrentCulture = _defaultCulture;
            CultureInfo.DefaultThreadCurrentUICulture = _defaultUiCulture;
        }
    }
}
