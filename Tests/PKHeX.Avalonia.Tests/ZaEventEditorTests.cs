using System.Buffers.Binary;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.VisualTree;
using PKHeX.Application.Models.Events;
using PKHeX.Avalonia.Tests.Fixtures;
using PKHeX.Avalonia.Tests.Harness;
using PKHeX.Avalonia.Views;
using PKHeX.Core;
using PKHeX.Presentation.Localization;
using PKHeX.Presentation.ViewModels;

namespace PKHeX.Avalonia.Tests;

public class ZaEventEditorTests
{
    [AvaloniaFact]
    public void RepeatedCategoryClicksKeepSidebarItemsAndChosenCategoryStable()
    {
        using var app = new HeadlessAppFixture();
        using var vm = new ZaEventEditorViewModel(Load(), app.Dialogs);
        var view = new ZaEventEditor { DataContext = vm };
        app.Window.Content = view; app.Pump();
        var list = view.GetVisualDescendants().OfType<ListBox>().Single();
        var items = vm.VisibleCategories;
        foreach (var category in new[] { vm.Categories[8], vm.Categories[3], vm.Categories[12], vm.Categories[8] })
        {
            list.ScrollIntoView(category); app.Pump();
            var item = list.GetVisualDescendants().OfType<ListBoxItem>().Single(i => ReferenceEquals(i.DataContext, category));
            var point = item.TranslatePoint(new Point(item.Bounds.Width / 2, item.Bounds.Height / 2), app.Window)!.Value;
            app.Window.MouseDown(point, MouseButton.Left); app.Window.MouseUp(point, MouseButton.Left); app.Pump();
            Assert.Same(category, vm.SelectedCategory);
            Assert.Same(category, list.SelectedItem);
            Assert.Same(items, vm.VisibleCategories);
            Assert.All(vm.VisibleRecords, r => Assert.Contains(r, category.Records));
        }
        vm.CategorySearch = vm.Categories[3].Name; app.Pump();
        Assert.NotEmpty(vm.VisibleCategories);
        Assert.NotNull(vm.SelectedCategory);
        vm.SearchText = "no matching event"; app.Pump();
        Assert.Same(vm.VisibleCategories, list.ItemsSource);
    }
    private static SAV9ZA Load() => Assert.IsType<SAV9ZA>(SaveFileFixture.LoadSave(
        Path.Combine(SaveFileFixture.FindSaveFilesPath()!, "gen9a_legendsza.main")));
    private static void Seed(ZaEventDataSession session)
    {
        foreach (var category in session.Categories)
        {
            Assert.NotEmpty(category.Records);
            BinaryPrimitives.WriteUInt64LittleEndian(category.Source.Data, 0x1234567890ABCDEF);
        }
    }
    [Fact]
    public void EveryCategoryAndScalarShapeRoundTripsWithoutChangingKeysOrUnrelatedRecords()
    {
        var save = Load(); var initial = new ZaEventDataSession(save); Seed(initial);
        var session = new ZaEventDataSession(save);
        Assert.Equal(15, session.Categories.Count);
        var before = session.Categories.Select(c => c.Source.Data.ToArray()).ToArray();
        int edits = 0;
        foreach (var category in session.Categories)
        foreach (var field in category.Records[0].Fields)
        {
            if (field.Kind == ZaEventFieldKind.Boolean) field.SetBoolean(!field.BooleanValue);
            else if (field.Kind == ZaEventFieldKind.Signed64) field.SetRaw(unchecked((ulong)long.MinValue));
            else field.SetRaw(ulong.MaxValue);
            edits++;
        }
        for (int i = 0; i < before.Length; i++) Assert.Equal(before[i], session.Categories[i].Source.Data.ToArray());
        Assert.Equal(edits, session.Commit());
        var reloaded = new ZaEventDataSession(save);
        for (int i = 0; i < session.Categories.Count; i++)
        {
            var actual = reloaded.Categories[i];
            var staged = session.Categories[i];
            Assert.Equal(staged.Records[0].HashText, actual.Records[0].HashText);
            Assert.Equal(staged.Records[0].Fields.Select(f => f.RawValue), actual.Records[0].Fields.Select(f => f.RawValue));
            Assert.Equal(before[i].AsSpan(staged.RecordSize).ToArray(), actual.Source.Data[actual.RecordSize..].ToArray());
            Assert.Equal(before[i].AsSpan(0, 8).ToArray(), actual.Source.Data[..8].ToArray());
            // Composite key components remain immutable; tuple's second word is a signed value.
            if (actual.Name == "Spawner4") Assert.Equal(before[i].AsSpan(8, 8).ToArray(), actual.Source.Data.Slice(8, 8).ToArray());
            if (actual.Name == "FieldObjectInteractable") Assert.Equal(before[i].AsSpan(8, 16).ToArray(), actual.Source.Data.Slice(8, 16).ToArray());
        }
        Assert.Equal(0, session.Commit());
    }
    [Fact]
    public void ResetAndNoOpPreserveNoncanonicalBooleanBits()
    {
        var save = Load();
        var seed = new ZaEventDataSession(save).Categories.First(c => c.Name == "Flags");
        BinaryPrimitives.WriteUInt64LittleEndian(seed.Source.Data, 42);
        BinaryPrimitives.WriteUInt64LittleEndian(seed.Source.Data[8..], 0xA000000000000002);
        var before = seed.Source.Data.ToArray();
        var session = new ZaEventDataSession(save);
        var boolean = session.Categories.First(c => c.Name == "Flags").Records[0].Fields[0];
        boolean.SetBoolean(true);
        Assert.Equal(0, session.Commit());
        Assert.Equal(before, seed.Source.Data.ToArray());
        boolean.SetBoolean(false);
        session.Reset();
        Assert.Equal(0xA000000000000002UL, boolean.RawValue);
        Assert.Equal(before, seed.Source.Data.ToArray());
    }
    [Fact]
    public void UnknownHashesRequireAdvancedAndTypedValidationBlocksInvalidApply()
    {
        var save = Load(); Seed(new ZaEventDataSession(save));
        using var vm = new ZaEventEditorViewModel(save, new RecordingDialogService());
        vm.SelectedCategory = vm.Categories.Single(c => c.Key == "Work");
        var row = vm.SelectedCategory.Records[0];
        Assert.False(row.CanEdit);
        var before = row.ValueSummary;
        row.Fields[0].ValueText = "99";
        Assert.Equal(before, row.ValueSummary);
        vm.Advanced = true;
        Assert.True(row.CanEdit);
        row.Fields[0].ValueText = "0xFFFFFFFFFFFFFFFF";
        Assert.False(row.Fields[0].HasErrors);
        row.Fields[0].ValueText = "18446744073709551616";
        Assert.True(row.Fields[0].HasErrors);
        Assert.False(vm.CanApply);
        vm.ResetCommand.Execute(null);
        Assert.True(vm.CanApply);
        vm.SelectedCategory = vm.Categories.Single(c => c.Key == "Report");
        var tuple = vm.SelectedCategory.Records[0];
        tuple.Fields[0].ValueText = long.MinValue.ToString();
        tuple.Fields[1].ValueText = ulong.MaxValue.ToString();
        Assert.True(vm.CanApply);
        vm.ApplyCommand.Execute(null);
        var session = new ZaEventDataSession(save);
        var actual = session.Categories.Single(c => c.Name == "Report").Records[0];
        Assert.Equal(long.MinValue, actual.Fields[0].SignedValue);
        Assert.Equal(ulong.MaxValue, actual.Fields[1].RawValue);
    }
    [Fact]
    public async Task LocalNameImportMakesKnownEntriesEditableAndCancelDiscardsPendingData()
    {
        var save = Load(); Seed(new ZaEventDataSession(save));
        var path = Path.Combine(Path.GetTempPath(), "pkhex-za-names-" + Guid.NewGuid().ToString("N") + ".txt");
        try
        {
            await File.WriteAllTextAsync(path, "1234567890ABCDEF\tKnown test event\ninvalid line\n", TestContext.Current.CancellationToken);
            var dialogs = new RecordingDialogService { OpenFileResult = path };
            using var vm = new ZaEventEditorViewModel(save, dialogs);
            await vm.LoadNamesCommand.ExecuteAsync(null);
            var row = vm.SelectedCategory.Records[0];
            Assert.Equal("Known test event", row.Name);
            Assert.True(row.CanEdit);
            var before = new ZaEventDataSession(save).Categories[0].Source.Data.ToArray();
            row.Fields[0].IsSet = !row.Fields[0].IsSet;
            bool closed = false; vm.CloseRequested = () => closed = true;
            vm.CancelCommand.Execute(null);
            Assert.True(closed);
            Assert.Equal(before, new ZaEventDataSession(save).Categories[0].Source.Data.ToArray());
            vm.SearchText = "Known test";
            Assert.Contains(row, vm.VisibleRecords);
            vm.SearchText = "0x1234567890ABCDEF";
            Assert.Contains(row, vm.VisibleRecords);
            Assert.DoesNotContain(path, vm.StatusText, StringComparison.Ordinal);
        }
        finally { File.Delete(path); }
    }
    [AvaloniaFact]
    public void MainShellRoutesZaToSupportedEventsWorkspace()
    {
        using var app = new HeadlessAppFixture();
        app.LoadSaveInstance(Load()); app.ViewModel.SelectSaveNavigationCommand.Execute(null); app.Pump();
        Assert.IsType<ZaEventEditorViewModel>(app.ViewModel.EventFlagsEditor);
        Assert.True(app.ViewModel.IsEventsWorkspace);
        Assert.Contains(app.Window.GetVisualDescendants(), c => c is ZaEventEditor);
    }
    [AvaloniaTheory]
    [InlineData(1.0)]
    [InlineData(1.5)]
    public void AllCategoriesFitAndLargeDatasetUsesVirtualizedRows(double scale)
    {
        using var app = new HeadlessAppFixture();
        using var vm = new ZaEventEditorViewModel(Load(), app.Dialogs) { Advanced = true, HideEmpty = false };
        vm.SelectedCategory = vm.Categories.Single(c => c.Key == "WorkSpawn");
        app.Window.Width = 1024; app.Window.Height = 720;
        var view = new ZaEventEditor { DataContext = vm };
        app.Window.Content = new LayoutTransformControl { Child = view, LayoutTransform = new ScaleTransform(scale, scale) }; app.Pump();
        var grid = view.FindControl<DataGrid>("RecordsGrid")!;
        Assert.True(vm.SelectedCategory.Records.Count >= 18000);
        Assert.InRange(grid.GetVisualDescendants().OfType<DataGridRow>().Count(), 1, 50);
        Assert.True(grid.Bounds.Height > 90);
        Assert.Equal(15, view.GetVisualDescendants().OfType<ListBox>().Single().ItemCount);
        var apply = view.GetVisualDescendants().OfType<Button>().Single(b => ReferenceEquals(b.Command, vm.ApplyCommand));
        Assert.InRange(apply.TranslatePoint(new Point(0, apply.Bounds.Height), app.Window)!.Value.Y, 1, 720);
        if (Environment.GetEnvironmentVariable("PKHEX_HEADLESS_CAPTURE") == "1")
        {
            var dir = Environment.GetEnvironmentVariable("PKHEX_HEADLESS_CAPTURE_DIR")!; Directory.CreateDirectory(dir);
            Assert.NotNull(app.CaptureFrame(Path.Combine(dir, $"za-events-{scale}.png")));
        }
    }
    [AvaloniaFact]
    public void KeyboardBooleanEditRemainsStagedAndResetRestoresSelectedRecord()
    {
        using var app = new HeadlessAppFixture();
        var save = Load(); Seed(new ZaEventDataSession(save));
        using var vm = new ZaEventEditorViewModel(save, app.Dialogs) { Advanced = true };
        app.Window.Content = new ZaEventEditor { DataContext = vm }; app.Window.Width = 900; app.Window.Height = 650; app.Pump();
        var view = (ZaEventEditor)app.Window.Content!;
        view.GetVisualDescendants().OfType<Expander>().Single().IsExpanded = true; app.Pump();
        var check = view.GetVisualDescendants().OfType<CheckBox>().Single(c => Equals(c.Content, LocalizedStrings.Instance["EventFlagsEditor_SetCheckbox"]));
        var original = check.IsChecked;
        var before = new ZaEventDataSession(save).Categories[0].Source.Data.ToArray();
        app.Focus(check); app.PressKey(PhysicalKey.Space);
        Assert.Equal(!original, vm.SelectedRecord!.Fields[0].IsSet);
        Assert.Equal(before, new ZaEventDataSession(save).Categories[0].Source.Data.ToArray());
        vm.ResetCommand.Execute(null); app.Pump();
        Assert.Equal(original, vm.SelectedRecord!.Fields[0].IsSet);
    }
    [AvaloniaFact]
    public void GermanNarrowViewKeepsApplyReachableAndLanguageChangePreservesEdits()
    {
        var previous = LocalizedStrings.Instance.CurrentLanguage;
        using var app = new HeadlessAppFixture();
        var save = Load(); Seed(new ZaEventDataSession(save));
        using var vm = new ZaEventEditorViewModel(save, app.Dialogs) { Advanced = true };
        vm.SelectedCategory = vm.Categories.Single(c => c.Key == "Report");
        vm.SelectedRecord = vm.SelectedCategory.Records[0];
        vm.SelectedRecord.Fields[0].ValueText = "-42";
        try
        {
            app.ViewModel.LanguageService.SetLanguage("de");
            Assert.Equal("-42", vm.SelectedRecord.Fields[0].ValueText);
            app.Window.MinWidth = 0; app.Window.MinHeight = 0;
            app.Window.Width = 620; app.Window.Height = 620;
            var view = new ZaEventEditor { DataContext = vm };
            app.Window.Content = view; app.Pump();
            var apply = view.GetVisualDescendants().OfType<Button>().Single(b => ReferenceEquals(b.Command, vm.ApplyCommand));
            Assert.InRange(apply.TranslatePoint(new Point(apply.Bounds.Width, apply.Bounds.Height), app.Window)!.Value.X, 1, 620);
            Assert.InRange(apply.TranslatePoint(new Point(0, apply.Bounds.Height), app.Window)!.Value.Y, 1, 620);
            Assert.True(view.FindControl<DataGrid>("RecordsGrid")!.Bounds.Height > 80);
            if (Environment.GetEnvironmentVariable("PKHEX_HEADLESS_CAPTURE") == "1")
                Assert.NotNull(app.CaptureFrame(Path.Combine(Environment.GetEnvironmentVariable("PKHEX_HEADLESS_CAPTURE_DIR")!, "za-events-de-narrow.png")));
        }
        finally { app.ViewModel.LanguageService.SetLanguage(previous); }
    }
    [Fact]
    public async Task MalformedNameFileDoesNotExposeUnknownRecordsForEditing()
    {
        var save = Load(); Seed(new ZaEventDataSession(save));
        var path = Path.Combine(Path.GetTempPath(), "pkhex-za-malformed-" + Guid.NewGuid().ToString("N") + ".txt");
        try
        {
            await File.WriteAllTextAsync(path, "not a hash\tInjected name\n", TestContext.Current.CancellationToken);
            using var vm = new ZaEventEditorViewModel(save, new RecordingDialogService { OpenFileResult = path });
            await vm.LoadNamesCommand.ExecuteAsync(null);
            Assert.False(vm.Categories[0].Records[0].CanEdit);
        }
        finally { File.Delete(path); }
    }

}
