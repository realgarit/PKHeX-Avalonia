using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PKHeX.Application.Services;
using PKHeX.Core;
using PKHeX.Presentation.Localization;

namespace PKHeX.Presentation.ViewModels;

/// <summary>
/// Savedata block dump (upstream "Savedata Block Dump" / SAV_BlockDump8 and the block accessor view):
/// named blocks with their raw keys, a detail pane with a bounded hex/ASCII preview, single and bulk
/// export, validated and confirmed imports, and a compare tab for two block sets.
/// </summary>
public partial class BlockEditorViewModel : ViewModelBase
{
    private readonly SaveFile _sav;
    private readonly IDialogService _dialogService;
    private readonly Func<string, SaveFile?> _loadSave;
    private readonly List<BlockRowViewModel> _rows;

    public BlockEditorViewModel(SaveFile sav, IDialogService dialogService)
        : this(sav, dialogService, SaveUtil.GetSaveFile)
    {
    }

    public BlockEditorViewModel(SaveFile sav, IDialogService dialogService, Func<string, SaveFile?> loadSave)
    {
        _sav = sav;
        _dialogService = dialogService;
        _loadSave = loadSave;
        BlockSet = SaveBlockSet.Create(sav);
        // Like upstream's dump: named blocks first (by name), then unnamed blocks by raw key.
        _rows = BlockSet.Entries
            .OrderBy(z => z.Name is null)
            .ThenBy(z => z.Name, StringComparer.OrdinalIgnoreCase)
            .ThenBy(z => z.Key)
            .Select(z => new BlockRowViewModel(z))
            .ToList();
        FilterBlocks();
        UpdateDetails();
    }

    private static LocalizedStrings L => LocalizedStrings.Instance;

    public SaveBlockSet BlockSet { get; }

    public bool IsSupported => BlockSet.IsSupported;

    /// <summary>Single-file export options only exist for hash-keyed saves (Core's SCBlockExportOption).</summary>
    public bool HasExportOptions => BlockSet.Kind == SaveBlockSetKind.SwishCrypto;

    public int TotalCount => _rows.Count;

    public ObservableCollection<BlockRowViewModel> FilteredBlocks { get; } = [];

    [ObservableProperty]
    private string _searchText = string.Empty;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ExportBlockCommand))]
    [NotifyCanExecuteChangedFor(nameof(ImportBlockCommand))]
    private BlockRowViewModel? _selectedBlock;

    [ObservableProperty] private string _blockCountText = string.Empty;
    [ObservableProperty] private string _statusText = string.Empty;

    [ObservableProperty] private bool _exportDataOnly = true;
    [ObservableProperty] private bool _exportKeys = true;
    [ObservableProperty] private bool _exportTypeInfo = true;
    [ObservableProperty] private bool _exportFakeHeader = true;

    // Detail pane
    [ObservableProperty] private bool _hasSelection;
    [ObservableProperty] private string _detailKey = string.Empty;
    [ObservableProperty] private string _detailName = string.Empty;
    [ObservableProperty] private string _detailType = string.Empty;
    [ObservableProperty] private string _detailSubType = string.Empty;
    [ObservableProperty] private bool _hasSubType;
    [ObservableProperty] private string _detailLength = string.Empty;
    [ObservableProperty] private string _detailOffset = string.Empty;
    [ObservableProperty] private bool _hasOffset;
    [ObservableProperty] private string _detailValue = string.Empty;
    [ObservableProperty] private bool _hasValue;
    [ObservableProperty] private string _hexPreviewText = string.Empty;
    [ObservableProperty] private string _hexPreviewNote = string.Empty;
    [ObservableProperty] private bool _hasHexPreviewNote;

    // Compare tab
    public ObservableCollection<BlockDifferenceRowViewModel> CompareResults { get; } = [];

    [ObservableProperty] private string _compareSummary = string.Empty;

    partial void OnSearchTextChanged(string value) => FilterBlocks();

    partial void OnSelectedBlockChanged(BlockRowViewModel? value) => UpdateDetails();

    private void FilterBlocks()
    {
        var search = SearchText.Trim();
        var selected = SelectedBlock;
        FilteredBlocks.Clear();
        foreach (var row in _rows)
        {
            if (row.Matches(search))
                FilteredBlocks.Add(row);
        }
        if (selected is not null && !FilteredBlocks.Contains(selected))
            SelectedBlock = null;
        BlockCountText = L.Format("BlockEditor_BlockCount", FilteredBlocks.Count, _rows.Count);
    }

    private void UpdateDetails()
    {
        var entry = SelectedBlock?.Entry;
        HasSelection = entry is not null;
        if (entry is null)
        {
            DetailKey = DetailName = DetailType = DetailSubType = DetailLength = DetailOffset = DetailValue = string.Empty;
            HasSubType = HasOffset = HasValue = false;
            HexPreviewText = string.Empty;
            HexPreviewNote = L["BlockEditor_NoSelection"];
            HasHexPreviewNote = true;
            return;
        }

        DetailKey = entry.KeyText;
        DetailName = entry.Name ?? L["BlockEditor_Unnamed"];
        DetailType = entry.TypeName;
        DetailSubType = entry.SubTypeName ?? string.Empty;
        HasSubType = entry.SubTypeName is not null;
        DetailLength = FormattableString.Invariant($"0x{entry.Length:X} ({entry.Length})");
        DetailOffset = entry.Offset >= 0 ? FormattableString.Invariant($"0x{entry.Offset:X5}") : string.Empty;
        HasOffset = entry.Offset >= 0;
        DetailValue = entry.ValueText ?? string.Empty;
        HasValue = entry.ValueText is not null;
        HexPreviewText = HexPreview.Format(entry.Data, PreviewBytesPerRow);
        if (!entry.HasData)
            HexPreviewNote = L["BlockEditor_NoData"];
        else if (entry.Length > HexPreview.MaxBytes)
            HexPreviewNote = L.Format("BlockEditor_PreviewTruncated", HexPreview.MaxBytes, entry.Length);
        else
            HexPreviewNote = string.Empty;
        HasHexPreviewNote = HexPreviewNote.Length != 0;
    }

    /// <summary>Eight bytes per row keeps the preview readable inside the compact detail pane.</summary>
    public const int PreviewBytesPerRow = 8;

    private void RefreshAfterWrite()
    {
        foreach (var row in _rows)
            row.Refresh();
        UpdateDetails();
    }

    private bool CanTransferSelected() => SelectedBlock?.Entry.HasData == true;

    private bool CanUseAll() => IsSupported;

    [RelayCommand(CanExecute = nameof(CanTransferSelected))]
    private async Task ExportBlockAsync()
    {
        if (SelectedBlock?.Entry is not { HasData: true } entry)
            return;
        var path = await _dialogService.SaveFileAsync(L["BlockEditor_ExportBlockTitle"], entry.ExportFileName);
        if (string.IsNullOrEmpty(path))
            return;

        try
        {
            await File.WriteAllBytesAsync(path, entry.Data.ToArray());
            StatusText = L.Format("BlockEditor_ExportBlockDone", entry.KeyText);
        }
        catch (Exception ex)
        {
            await _dialogService.ShowErrorAsync(L["BlockEditor_ExportErrorTitle"], ex.Message);
        }
    }

    [RelayCommand(CanExecute = nameof(CanTransferSelected))]
    private async Task ImportBlockAsync()
    {
        if (SelectedBlock?.Entry is not { HasData: true } entry)
            return;
        var path = await _dialogService.OpenFileAsync(L["BlockEditor_ImportBlockTitle"], ["*.*"]);
        if (string.IsNullOrEmpty(path))
            return;

        try
        {
            var fileName = Path.GetFileName(path);
            if (BlockSet.TryParseFileKey(fileName, out var fileKey) && fileKey != entry.Key && BlockSet.Find(fileKey) is { } owner)
            {
                await _dialogService.ShowErrorAsync(L["BlockEditor_ImportErrorTitle"],
                    L.Format("BlockEditor_WrongBlockFile", fileName, owner.KeyText, entry.KeyText));
                return;
            }

            var data = await File.ReadAllBytesAsync(path);
            if (data.Length != entry.Length)
            {
                await _dialogService.ShowErrorAsync(L["BlockEditor_ImportErrorTitle"],
                    L.Format("BlockEditor_SizeMismatch", entry.Length, data.Length));
                return;
            }

            var label = BlockLabel(entry);
            var confirmed = await _dialogService.ShowConfirmationAsync(L["BlockEditor_ConfirmImportTitle"],
                L.Format("BlockEditor_ConfirmImportBlock", label, entry.Length, fileName),
                L["BlockEditor_ImportButton"], L["Common_Cancel"]);
            if (!confirmed)
                return;

            entry.Replace(data);
            _sav.State.Edited = true;
            RefreshAfterWrite();
            StatusText = L.Format("BlockEditor_ImportSuccessMessage", label);
        }
        catch (Exception ex)
        {
            await _dialogService.ShowErrorAsync(L["BlockEditor_ImportErrorTitle"], ex.Message);
        }
    }

    [RelayCommand(CanExecute = nameof(CanUseAll))]
    private async Task ExportAllToFolderAsync()
    {
        var folder = await _dialogService.OpenFolderAsync(L["BlockEditor_SelectFolder"]);
        if (string.IsNullOrEmpty(folder))
            return;
        try
        {
            var count = BlockSet.ExportToFolder(folder);
            StatusText = L.Format("BlockEditor_ExportFolderDone", count);
        }
        catch (Exception ex)
        {
            await _dialogService.ShowErrorAsync(L["BlockEditor_ExportErrorTitle"], ex.Message);
        }
    }

    public SCBlockExportOption ExportOption =>
        (ExportDataOnly ? SCBlockExportOption.DataOnly : 0)
        | (ExportKeys ? SCBlockExportOption.Key : 0)
        | (ExportTypeInfo ? SCBlockExportOption.TypeInfo : 0)
        | (ExportFakeHeader ? SCBlockExportOption.FakeHeader : 0);

    [RelayCommand(CanExecute = nameof(CanUseAll))]
    private async Task ExportAllToFileAsync()
    {
        var path = await _dialogService.SaveFileAsync(L["BlockEditor_ExportAllTitle"], "blocks.bin");
        if (string.IsNullOrEmpty(path))
            return;
        try
        {
            await File.WriteAllBytesAsync(path, BlockSet.ExportAll(ExportOption));
            StatusText = L.Format("BlockEditor_ExportFileDone", Path.GetFileName(path));
        }
        catch (Exception ex)
        {
            await _dialogService.ShowErrorAsync(L["BlockEditor_ExportErrorTitle"], ex.Message);
        }
    }

    [RelayCommand(CanExecute = nameof(CanUseAll))]
    private async Task ImportFromFolderAsync()
    {
        var folder = await _dialogService.OpenFolderAsync(L["BlockEditor_SelectFolder"]);
        if (string.IsNullOrEmpty(folder))
            return;

        SaveBlockImportPlan plan;
        try
        {
            plan = BlockSet.PlanFolderImport(folder);
        }
        catch (Exception ex)
        {
            await _dialogService.ShowErrorAsync(L["BlockEditor_ImportErrorTitle"], ex.Message);
            return;
        }

        var report = BuildImportReport(plan);
        if (plan.Items.Count == 0)
        {
            await _dialogService.ShowInformationAsync(L["BlockEditor_ImportFolderTitle"], report);
            StatusText = L.Format("BlockEditor_ImportFolderDone", 0);
            return;
        }

        var confirmed = await _dialogService.ShowConfirmationAsync(L["BlockEditor_ConfirmImportTitle"],
            report + "\n\n" + L.Format("BlockEditor_ImportApplyConfirm", plan.Items.Count),
            L["BlockEditor_ImportButton"], L["Common_Cancel"]);
        if (!confirmed)
            return;

        try
        {
            var count = plan.Apply();
            RefreshAfterWrite();
            StatusText = L.Format("BlockEditor_ImportFolderDone", count);
        }
        catch (Exception ex)
        {
            await _dialogService.ShowErrorAsync(L["BlockEditor_ImportErrorTitle"], ex.Message);
        }
    }

    /// <summary>Localized text for a validated folder import: what changes and what is refused, and why.</summary>
    public static string BuildImportReport(SaveBlockImportPlan plan)
    {
        var builder = new StringBuilder();
        builder.Append(L.Format("BlockEditor_ImportPlanSummary", plan.Items.Count, plan.UnchangedCount, plan.Failures.Count));
        const int maxListed = 12;
        if (plan.Items.Count != 0)
        {
            builder.Append("\n\n").Append(L["BlockEditor_ImportPlanChanged"]);
            foreach (var item in plan.Items.Take(maxListed))
            {
                builder.Append("\n• ").Append(BlockLabel(item.Entry));
            }
            if (plan.Items.Count > maxListed)
                builder.Append("\n• ").Append(L.Format("BlockEditor_AndMore", plan.Items.Count - maxListed));
        }
        if (plan.Failures.Count != 0)
        {
            builder.Append("\n\n").Append(L["BlockEditor_ImportPlanRefused"]);
            foreach (var failure in plan.Failures.Take(maxListed))
                builder.Append("\n• ").Append(failure.FileName).Append(": ").Append(DescribeFailure(failure));
            if (plan.Failures.Count > maxListed)
                builder.Append("\n• ").Append(L.Format("BlockEditor_AndMore", plan.Failures.Count - maxListed));
        }
        return builder.ToString();
    }

    /// <summary>Raw key, plus the resolved name when there is one.</summary>
    private static string BlockLabel(SaveBlockEntry entry) => entry.Name is null ? entry.KeyText : $"{entry.KeyText} ({entry.Name})";

    private static string DescribeFailure(SaveBlockImportFailure failure) => failure.Reason switch
    {
        SaveBlockImportFailureReason.UnrecognizedName => L["BlockEditor_FailName"],
        SaveBlockImportFailureReason.UnknownBlock => L["BlockEditor_FailUnknown"],
        SaveBlockImportFailureReason.NoData => L["BlockEditor_FailNoData"],
        SaveBlockImportFailureReason.SizeMismatch => L.Format("BlockEditor_FailSize", failure.ActualLength, failure.ExpectedLength),
        SaveBlockImportFailureReason.Duplicate => L["BlockEditor_FailDuplicate"],
        _ => L["BlockEditor_FailRead"],
    };

    [RelayCommand(CanExecute = nameof(CanUseAll))]
    private async Task CompareWithFileAsync()
    {
        var path = await _dialogService.OpenFileAsync(L["BlockEditor_CompareSelectFile"]);
        if (string.IsNullOrEmpty(path))
            return;
        var other = await LoadComparableAsync(path);
        if (other is null)
            return;
        await ShowComparisonAsync(BlockSet, other);
    }

    [RelayCommand]
    private async Task CompareFilesAsync()
    {
        var first = await _dialogService.OpenFileAsync(L["BlockEditor_CompareSelectFirst"]);
        if (string.IsNullOrEmpty(first))
            return;
        var second = await _dialogService.OpenFileAsync(L["BlockEditor_CompareSelectSecond"]);
        if (string.IsNullOrEmpty(second))
            return;
        var a = await LoadComparableAsync(first);
        if (a is null)
            return;
        var b = await LoadComparableAsync(second);
        if (b is null)
            return;
        await ShowComparisonAsync(a, b);
    }

    private async Task<SaveBlockSet?> LoadComparableAsync(string path)
    {
        SaveFile? save;
        try
        {
            save = _loadSave(path);
        }
        catch (Exception)
        {
            save = null;
        }
        var set = save is null ? null : SaveBlockSet.Create(save);
        if (set is { IsSupported: true })
            return set;
        await _dialogService.ShowErrorAsync(L["BlockEditor_CompareErrorTitle"],
            L.Format("BlockEditor_CompareInvalid", Path.GetFileName(path)));
        return null;
    }

    private async Task ShowComparisonAsync(SaveBlockSet before, SaveBlockSet after)
    {
        if (!SaveBlockSet.AreComparable(before, after))
        {
            await _dialogService.ShowErrorAsync(L["BlockEditor_CompareErrorTitle"], L["BlockEditor_CompareMismatch"]);
            return;
        }
        ApplyComparison(SaveBlockSet.Compare(before, after));
    }

    /// <summary>Loads compare results into the Compare tab.</summary>
    public void ApplyComparison(IReadOnlyList<SaveBlockDifference> differences)
    {
        CompareResults.Clear();
        foreach (var diff in differences
                     .OrderBy(z => z.Kind)
                     .ThenBy(z => z.Name is null)
                     .ThenBy(z => z.Name ?? z.KeyText, StringComparer.Ordinal))
        {
            CompareResults.Add(new BlockDifferenceRowViewModel(diff));
        }
        var added = differences.Count(z => z.Kind == SaveBlockDifferenceKind.Added);
        var removed = differences.Count(z => z.Kind == SaveBlockDifferenceKind.Removed);
        var changed = differences.Count - added - removed;
        CompareSummary = differences.Count == 0
            ? L["BlockEditor_CompareNoDifferences"]
            : L.Format("BlockEditor_CompareSummary", added, removed, changed);
    }

    internal static string FormatKind(SaveBlockDifferenceKind kind) => kind switch
    {
        SaveBlockDifferenceKind.Added => L["BlockEditor_KindAdded"],
        SaveBlockDifferenceKind.Removed => L["BlockEditor_KindRemoved"],
        SaveBlockDifferenceKind.TypeChanged => L["BlockEditor_KindType"],
        SaveBlockDifferenceKind.LengthChanged => L["BlockEditor_KindLength"],
        SaveBlockDifferenceKind.ValueChanged => L["BlockEditor_KindValue"],
        _ => L["BlockEditor_KindBytes"],
    };

    internal static string FormatDifference(SaveBlockDifference diff) => diff.Kind switch
    {
        SaveBlockDifferenceKind.Added => L.Format("BlockEditor_DiffBlockInfo", diff.NewType ?? string.Empty, diff.NewLength),
        SaveBlockDifferenceKind.Removed => L.Format("BlockEditor_DiffBlockInfo", diff.OldType ?? string.Empty, diff.OldLength),
        SaveBlockDifferenceKind.TypeChanged => L.Format("BlockEditor_DiffArrow", diff.OldType ?? string.Empty, diff.NewType ?? string.Empty),
        SaveBlockDifferenceKind.LengthChanged => L.Format("BlockEditor_DiffArrow", diff.OldLength, diff.NewLength),
        SaveBlockDifferenceKind.ValueChanged => L.Format("BlockEditor_DiffArrow", diff.OldValue ?? diff.OldType ?? string.Empty, diff.NewValue ?? diff.NewType ?? string.Empty),
        _ => L.Format("BlockEditor_DiffBytes", diff.ChangedBytes, diff.NewLength),
    };
}

/// <summary>One row in the block list. Reads the live block, so <see cref="Refresh"/> after a write updates it.</summary>
public sealed partial class BlockRowViewModel : ObservableObject
{
    public BlockRowViewModel(SaveBlockEntry entry) => Entry = entry;

    public SaveBlockEntry Entry { get; }

    public string Key => Entry.KeyText;
    public string Name => Entry.Name ?? string.Empty;
    public string TypeText => Entry.SubTypeName is null ? Entry.TypeName : $"{Entry.TypeName}<{Entry.SubTypeName}>";
    public string SizeString => FormattableString.Invariant($"0x{Entry.Length:X}");
    public string OffsetString => Entry.Offset >= 0 ? FormattableString.Invariant($"0x{Entry.Offset:X5}") : string.Empty;
    public string ValueText => Entry.ValueText ?? string.Empty;

    public bool Matches(string search)
    {
        if (search.Length == 0)
            return true;
        return Key.Contains(search, StringComparison.OrdinalIgnoreCase)
            || Name.Contains(search, StringComparison.OrdinalIgnoreCase)
            || TypeText.Contains(search, StringComparison.OrdinalIgnoreCase)
            || ValueText.Contains(search, StringComparison.OrdinalIgnoreCase)
            || (OffsetString.Length != 0 && OffsetString.Contains(search, StringComparison.OrdinalIgnoreCase));
    }

    public void Refresh() => OnPropertyChanged(nameof(ValueText));
}

/// <summary>One row of compare results.</summary>
public sealed class BlockDifferenceRowViewModel(SaveBlockDifference difference)
{
    public SaveBlockDifference Difference { get; } = difference;
    public string KindText { get; } = BlockEditorViewModel.FormatKind(difference.Kind);
    public string Key => Difference.KeyText;
    public string Name => Difference.Name ?? string.Empty;
    public string DetailText { get; } = BlockEditorViewModel.FormatDifference(difference);
}
