using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.VisualTree;
using PKHeX.Application.Services;
using PKHeX.Avalonia.Tests.Fixtures;
using PKHeX.Avalonia.Tests.Harness;
using PKHeX.Avalonia.Views;
using PKHeX.Core;
using PKHeX.Presentation.ViewModels;

namespace PKHeX.Avalonia.Tests;

/// <summary>
/// Block Editor parity with upstream "Savedata Block Dump" (#260): Core-metadata names, details,
/// filtering, guarded imports, folder round trips and compare, on real fixtures.
/// </summary>
public sealed class BlockEditorDumpTests : IDisposable
{
    private const uint BoxLayoutKey = 0x19722C89; // SWSH *Array_BoxLayout, UInt16 x 0x220
    private readonly string _temp = Path.Combine(Path.GetTempPath(), "pkhex-blockdump-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_temp))
            Directory.Delete(_temp, true);
    }

    private static SaveFile Load(string name)
    {
        var data = File.ReadAllBytes(Path.Combine(SaveFileFixture.FindSaveFilesPath()!, name));
        // The Let's Go fixture lacks the metadata used for auto-detection; open it with its known type.
        if (name.StartsWith("gen7b_", StringComparison.Ordinal))
            return new SAV7b(data);
        return SaveUtil.GetSaveFile(data) ?? throw new InvalidOperationException(name);
    }

    private string Folder(string name)
    {
        var path = Path.Combine(_temp, name);
        Directory.CreateDirectory(path);
        return path;
    }

    [Fact]
    public void SwshBlocksResolveCoreNamesAndKeepRawKeys()
    {
        var save = Load("gen8_sword_ct_public.main");
        var set = SaveBlockSet.Create(save);
        Assert.Equal(SaveBlockSetKind.SwishCrypto, set.Kind);

        var box = Assert.IsType<SaveBlockEntry>(set.Find(BoxLayoutKey));
        Assert.Equal("19722C89", box.KeyText);
        Assert.Equal("BoxLayout", box.Name);
        Assert.Equal("Array", box.TypeName);
        Assert.Equal("UInt16", box.SubTypeName);
        Assert.Equal(0x440, box.Length);

        // Most blocks get a Core name; every block keeps its raw key.
        Assert.True(set.Entries.Count(z => z.Name is not null) > 100);
        Assert.All(set.Entries, z => Assert.Matches("^[0-9A-F]{8}$", z.KeyText));
    }

    [Theory]
    [InlineData("gen9_scarlet.main")]
    [InlineData("gen8a_legendsarceus.main")]
    [InlineData("gen9a_legendsza.main")]
    public void OtherSwishSavesResolveNames(string fixture)
    {
        var set = SaveBlockSet.Create(Load(fixture));
        Assert.Equal(SaveBlockSetKind.SwishCrypto, set.Kind);
        Assert.Contains(set.Entries, z => z.Name is not null);
    }

    [Fact]
    public void LgpeIndexedBlocksResolveAccessorNames()
    {
        var save = Load("gen7b_letsgopikachu.bin");
        var set = SaveBlockSet.Create(save);
        Assert.Equal(SaveBlockSetKind.Indexed, set.Kind);
        var captured = Assert.IsType<SaveBlockEntry>(set.Find((uint)BelugaBlockIndex.CaptureRecord));
        Assert.Equal("Captured", captured.Name);
        Assert.Equal($"{(int)BelugaBlockIndex.CaptureRecord:00}", captured.KeyText);
        Assert.True(captured.Offset >= 0);
        Assert.Equal("Items", set.Find((uint)BelugaBlockIndex.MyItem)!.Name);
    }

    [Fact]
    public void UnsupportedSaveShowsEmptyState()
    {
        var vm = new BlockEditorViewModel(Load("gen3_emerald.sav"), new RecordingDialogService());
        Assert.False(vm.IsSupported);
        Assert.Empty(vm.FilteredBlocks);
        Assert.False(vm.ExportAllToFolderCommand.CanExecute(null));
        Assert.False(vm.ImportFromFolderCommand.CanExecute(null));
    }

    [Fact]
    public void FilteringMatchesKeyNameTypeAndValue()
    {
        var vm = new BlockEditorViewModel(Load("gen8_sword_ct_public.main"), new RecordingDialogService());
        var total = vm.FilteredBlocks.Count;
        Assert.Equal(vm.TotalCount, total);

        vm.SearchText = "19722c89";
        Assert.Equal("19722C89", Assert.Single(vm.FilteredBlocks).Key);

        vm.SearchText = "BoxLayout";
        Assert.Contains(vm.FilteredBlocks, z => z.Entry.Key == BoxLayoutKey);
        Assert.All(vm.FilteredBlocks, z => Assert.Contains("BoxLayout", z.Name, StringComparison.OrdinalIgnoreCase));

        vm.SearchText = "UInt16";
        Assert.Contains(vm.FilteredBlocks, z => z.Entry.Key == BoxLayoutKey);

        vm.SearchText = "no-such-block-anywhere";
        Assert.Empty(vm.FilteredBlocks);
        vm.SearchText = string.Empty;
        Assert.Equal(total, vm.FilteredBlocks.Count);

        // Named blocks come first, like upstream's dump.
        var firstUnnamed = vm.FilteredBlocks.ToList().FindIndex(z => z.Name.Length == 0);
        Assert.True(firstUnnamed > 0);
        Assert.All(vm.FilteredBlocks.Skip(firstUnnamed), z => Assert.Empty(z.Name));
    }

    [Fact]
    public void SelectionDrivesDetailsAndCommandState()
    {
        var vm = new BlockEditorViewModel(Load("gen8_sword_ct_public.main"), new RecordingDialogService());
        Assert.False(vm.HasSelection);
        Assert.False(vm.ExportBlockCommand.CanExecute(null));
        Assert.False(vm.ImportBlockCommand.CanExecute(null));

        vm.SelectedBlock = vm.FilteredBlocks.Single(z => z.Entry.Key == BoxLayoutKey);
        Assert.True(vm.HasSelection);
        Assert.Equal("19722C89", vm.DetailKey);
        Assert.Equal("BoxLayout", vm.DetailName);
        Assert.Equal("Array", vm.DetailType);
        Assert.True(vm.HasSubType);
        Assert.Equal("UInt16", vm.DetailSubType);
        Assert.Equal("0x440 (1088)", vm.DetailLength);
        Assert.StartsWith("00000000  ", vm.HexPreviewText);
        Assert.Equal(0x440 / BlockEditorViewModel.PreviewBytesPerRow, vm.HexPreviewText.Split('\n').Length);
        Assert.True(vm.ExportBlockCommand.CanExecute(null));
        Assert.True(vm.ImportBlockCommand.CanExecute(null));

        // A flag block has no bytes: no transfer, and the detail pane says why.
        vm.SelectedBlock = vm.FilteredBlocks.First(z => z.Entry.TypeName.StartsWith("Bool", StringComparison.Ordinal));
        Assert.False(vm.ExportBlockCommand.CanExecute(null));
        Assert.False(vm.ImportBlockCommand.CanExecute(null));
        Assert.True(vm.HasValue);
        Assert.True(vm.HasHexPreviewNote);

        // Filtering the selected row away clears the selection.
        vm.SearchText = "no-such-block-anywhere";
        Assert.Null(vm.SelectedBlock);
        Assert.False(vm.HasSelection);
    }

    [Fact]
    public void LargeBlockPreviewIsBounded()
    {
        var vm = new BlockEditorViewModel(Load("gen7b_letsgopikachu.bin"), new RecordingDialogService());
        vm.SelectedBlock = vm.FilteredBlocks.MaxBy(z => z.Entry.Length);
        Assert.True(vm.SelectedBlock!.Entry.Length > HexPreview.MaxBytes);
        Assert.Equal(HexPreview.MaxBytes / BlockEditorViewModel.PreviewBytesPerRow, vm.HexPreviewText.Split('\n').Length);
        Assert.True(vm.HasHexPreviewNote);
    }

    [Fact]
    public void HexPreviewShowsAsciiAndPadsShortRows()
    {
        var text = HexPreview.Format("AB\0\u0001xyz"u8.ToArray());
        Assert.StartsWith("00000000  41 42 00 01 78 79 7A    ", text);
        Assert.EndsWith(" AB..xyz", text);
        Assert.Equal(10 + (16 * 3) + 1 + 1 + 7, text.Length);
    }

    [Fact]
    public async Task SingleImportRejectsWrongSizeWrongBlockAndDeclinedConfirmation()
    {
        var save = Load("gen8_sword_ct_public.main");
        var dialogs = new RecordingDialogService { ConfirmResult = false };
        var vm = new BlockEditorViewModel(save, dialogs);
        vm.SelectedBlock = vm.FilteredBlocks.Single(z => z.Entry.Key == BoxLayoutKey);
        var before = vm.SelectedBlock.Entry.Data.ToArray();
        save.State.Edited = false;

        // Wrong size.
        var wrongSize = Path.Combine(Folder("single"), "blob.bin");
        File.WriteAllBytes(wrongSize, new byte[10]);
        dialogs.OpenFileResult = wrongSize;
        await vm.ImportBlockCommand.ExecuteAsync(null);
        Assert.Single(dialogs.Errors);

        // Right size, but the file name names another block.
        var other = vm.BlockSet.Entries.First(z => z.Key != BoxLayoutKey && z.HasData);
        var wrongBlock = Path.Combine(Folder("single"), other.KeyText + ".bin");
        File.WriteAllBytes(wrongBlock, new byte[0x440]);
        dialogs.OpenFileResult = wrongBlock;
        await vm.ImportBlockCommand.ExecuteAsync(null);
        Assert.Equal(2, dialogs.Errors.Count);

        // Right size and name, but the user declines.
        var good = Path.Combine(Folder("single"), "19722C89.bin");
        File.WriteAllBytes(good, Enumerable.Repeat((byte)0x5A, 0x440).ToArray());
        dialogs.OpenFileResult = good;
        await vm.ImportBlockCommand.ExecuteAsync(null);
        Assert.Single(dialogs.Confirmations);
        Assert.Equal(before, vm.SelectedBlock.Entry.Data.ToArray());
        Assert.False(save.State.Edited);

        // Confirmed: the block changes and the save is marked edited.
        dialogs.ConfirmResult = true;
        await vm.ImportBlockCommand.ExecuteAsync(null);
        Assert.All(vm.SelectedBlock.Entry.Data.ToArray(), b => Assert.Equal(0x5A, b));
        Assert.True(save.State.Edited);
    }

    [Theory]
    [InlineData("gen8_sword_ct_public.main")]
    [InlineData("gen9_scarlet.main")]
    [InlineData("gen7b_letsgopikachu.bin")]
    public async Task FolderExportImportRoundTripsEveryBlock(string fixture)
    {
        var save = Load(fixture);
        var original = save.Write().ToArray();
        var dialogs = new RecordingDialogService { ConfirmResult = true, OpenFolderResult = Folder("dump") };
        var vm = new BlockEditorViewModel(save, dialogs);

        await vm.ExportAllToFolderCommand.ExecuteAsync(null);
        var exported = Directory.GetFiles(dialogs.OpenFolderResult!);
        Assert.Equal(vm.BlockSet.Entries.Count(z => z.HasData), exported.Length);

        // Corrupt three blocks in memory, then restore them from the dump.
        var targets = vm.BlockSet.Entries.Where(z => z.HasData && z.Length >= 4).Take(3).ToList();
        foreach (var target in targets)
            target.Replace(new byte[target.Length]);
        save.State.Edited = false;

        await vm.ImportFromFolderCommand.ExecuteAsync(null);
        Assert.Single(dialogs.Confirmations);
        Assert.Empty(dialogs.Errors);
        Assert.True(save.State.Edited);
        Assert.Equal(original, save.Write().ToArray());
    }

    [Fact]
    public void FolderImportPlanRefusesBadFilesWithoutWriting()
    {
        var save = Load("gen8_sword_ct_public.main");
        var set = SaveBlockSet.Create(save);
        var folder = Folder("plan");
        var box = set.Find(BoxLayoutKey)!;
        var flag = set.Entries.First(z => z.TypeName.StartsWith("Bool", StringComparison.Ordinal));

        File.WriteAllBytes(Path.Combine(folder, "19722C89.bin"), new byte[box.Length - 1]);      // wrong size
        File.WriteAllBytes(Path.Combine(folder, "notes.txt"), [1, 2, 3]);                        // not a key
        File.WriteAllBytes(Path.Combine(folder, "0BADF00D.bin"), [1]);                           // unknown key
        File.WriteAllBytes(Path.Combine(folder, flag.KeyText + ".bin"), [1]);                    // flag, no bytes
        var other = set.Entries.First(z => z.HasData && z.Key != BoxLayoutKey && z.Length >= 4);
        var changed = Enumerable.Repeat((byte)0xEE, other.Length).ToArray();
        File.WriteAllBytes(Path.Combine(folder, other.KeyText + " a.bin"), changed);             // duplicate pair
        File.WriteAllBytes(Path.Combine(folder, other.KeyText + " b.bin"), changed);

        var before = save.Write().ToArray();
        var plan = set.PlanFolderImport(folder);
        Assert.Empty(plan.Items);
        Assert.Contains(plan.Failures, f => f.Reason == SaveBlockImportFailureReason.SizeMismatch && f.ExpectedLength == box.Length);
        Assert.Contains(plan.Failures, f => f.Reason == SaveBlockImportFailureReason.UnrecognizedName);
        Assert.Contains(plan.Failures, f => f.Reason == SaveBlockImportFailureReason.UnknownBlock);
        Assert.Contains(plan.Failures, f => f.Reason == SaveBlockImportFailureReason.NoData);
        Assert.Equal(2, plan.Failures.Count(f => f.Reason == SaveBlockImportFailureReason.Duplicate));
        Assert.Equal(0, plan.Apply());
        Assert.Equal(before, save.Write().ToArray());
        Assert.Contains("19722C89.bin", BlockEditorViewModel.BuildImportReport(plan));
    }

    [Fact]
    public async Task DeclinedFolderImportChangesNothing()
    {
        var save = Load("gen8_sword_ct_public.main");
        var dialogs = new RecordingDialogService { ConfirmResult = false, OpenFolderResult = Folder("declined") };
        var vm = new BlockEditorViewModel(save, dialogs);
        var box = vm.BlockSet.Find(BoxLayoutKey)!;
        File.WriteAllBytes(Path.Combine(dialogs.OpenFolderResult!, "19722C89.bin"), new byte[box.Length]);
        var before = save.Write().ToArray();
        save.State.Edited = false;

        await vm.ImportFromFolderCommand.ExecuteAsync(null);
        Assert.Single(dialogs.Confirmations);
        Assert.Equal(before, save.Write().ToArray());
        Assert.False(save.State.Edited);
    }

    [Fact]
    public void SingleFileExportUsesCoreOptions()
    {
        var set = SaveBlockSet.Create(Load("gen8_sword_ct_public.main"));
        var dataOnly = set.ExportAll(SCBlockExportOption.DataOnly);
        Assert.Equal(set.Entries.Sum(z => z.Length), dataOnly.Length);
        var all = set.ExportAll(SCBlockExportOption.All);
        Assert.True(all.Length > dataOnly.Length);

        var lgpe = SaveBlockSet.Create(Load("gen7b_letsgopikachu.bin"));
        Assert.Equal(lgpe.Entries.Sum(z => z.Length), lgpe.ExportAll(SCBlockExportOption.None).Length);
    }

    [Fact]
    public void CompareReportsChangedValuesBytesAndFlags()
    {
        var a = Load("gen8_sword_ct_public.main");
        var b = Load("gen8_sword_ct_public.main");
        Assert.Empty(SaveBlockSet.Compare(SaveBlockSet.Create(a), SaveBlockSet.Create(b)));

        var accessor = ((ISCBlockArray)b).Accessor;
        var scalar = ((ISCBlockArray)b).AllBlocks.First(z => z.Type == SCTypeCode.UInt32);
        var oldValue = (uint)scalar.GetValue();
        scalar.SetValue(oldValue + 1);
        var box = accessor.GetBlock(BoxLayoutKey);
        box.Data[0] ^= 0xFF;
        var flag = ((ISCBlockArray)b).AllBlocks.First(z => z.Type is SCTypeCode.Bool1 or SCTypeCode.Bool2);
        flag.ChangeBooleanType(flag.Type == SCTypeCode.Bool1 ? SCTypeCode.Bool2 : SCTypeCode.Bool1);

        var diffs = SaveBlockSet.Compare(SaveBlockSet.Create(a), SaveBlockSet.Create(b));
        Assert.Equal(3, diffs.Count);
        var value = Assert.Single(diffs, d => d.KeyText == $"{scalar.Key:X8}");
        Assert.Equal(SaveBlockDifferenceKind.ValueChanged, value.Kind);
        Assert.Equal($"{oldValue}", value.OldValue);
        Assert.Equal($"{oldValue + 1}", value.NewValue);
        var bytes = Assert.Single(diffs, d => d.KeyText == "19722C89");
        Assert.Equal(SaveBlockDifferenceKind.BytesChanged, bytes.Kind);
        Assert.Equal("BoxLayout", bytes.Name);
        Assert.Equal(1, bytes.ChangedBytes);
        Assert.Equal(SaveBlockDifferenceKind.ValueChanged, Assert.Single(diffs, d => d.KeyText == $"{flag.Key:X8}").Kind);

        var vm = new BlockEditorViewModel(a, new RecordingDialogService());
        vm.ApplyComparison(diffs);
        Assert.Equal(3, vm.CompareResults.Count);
        Assert.Contains("3", vm.CompareSummary);
    }

    [Fact]
    public void CompareReportsAddedAndRemovedBlocks()
    {
        var full = Load("gen9_violet_indigo_public.main");
        var older = Load("gen9_scarlet.main");
        var diffs = SaveBlockSet.Compare(SaveBlockSet.Create(older), SaveBlockSet.Create(full));
        Assert.Contains(diffs, d => d.Kind == SaveBlockDifferenceKind.Added);
        Assert.All(diffs.Where(d => d.Kind == SaveBlockDifferenceKind.Added), d => Assert.Equal(0, d.OldLength));
    }

    [Fact]
    public void CompareIndexedSavesReportsByteChanges()
    {
        var a = Load("gen7b_letsgopikachu.bin");
        var b = Load("gen7b_letsgopikachu.bin");
        var bSet = SaveBlockSet.Create(b);
        var captured = bSet.Find((uint)BelugaBlockIndex.CaptureRecord)!;
        var data = captured.Data.ToArray();
        data[0] ^= 1;
        data[1] ^= 1;
        captured.Replace(data);
        var diff = Assert.Single(SaveBlockSet.Compare(SaveBlockSet.Create(a), bSet));
        Assert.Equal(SaveBlockDifferenceKind.BytesChanged, diff.Kind);
        Assert.Equal("Captured", diff.Name);
        Assert.Equal(2, diff.ChangedBytes);
    }

    [Fact]
    public async Task CompareRefusesDifferentGames()
    {
        var dialogs = new RecordingDialogService { OpenFileResult = "other.main" };
        var vm = new BlockEditorViewModel(Load("gen8_sword_ct_public.main"), dialogs, _ => Load("gen9_scarlet.main"));
        Assert.False(SaveBlockSet.AreComparable(vm.BlockSet, SaveBlockSet.Create(Load("gen9_scarlet.main"))));
        await vm.CompareWithFileCommand.ExecuteAsync(null);
        Assert.Single(dialogs.Errors);
        Assert.Empty(vm.CompareResults);
    }

    [Fact]
    public async Task CompareWithFileLoadsResults()
    {
        var other = Load("gen8_sword_ct_public.main");
        ((ISCBlockArray)other).Accessor.GetBlock(BoxLayoutKey).Data[2] ^= 0x80;
        var dialogs = new RecordingDialogService { OpenFileResult = "other.main" };
        var vm = new BlockEditorViewModel(Load("gen8_sword_ct_public.main"), dialogs, _ => other);
        await vm.CompareWithFileCommand.ExecuteAsync(null);
        Assert.Empty(dialogs.Errors);
        var row = Assert.Single(vm.CompareResults);
        Assert.Equal("19722C89", row.Key);
        Assert.Equal("BoxLayout", row.Name);
    }

    private static Rect WindowRect(Control control, Window window)
    {
        var origin = control.TranslatePoint(default, window)!.Value;
        return new Rect(origin, control.Bounds.Size);
    }

    [AvaloniaFact]
    public void CompactViewKeepsListDetailsAndActionsVisible()
    {
        using var app = new HeadlessAppFixture();
        var vm = new BlockEditorViewModel(Load("gen8_sword_ct_public.main"), new RecordingDialogService());
        vm.SelectedBlock = vm.FilteredBlocks.Single(z => z.Entry.Key == BoxLayoutKey);
        app.Window.MinWidth = 0;
        app.Window.MinHeight = 0;
        app.Window.Width = 860;
        app.Window.Height = 580;
        var view = new BlockEditor { DataContext = vm };
        app.Window.Content = view;
        app.Pump();

        Assert.IsType<BlockEditor>(ViewLocator.Build(vm));
        var grid = view.FindControl<DataGrid>("BlockGrid")!;
        var detail = view.FindControl<Grid>("DetailPane")!;
        var hex = view.FindControl<SelectableTextBlock>("HexText")!;
        Assert.True(grid.Bounds.Height > 150, $"grid height {grid.Bounds.Height}");
        Assert.True(detail.Bounds.Width > 150, $"detail width {detail.Bounds.Width}");
        Assert.True(hex.IsEffectivelyVisible);
        Assert.False(string.IsNullOrEmpty(hex.Text));

        var gridRect = WindowRect(grid, app.Window);
        var detailRect = WindowRect(detail, app.Window);
        Assert.False(gridRect.Intersects(detailRect), $"grid {gridRect} overlaps detail {detailRect}");

        var windowWidth = app.Window.Bounds.Width;
        foreach (var button in view.GetVisualDescendants().OfType<Button>().Where(b => b.Command is not null && b.IsEffectivelyVisible))
        {
            var bounds = WindowRect(button, app.Window);
            Assert.True(bounds.Right <= windowWidth + 0.5, $"{button.Content} overflows ({bounds.Right} > {windowWidth})");
        }
    }
}
