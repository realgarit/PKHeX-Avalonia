using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PKHeX.Core;

namespace PKHeX.Presentation.ViewModels;

public partial class RecordsEditorViewModel : ViewModelBase, ICloseableDialog
{
    private readonly SaveFile _sav;
    private readonly ITrainerStatRecord? _storage;
    private readonly Dictionary<int, string>? _recordNames;

    public RecordsEditorViewModel(SaveFile sav)
    {
        _sav = sav;
        _storage = sav is SAV5 gen5 ? new Gen5Records(gen5) : sav as ITrainerStatRecord;
        _recordNames = sav is SAV8BS ? Record8b.RecordList_8b : GetRecordList(sav.Generation);

        LoadRecords();
    }

    public Action? CloseRequested { get; set; }
    public bool CanSave => HasRecords && Records.All(r => r.CanCommit);

    public bool HasRecords => _storage is not null && _recordNames is not null;

    [ObservableProperty]
    private ObservableCollection<RecordItemViewModel> _records = [];

    [ObservableProperty]
    private string _searchText = string.Empty;

    [ObservableProperty]
    private ObservableCollection<RecordItemViewModel> _filteredRecords = [];

    partial void OnSearchTextChanged(string value)
    {
        FilterRecords();
    }

    private void LoadRecords()
    {
        foreach (var record in Records) record.PropertyChanged -= RecordChanged;
        Records.Clear();
        
        if (_storage is null || _recordNames is null) return;

        // Gen 5 reads decrypt the block in place. Inspect a clone to keep opening/closing read-only.
        var reader = _sav is SAV5 gen5 ? new Gen5Records((SAV5)gen5.Clone()) : _storage;
        foreach (var kvp in _recordNames.OrderBy(x => x.Key))
        {
            if (kvp.Key >= _storage.RecordCount) continue; // skip IDs outside save's valid range
            var value = reader.GetRecord(kvp.Key);
            var vm = new RecordItemViewModel(kvp.Key, kvp.Value, value, _storage);
            vm.PropertyChanged += RecordChanged;
            Records.Add(vm);
        }

        FilterRecords();
        SaveCommand.NotifyCanExecuteChanged();
    }

    private void RecordChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e) => SaveCommand.NotifyCanExecuteChanged();

    private void FilterRecords()
    {
        if (string.IsNullOrWhiteSpace(SearchText))
        {
            FilteredRecords = new ObservableCollection<RecordItemViewModel>(Records);
        }
        else
        {
            var search = SearchText.ToLowerInvariant();
            FilteredRecords = new ObservableCollection<RecordItemViewModel>(
                Records.Where(r => r.Name.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                                   r.Id.ToString().Contains(search)));
        }
    }

    [RelayCommand]
    private void RefreshRecords()
    {
        LoadRecords();
    }

    [RelayCommand(CanExecute = nameof(CanSave))]
    private void Save()
    {
        if (!CanSave || _storage is null) return;
        try
        {
            foreach (var record in Records.Where(r => r.IsChanged)) _storage.SetRecord(record.Id, record.Value);
        }
        finally
        {
            if (_sav is SAV5 gen5) gen5.Records.EndAccess();
        }
        _sav.State.Edited = true;
        CloseRequested?.Invoke();
    }

    [RelayCommand]
    private void Cancel()
    {
        LoadRecords();
        CloseRequested?.Invoke();
    }

    private sealed class Gen5Records(SAV5 save) : ITrainerStatRecord
    {
        public int RecordCount => Record5.Record32 + Record5.Record16;
        public int GetRecordOffset(int id) => Record5.GetOffset(id);
        public int GetRecordMax(int id) => id < Record5.Record32 ? (int)Record5.GetMax32(id) : Record5.GetMax16(id - Record5.Record32);
        public int GetRecord(int id) => id < Record5.Record32 ? (int)save.Records.GetRecord32(id) : save.Records.GetRecord16(id - Record5.Record32);
        public void SetRecord(int id, int value)
        {
            if (id < Record5.Record32) save.Records.SetRecord32(id, (uint)value);
            else save.Records.SetRecord16(id - Record5.Record32, (ushort)value);
        }
    }

    private static Dictionary<int, string>? GetRecordList(byte generation) => generation switch
    {
        5 => RecordLists.RecordList_5,
        6 => RecordLists.RecordList_6,
        7 => RecordLists.RecordList_7,
        8 => RecordLists.RecordList_8,
        _ => null
    };
}

public partial class RecordItemViewModel : ViewModelBase
{
    private readonly int _original;
    public RecordItemViewModel(int id, string name, int value, ITrainerStatRecord storage)
    {
        Id = id;
        Name = name;
        _value = _original = value;
        _valueText = value.ToString(CultureInfo.InvariantCulture);
        Maximum = storage.GetRecordMax(id);
    }

    public int Id { get; }
    public string Name { get; }
    public int Maximum { get; }
    public string LimitHint => $"0 … {Maximum}";
    public bool IsValid => int.TryParse(ValueText, NumberStyles.None, CultureInfo.InvariantCulture, out var value) && value >= 0 && value <= Maximum;
    // Existing out-of-range data is shown without normalization; only newly entered invalid text blocks Save.
    public bool CanCommit => IsValid || ValueText == _original.ToString(CultureInfo.InvariantCulture);
    public bool IsChanged => Value != _original;
    [ObservableProperty] private int _value;
    [ObservableProperty] private string _valueText;

    partial void OnValueChanged(int value) => ValueText = value.ToString(CultureInfo.InvariantCulture);
    partial void OnValueTextChanged(string value)
    {
        if (value == _original.ToString(CultureInfo.InvariantCulture))
        {
            _value = _original;
            OnPropertyChanged(nameof(Value));
        }
        else if (IsValid && int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var parsed))
        {
            _value = parsed;
            OnPropertyChanged(nameof(Value));
        }
        OnPropertyChanged(nameof(IsValid));
        OnPropertyChanged(nameof(IsChanged));
    }
}
