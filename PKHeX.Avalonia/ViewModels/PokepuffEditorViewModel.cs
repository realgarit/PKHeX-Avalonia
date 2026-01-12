using System;
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PKHeX.Core;

namespace PKHeX.Avalonia.ViewModels;

public partial class PokepuffEditorViewModel : ViewModelBase
{
    private readonly SaveFile _sav;
    private readonly Puff6? _puff;

    public PokepuffEditorViewModel(SaveFile sav)
    {
        _sav = sav;

        if (sav is ISaveBlock6Main main)
        {
            _puff = main.Puff;
            IsSupported = true;
            LoadData();
        }
    }

    public bool IsSupported { get; }

    [ObservableProperty]
    private ObservableCollection<PokepuffSlotViewModel> _puffs = [];

    private void LoadData()
    {
        if (_puff is null) return;

        Puffs.Clear();
        var puffData = _puff.GetPuffs();
        var puffNames = GameInfo.Strings.puffs;

        for (int i = 0; i < puffData.Length; i++)
        {
            var puffValue = puffData[i];
            Puffs.Add(new PokepuffSlotViewModel(i, puffValue, puffNames, SetPuff));
        }
    }

    private void SetPuff(int index, byte value)
    {
        if (_puff is null) return;
        var puffs = _puff.GetPuffs();
        puffs[index] = value;
        _puff.SetPuffs(puffs);
    }

    [RelayCommand]
    private void GiveAll()
    {
        _puff?.MaxCheat(special: true);
        LoadData();
    }

    [RelayCommand]
    private void GiveBest()
    {
        _puff?.MaxCheat(special: false);
        LoadData();
    }

    [RelayCommand]
    private void ClearAll()
    {
        _puff?.Reset();
        LoadData();
    }

    [RelayCommand]
    private void Sort()
    {
        _puff?.Sort(reverse: false);
        LoadData();
    }

    [RelayCommand]
    private void Refresh() => LoadData();
}

public partial class PokepuffSlotViewModel : ViewModelBase
{
    private readonly Action<int, byte> _onChanged;
    private readonly string[] _puffNames;

    public PokepuffSlotViewModel(int index, byte value, string[] puffNames, Action<int, byte> onChanged)
    {
        Index = index;
        _value = value;
        _puffNames = puffNames;
        _onChanged = onChanged;
    }

    public int Index { get; }
    public string SlotLabel => $"Slot {Index + 1}";

    [ObservableProperty]
    private byte _value;

    partial void OnValueChanged(byte value)
    {
        _onChanged(Index, value);
        OnPropertyChanged(nameof(Name));
    }

    public string Name => Value < _puffNames.Length ? _puffNames[Value] : $"Unknown ({Value})";
}
