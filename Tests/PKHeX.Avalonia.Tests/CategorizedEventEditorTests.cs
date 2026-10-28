using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.VisualTree;
using CommunityToolkit.Mvvm.Messaging;
using Moq;
using PKHeX.Application.Abstractions;
using PKHeX.Application.Models.Events;
using PKHeX.Avalonia.Tests.Fixtures;
using PKHeX.Avalonia.Tests.Harness;
using PKHeX.Avalonia.Views;
using PKHeX.Core;
using PKHeX.Presentation.Localization;
using PKHeX.Presentation.ViewModels;

namespace PKHeX.Avalonia.Tests;

public class CategorizedEventEditorTests
{
    private static SaveFile Load(string file)
    {
        var path = Path.Combine(SaveFileFixture.FindSaveFilesPath()!, file);
        return file == "gen7b_letsgopikachu.bin" ? new SAV7b(File.ReadAllBytes(path))
            : Assert.IsAssignableFrom<SaveFile>(SaveFileFixture.LoadSave(path));
    }
    [Theory]
    [InlineData("gen7_sun.main")]
    [InlineData("gen7_ultrasun.main")]
    [InlineData("gen7b_letsgopikachu.bin")]
    [InlineData("gen8b_brilliantdiamond.bin")]
    public void QuickAccessGridAndCancellationShareOneSnapshot(string file)
    {
        var save = Load(file);
        var before = save.Data.ToArray();
        using var vm = new CategorizedEventEditorViewModel(save, new RecordingDialogService());
        var row = Assert.IsType<EventDataRow>(vm.SelectedRow);
        Assert.Equal(0, row.Index);
        var original = row.IsSet;
        row.IsSet = !original;
        Assert.Equal(before, save.Data.ToArray());
        vm.ResetCommand.Execute(null);
        Assert.Equal(original, row.IsSet);
        vm.SelectedIndex = vm.MaxIndex;
        Assert.Equal(vm.MaxIndex, vm.SelectedRow!.Index);
        vm.SelectedRow.IsSet = !vm.SelectedRow.IsSet;
        bool closed = false;
        vm.CloseRequested = () => closed = true;
        vm.CancelCommand.Execute(null);
        Assert.True(closed);
        Assert.Equal(before, save.Data.ToArray());
        vm.SelectedIndex = vm.MaxIndex + 1;
        Assert.Equal(vm.MaxIndex, vm.SelectedIndex);
        vm.SelectedIndex = -1;
        Assert.Equal(0, vm.SelectedIndex);
    }
    [Theory]
    [InlineData("gen7_sun.main")]
    [InlineData("gen7_ultrasun.main")]
    [InlineData("gen7b_letsgopikachu.bin")]
    [InlineData("gen8b_brilliantdiamond.bin")]
    public void NamesCategoriesSearchAndKnownValuesRemainEditable(string file)
    {
        using var vm = new CategorizedEventEditorViewModel(Load(file), new RecordingDialogService());
        Assert.Contains(vm.Rows, r => !r.Name.StartsWith(LocalizedStrings.Instance.Format("Events_RawEntry", r.Index), StringComparison.Ordinal));
        var named = vm.Rows.First(r => r.CategoryKey != "None");
        vm.SearchText = named.Name;
        Assert.Contains(named, vm.VisibleRows);
        vm.SearchText = named.HexIndex;
        Assert.Contains(named, vm.VisibleRows);
        vm.SelectedCategory = vm.Categories.Single(c => c.Key == named.CategoryKey);
        Assert.All(vm.VisibleRows, r => Assert.Equal(named.CategoryKey, r.CategoryKey));
        vm.SelectedBank = vm.Banks.Single(b => b.Kind == EventDataKind.Work);
        Assert.All(vm.VisibleRows, r => Assert.False(r.IsBoolean));
        var presetRow = vm.Rows.First(r => r.Kind == EventDataKind.Work && r.HasOptions);
        vm.SelectedIndex = presetRow.Index;
        presetRow.SelectedPreset = presetRow.Options[0];
        Assert.Equal(presetRow.Options[0].Value.ToString(System.Globalization.CultureInfo.InvariantCulture), presetRow.ValueText);
        presetRow.ValueText = "invalid";
        Assert.True(presetRow.HasErrors);
        Assert.False(vm.CanApply);
        Assert.False(vm.ApplyCommand.CanExecute(null));
        vm.ResetCommand.Execute(null);
        Assert.False(presetRow.HasErrors);
        Assert.True(vm.CanApply);
    }
    [Fact]
    public void BdspSignedHexAndFloatInterpretationsRoundTrip()
    {
        var save = Assert.IsType<SAV8BS>(Load("gen8b_brilliantdiamond.bin"));
        using var vm = new CategorizedEventEditorViewModel(save, new RecordingDialogService());
        vm.SelectedBank = vm.Banks.Single(b => b.Kind == EventDataKind.Work);
        vm.SelectedIndex = 499;
        var row = vm.SelectedRow!;
        row.ValueText = "0xFFFFFFFF";
        Assert.False(row.HasErrors);
        Assert.Equal("-1", row.DisplayValue);
        vm.ApplyCommand.Execute(null);
        Assert.Equal(-1, save.FlagWork.GetWork(499));
        row.EditAsFloat = true;
        Assert.False(row.HasErrors); // Existing arbitrary integer bits may represent NaN.
        row.FloatText = "Infinity";
        Assert.True(row.HasErrors);
        Assert.False(vm.CanApply);
        row.FloatText = "1.25";
        Assert.False(row.HasErrors);
        vm.ApplyCommand.Execute(null);
        Assert.Equal(1.25f, save.FlagWork.GetFloatWork(499));
        row.FloatText = "NaN";
        Assert.True(row.HasErrors);
        vm.ResetCommand.Execute(null);
        vm.SelectedBank = vm.Banks.Single(b => b.Kind == EventDataKind.SystemFlag);
        Assert.Equal(999, vm.MaxIndex);
        vm.SelectedIndex = 999;
        var expected = !save.FlagWork.GetSystemFlag(999);
        vm.SelectedRow!.IsSet = expected;
        vm.ApplyCommand.Execute(null);
        Assert.Equal(expected, save.FlagWork.GetSystemFlag(999));
    }
    [Fact]
    public async Task ResearchLoadsTwoFilesWithoutTouchingLoadedSaveOrRetainingPaths()
    {
        var older = Assert.IsAssignableFrom<SAV7>(Load("gen7_sun.main"));
        var newer = Assert.IsAssignableFrom<SAV7>(older.Clone());
        newer.EventWork.SetEventFlag(123, !older.EventWork.GetEventFlag(123));
        var directory = Path.Combine(Path.GetTempPath(), "pkhex-event-diff-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        var olderPath = Path.Combine(directory, "older.main");
        var newerPath = Path.Combine(directory, "newer.main");
        try
        {
            await File.WriteAllBytesAsync(olderPath, older.Data.ToArray(), TestContext.Current.CancellationToken);
            await File.WriteAllBytesAsync(newerPath, newer.Data.ToArray(), TestContext.Current.CancellationToken);
            var dialogs = new Mock<IDialogService>();
            dialogs.SetupSequence(d => d.OpenFileAsync(It.IsAny<string>(), It.IsAny<string[]?>()))
                .ReturnsAsync(olderPath).ReturnsAsync(newerPath);
            var loaded = Load("gen8b_brilliantdiamond.bin");
            var before = loaded.Data.ToArray();
            using var vm = new CategorizedEventEditorViewModel(loaded, dialogs.Object);
            await vm.CompareCommand.ExecuteAsync(null);
            Assert.All(dialogs.Invocations.Where(i => i.Method.Name == nameof(IDialogService.OpenFileAsync)), invocation =>
            {
                var patterns = Assert.IsType<string[]>(invocation.Arguments[1]);
                Assert.Contains("*.sav", patterns);
                Assert.Contains("*", patterns);
                Assert.Contains("*.*", patterns);
            });
            var diff = Assert.Single(vm.Differences);
            Assert.Equal(123, diff.Index);
            Assert.Equal(before, loaded.Data.ToArray());
            Assert.False(vm.IsComparing);
            Assert.DoesNotContain(directory, vm.StatusText, StringComparison.Ordinal);
            Assert.False(vm.CompareSaves(older, loaded));
            Assert.Empty(vm.Differences);
            dialogs.Verify(d => d.ShowErrorAsync(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
        }
        finally { Directory.Delete(directory, true); }
    }
    [AvaloniaTheory]
    [InlineData("gen7_sun.main")]
    [InlineData("gen7_ultrasun.main")]
    [InlineData("gen7b_letsgopikachu.bin")]
    [InlineData("gen8b_brilliantdiamond.bin")]
    public void MainShellRoutesGameAwareEventsAndShowsSupportedWorkspace(string file)
    {
        using var app = new HeadlessAppFixture();
        app.LoadSaveInstance(Load(file));
        app.ViewModel.SelectSaveNavigationCommand.Execute(null);
        app.Pump();
        Assert.IsType<CategorizedEventEditorViewModel>(app.ViewModel.EventFlagsEditor);
        Assert.True(app.ViewModel.IsEventsWorkspace);
        Assert.Contains(app.Window.GetVisualDescendants(), control => control is CategorizedEventEditor);
        app.LoadSaveInstance(new SAV6XY());
        Assert.IsType<EventFlagsEditorViewModel>(app.ViewModel.EventFlagsEditor);
    }
    [AvaloniaTheory]
    [InlineData("gen7_sun.main", false)]
    [InlineData("gen7b_letsgopikachu.bin", false)]
    [InlineData("gen8b_brilliantdiamond.bin", false)]
    [InlineData("gen8b_brilliantdiamond.bin", true)]
    public void ViewRealizesVirtualizedRowsAndKeyboardFlagToggle(string file, bool work)
    {
        using var app = new HeadlessAppFixture();
        using var vm = new CategorizedEventEditorViewModel(Load(file), app.Dialogs);
        if (work) vm.SelectedBank = vm.Banks.Single(b => b.Kind == EventDataKind.Work);
        app.Window.Width = 900;
        app.Window.Height = 600;
        var view = new CategorizedEventEditor { DataContext = vm };
        app.Window.Content = view;
        app.Pump();
        var grid = view.FindControl<DataGrid>("EventGrid")!;
        Assert.True(grid.Bounds.Height > 80);
        Assert.InRange(grid.GetVisualDescendants().OfType<DataGridRow>().Count(), 1, 40);
        if (!work)
        {
            var checkbox = grid.GetVisualDescendants().OfType<CheckBox>().First(c => c.IsEffectivelyVisible);
            Assert.NotEmpty(AutomationProperties.GetName(checkbox)!);
            var before = checkbox.IsChecked;
            app.Focus(checkbox);
            app.PressKey(PhysicalKey.Space);
            Assert.Equal(!before, checkbox.IsChecked);
        }
        var research = view.GetVisualDescendants().OfType<TabControl>().Single();
        if (Environment.GetEnvironmentVariable("PKHEX_HEADLESS_CAPTURE") == "1")
        {
            var dir = Environment.GetEnvironmentVariable("PKHEX_HEADLESS_CAPTURE_DIR")!;
            Directory.CreateDirectory(dir);
            Assert.NotNull(app.CaptureFrame(Path.Combine(dir, $"events-{Path.GetFileNameWithoutExtension(file)}-{work}.png")));
        }
        research.SelectedIndex = 1;
        app.Pump();
        Assert.True(view.FindControl<DataGrid>("DifferenceGrid")!.IsEffectivelyVisible);
    }
    [AvaloniaTheory]
    [InlineData(1.0)]
    [InlineData(1.5)]
    public void GermanNamesRefreshWithoutDiscardingPendingValuesAndFitViewport(double scale)
    {
        var previous = LocalizedStrings.Instance.CurrentLanguage;
        using var app = new HeadlessAppFixture();
        using var vm = new CategorizedEventEditorViewModel(Load("gen8b_brilliantdiamond.bin"), app.Dialogs);
        var row = vm.Rows.First(r => r.Kind == EventDataKind.Work);
        row.ValueText = "-1";
        try
        {
            app.ViewModel.LanguageService.SetLanguage("de");
            Assert.Equal("-1", row.ValueText);
            app.Window.Width = 1024;
            app.Window.Height = 720;
            vm.SelectedBank = vm.Banks.Single(b => b.Kind == EventDataKind.Work);
            var view = new CategorizedEventEditor { DataContext = vm };
            app.Window.Content = new LayoutTransformControl { Child = view, LayoutTransform = new ScaleTransform(scale, scale) };
            app.Pump();
            var grid = view.FindControl<DataGrid>("EventGrid")!;
            var filters = view.FindControl<StackPanel>("EditorFilters")!;
            var details = view.FindControl<ContentControl>("EditorDetails")!;
            var gridTop = grid.TranslatePoint(new Point(0, 0), view)!.Value.Y;
            var gridBottom = grid.TranslatePoint(new Point(0, grid.Bounds.Height), view)!.Value.Y;
            var filtersBottom = filters.TranslatePoint(new Point(0, filters.Bounds.Height), view)!.Value.Y;
            var detailsTop = details.TranslatePoint(new Point(0, 0), view)!.Value.Y;
            Assert.True(gridTop >= filtersBottom - 1);
            Assert.True(detailsTop >= gridBottom - 1);
            Assert.True(grid.Bounds.Height > 60);
            foreach (var button in view.GetVisualDescendants().OfType<Button>().Where(b => ReferenceEquals(b.Command, vm.ApplyCommand) || ReferenceEquals(b.Command, vm.CancelCommand)))
            {
                var bottom = button.TranslatePoint(new Point(0, button.Bounds.Height), app.Window)!.Value.Y;
                Assert.InRange(bottom, 1, 720);
            }
            if (Environment.GetEnvironmentVariable("PKHEX_HEADLESS_CAPTURE") == "1" && scale == 1.5)
                Assert.NotNull(app.CaptureFrame(Path.Combine(Environment.GetEnvironmentVariable("PKHEX_HEADLESS_CAPTURE_DIR")!, "events-de-150.png")));
        }
        finally { app.ViewModel.LanguageService.SetLanguage(previous); }
    }
}
