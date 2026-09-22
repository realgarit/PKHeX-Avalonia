using System;
using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PKHeX.Application.Abstractions;
using PKHeX.Core;

namespace PKHeX.Presentation.ViewModels;

public partial class BattlePassEditorViewModel : ViewModelBase, ICloseableDialog
{
    private readonly SAV4BR _source;
    private SAV4BR _working;
    private BattlePassAccessor _accessor;

    public Action? CloseRequested { get; set; }

    public BattlePassEditorViewModel(SaveFile sav)
    {
        _source = (SAV4BR)sav;
        _working = (SAV4BR)_source.Clone();
        _accessor = _working.BattlePasses;
        IsSupported = true;

        LoadPasses();
    }

    public bool IsSupported { get; }

    [ObservableProperty]
    private ObservableCollection<BattlePassEntryViewModel> _passes = [];

    [ObservableProperty]
    private BattlePassEntryViewModel? _selectedPass;

    private void LoadPasses()
    {
        Passes.Clear();
        for (int i = 0; i < BattlePassAccessor.PASS_COUNT; i++)
        {
            var pass = _accessor[i];
            Passes.Add(new BattlePassEntryViewModel(i, pass, _working));
        }

        if (Passes.Count > 0)
            SelectedPass = Passes[0];
    }

    [RelayCommand]
    private void UnlockAll()
    {
        _accessor.UnlockAllCustomPasses();
        _accessor.UnlockAllRentalPasses();
        LoadPasses();
    }

    [RelayCommand]
    private void Save()
    {
        _source.CopyChangesFrom(_working);
        _source.State.Edited = true;
        CloseRequested?.Invoke();
    }

    [RelayCommand]
    private void Refresh()
    {
        _working = (SAV4BR)_source.Clone();
        _accessor = _working.BattlePasses;
        LoadPasses();
    }

    [RelayCommand]
    private void Cancel() => CloseRequested?.Invoke();
}

public partial class BattlePassEntryViewModel : ViewModelBase
{
    private readonly BattlePass _pass;

    public BattlePassEntryViewModel(int index, BattlePass pass, SAV4BR sav)
    {
        Index = index;
        _pass = pass;

        _name = pass.Name;
        _tid = pass.TID;
        _sid = pass.SID;
        _model = pass.Model;
        _skin = pass.Skin;
        
        _greeting = pass.Greeting;
        _sentOut = pass.SentOut;
        _win = pass.Win;
        _lose = pass.Lose;

        _battles = pass.Battles;
    }

    public int Index { get; }
    public string DisplayName => $"{Index + 1:00} - {Name}";

    [ObservableProperty] private string _name;
    partial void OnNameChanged(string value) => _pass.Name = value;

    [ObservableProperty] private ushort _tid;
    partial void OnTidChanged(ushort value) => _pass.TID = value;

    [ObservableProperty] private ushort _sid;
    partial void OnSidChanged(ushort value) => _pass.SID = value;

    [ObservableProperty] private int _model;
    partial void OnModelChanged(int value) => _pass.Model = value;

    [ObservableProperty] private int _skin;
    partial void OnSkinChanged(int value) => _pass.Skin = value;

    [ObservableProperty] private string _greeting;
    partial void OnGreetingChanged(string value) => _pass.Greeting = value;

    [ObservableProperty] private string _sentOut;
    partial void OnSentOutChanged(string value) => _pass.SentOut = value;

    [ObservableProperty] private string _win;
    partial void OnWinChanged(string value) => _pass.Win = value;

    [ObservableProperty] private string _lose;
    partial void OnLoseChanged(string value) => _pass.Lose = value;

    [ObservableProperty] private int _battles;
    partial void OnBattlesChanged(int value) => _pass.Battles = value;
}
