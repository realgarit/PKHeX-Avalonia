using System;
using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PKHeX.Application.Abstractions;
using PKHeX.Core;
using PKHeX.Presentation.Localization;

namespace PKHeX.Presentation.ViewModels;

public partial class MoveShopEditorViewModel : ViewModelBase, ICloseableDialog
{
    private readonly PKM _source;
    private readonly PKM _working;
    private readonly IMoveShop8 _shop;
    private readonly IMoveShop8Mastery _mastery;
    
    public ObservableCollection<MoveShopItemViewModel> Moves { get; } = [];

    public Action? CloseRequested { get; set; }

    public MoveShopEditorViewModel(PKM pkm)
    {
        _source = pkm;
        _working = pkm.Clone();
        _shop = _working as IMoveShop8 ?? throw new ArgumentException("The Pokémon does not support Move Shop records.", nameof(pkm));
        _mastery = _working as IMoveShop8Mastery ?? throw new ArgumentException("The Pokémon does not support Move Shop mastery.", nameof(pkm));

        PopulateRecords();
    }

    private void PopulateRecords()
    {
        var names = GameInfo.Strings.Move;
        var indexes = _shop.Permit.RecordPermitIndexes;
        
        for (int i = 0; i < indexes.Length; i++)
        {
            var move = indexes[i];
            var isValid = _shop.Permit.IsRecordPermitted(i);
            var type = MoveInfo.GetType(move, _working.Context);
            var name = names[move];

            var item = new MoveShopItemViewModel(i, move, name, type, isValid);
            item.IsPurchased = _shop.GetPurchasedRecordFlag(i);
            item.IsMastered = _mastery.GetMasteredRecordFlag(i);

            Moves.Add(item);
        }
    }

    [RelayCommand]
    private void Save()
    {
        foreach (var item in Moves)
        {
            var purchased = item.IsPermitted && item.IsPurchased;
            var mastered = purchased && item.IsMastered;
            _shop.SetPurchasedRecordFlag(item.Index, purchased);
            _mastery.SetMasteredRecordFlag(item.Index, mastered);
        }
        _working.Data.CopyTo(_source.Data);
        _source.RefreshChecksum();
        CloseRequested?.Invoke();
    }

    [RelayCommand]
    private void SetAll()
    {
        _mastery.SetMoveShopFlagsAll(_working);
        ReloadFlags();
    }

    [RelayCommand]
    private void SetAllPurchased()
    {
        _mastery.SetPurchasedFlagsAll(_working);
        ReloadFlags();
    }

    [RelayCommand]
    private void SetNone()
    {
        _shop.ClearMoveShopFlags();
        ReloadFlags();
    }

    [RelayCommand]
    private void Cancel() => CloseRequested?.Invoke();

    private void ReloadFlags()
    {
        foreach (var item in Moves)
        {
            item.IsPurchased = _shop.GetPurchasedRecordFlag(item.Index);
            item.IsMastered = _mastery.GetMasteredRecordFlag(item.Index);
        }
    }
}

public partial class MoveShopItemViewModel : ObservableObject
{
    public int Index { get; }
    public int MoveID { get; }
    public string Name { get; }
    public int Type { get; }
    public bool IsPermitted { get; }
    public string TypeName => Type >= 0 && Type < GameInfo.Strings.Types.Count ? GameInfo.Strings.Types[Type] : Type.ToString();
    public string PermissionStatus => IsPermitted
        ? LocalizedStrings.Instance["MoveShopEditor_Permitted"]
        : LocalizedStrings.Instance["MoveShopEditor_NotPermitted"];

    [ObservableProperty]
    private bool _isPurchased;

    [ObservableProperty]
    private bool _isMastered;

    partial void OnIsPurchasedChanged(bool value)
    {
        if (value && !IsPermitted)
            IsPurchased = false;
        else if (!value)
            IsMastered = false;
    }

    partial void OnIsMasteredChanged(bool value)
    {
        if (value && !IsPermitted)
            IsMastered = false;
        else if (value && !IsPurchased)
            IsPurchased = true;
    }
    
    // For sorting/display
    public string IndexDisplay => $"{Index + 1:00}";

    public MoveShopItemViewModel(int index, int moveId, string name, int type, bool isPermitted)
    {
        Index = index;
        MoveID = moveId;
        Name = name;
        Type = type;
        IsPermitted = isPermitted;
    }
}
