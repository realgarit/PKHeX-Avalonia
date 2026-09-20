using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PKHeX.Core;

namespace PKHeX.Presentation.ViewModels;

public partial class SealStickers8bEditorViewModel : ViewModelBase, ICloseableDialog
{
    private readonly SAV8BS? _source;
    private readonly SAV8BS? _working;
    private readonly IReadOnlyList<SealSticker8b>? _allItems;
    private readonly string[] _itemNames;

    public Action? CloseRequested { get; set; }

    public SealStickers8bEditorViewModel(SaveFile sav)
    {
        _source = sav as SAV8BS;
        _working = _source?.Clone() as SAV8BS;
        IsSupported = _working is not null;
        _itemNames = Util.GetStringList("stickers", GameInfo.CurrentLanguage);

        if (_working is not null)
        {
            _allItems = _working.SealList.ReadItems();
            LoadItems();
        }
    }

    public bool IsSupported { get; }

    [ObservableProperty]
    private ObservableCollection<SealSticker8bViewModel> _items = [];

    private void LoadItems()
    {
        if (_allItems is null) return;

        Items.Clear();
        foreach (var item in _allItems)
        {
            var name = item.Index < _itemNames.Length ? _itemNames[item.Index] : $"Seal {item.Index}";
            if (string.IsNullOrWhiteSpace(name)) continue;

            Items.Add(new SealSticker8bViewModel(item, name));
        }
    }

    [RelayCommand]
    private void SetAllMax()
    {
        foreach (var item in Items)
        {
            item.Count = SealSticker8b.MaxValue;
            item.TotalCount = SealSticker8b.MaxValue;
            item.IsGet = true;
        }
    }

    [RelayCommand]
    private void SetAllNone()
    {
        foreach (var item in Items)
        {
            item.Count = 0;
            item.TotalCount = 0;
            item.IsGet = false;
        }
    }

    [RelayCommand]
    private void Save()
    {
        if (_source is null || _working is null) return;
        _working.SealList.WriteItems(_allItems!);
        _source.CopyChangesFrom(_working);
        CloseRequested?.Invoke();
    }

    [RelayCommand]
    private void Cancel() => CloseRequested?.Invoke();
}

public partial class SealSticker8bViewModel : ViewModelBase
{
    private readonly SealSticker8b _item;

    public SealSticker8bViewModel(SealSticker8b item, string name)
    {
        _item = item;
        Name = name;
        _count = item.Count;
        _totalCount = item.TotalCount;
        _isGet = item.IsGet;
        NormalizeValues();
    }

    public int Index => _item.Index;
    public string Name { get; }
    public int MaxValue => SealSticker8b.MaxValue;

    private int _count;
    public int Count
    {
        get => _count;
        set
        {
            if (SetProperty(ref _count, value))
                NormalizeValues();
        }
    }

    private int _totalCount;
    public int TotalCount
    {
        get => _totalCount;
        set
        {
            if (SetProperty(ref _totalCount, value))
                NormalizeValues();
        }
    }

    private bool _isGet;
    public bool IsGet
    {
        get => _isGet;
        set
        {
            if (SetProperty(ref _isGet, value))
            {
                if (!value)
                {
                    _count = 0;
                    _totalCount = 0;
                    OnPropertyChanged(nameof(Count));
                    OnPropertyChanged(nameof(TotalCount));
                }
                NormalizeValues();
            }
        }
    }

    private void NormalizeValues()
    {
        var count = Math.Clamp(_count, 0, MaxValue);
        var totalCount = Math.Clamp(_totalCount, 0, MaxValue);
        var isGet = _isGet;

        // A held sticker must have a lifetime total at least as large as its current count.
        if (count > 0)
        {
            isGet = true;
            totalCount = Math.Max(totalCount, count);
        }

        // Clearing Obtained clears both stored counters. This keeps the three persisted fields
        // from representing a state the game itself cannot produce.
        if (!isGet)
        {
            count = 0;
            totalCount = 0;
        }

        if (_count != count)
        {
            _count = count;
            OnPropertyChanged(nameof(Count));
        }
        if (_totalCount != totalCount)
        {
            _totalCount = totalCount;
            OnPropertyChanged(nameof(TotalCount));
        }
        if (_isGet != isGet)
        {
            _isGet = isGet;
            OnPropertyChanged(nameof(IsGet));
        }

        _item.Count = count;
        _item.TotalCount = totalCount;
        _item.IsGet = isGet;
    }
}
