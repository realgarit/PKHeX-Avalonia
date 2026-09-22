using System;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PKHeX.Application.Abstractions;
using PKHeX.Core;
using static PKHeX.Core.SaveBlockAccessor9SV;

namespace PKHeX.Presentation.ViewModels;

/// <summary>
/// Misc editor for Gen 9 Scarlet/Violet saves covering LP, BP, Fly locations, TM Recipes, and more.
/// </summary>
public partial class Misc9EditorViewModel : ViewModelBase, ICloseableDialog
{
    private readonly SAV9SV _source;
    private readonly SAV9SV _working;

    public Action? CloseRequested { get; set; }

    public Misc9EditorViewModel(SAV9SV sav)
    {
        _source = sav;
        _working = sav.Clone() as SAV9SV ?? throw new InvalidOperationException("The SV save clone was not SAV9SV.");
        HasBlueberry = _working.SaveRevision >= 2;
        LoadCurrency();
        if (HasBlueberry)
            LoadBlueberry();
    }

    public bool HasBlueberry { get; }

    #region Currency

    [ObservableProperty] private uint _money;
    [ObservableProperty] private uint _leaguePoints;

    private void LoadCurrency()
    {
        Money = _working.Money;
        LeaguePoints = _working.LeaguePoints;
    }

    private void SaveCurrency()
    {
        _working.Money = Money;
        _working.LeaguePoints = LeaguePoints;
    }

    [RelayCommand]
    private void MaxMoney()
    {
        Money = (uint)_working.MaxMoney;
    }

    [RelayCommand]
    private void MaxLP()
    {
        LeaguePoints = 9_999_999;
    }

    #endregion

    #region Blueberry (DLC2)

    [ObservableProperty] private uint _questsDoneSolo;
    [ObservableProperty] private uint _questsDoneGroup;
    [ObservableProperty] private int _throwStyleIndex;
    [ObservableProperty] private bool _hasUnknownThrowStyle;

    public string[] ThrowStyles { get; } = Util.GetStringList("throw_styles", GameInfo.CurrentLanguage);

    private void LoadBlueberry()
    {
        var bbq = _working.BlueberryQuestRecord;
        QuestsDoneSolo = bbq.QuestsDoneSolo;
        QuestsDoneGroup = bbq.QuestsDoneGroup;
        var raw = (int)_working.ThrowStyle;
        HasUnknownThrowStyle = raw < 1 || raw > ThrowStyles.Length;
        ThrowStyleIndex = HasUnknownThrowStyle ? -1 : raw - 1;
    }

    partial void OnThrowStyleIndexChanged(int value)
    {
        if (value >= 0)
            HasUnknownThrowStyle = false;
    }

    private void SaveBlueberry()
    {
        if (!HasBlueberry) return;

        var bbq = _working.BlueberryQuestRecord;
        bbq.QuestsDoneSolo = QuestsDoneSolo;
        bbq.QuestsDoneGroup = QuestsDoneGroup;
        if (!HasUnknownThrowStyle && ThrowStyleIndex >= 0)
            _working.ThrowStyle = (ThrowStyle9)(ThrowStyleIndex + 1);
    }

    #endregion

    #region Unlocks

    [RelayCommand]
    private void UnlockAllFlyLocations()
    {
        ReadOnlySpan<uint> flyHashes =
        [
            FSYS_YMAP_FLY_01, FSYS_YMAP_FLY_02, FSYS_YMAP_FLY_03, FSYS_YMAP_FLY_04, FSYS_YMAP_FLY_05,
            FSYS_YMAP_FLY_06, FSYS_YMAP_FLY_07, FSYS_YMAP_FLY_08, FSYS_YMAP_FLY_09, FSYS_YMAP_FLY_10,
            FSYS_YMAP_FLY_11, FSYS_YMAP_FLY_12, FSYS_YMAP_FLY_13, FSYS_YMAP_FLY_14, FSYS_YMAP_FLY_15,
            FSYS_YMAP_FLY_16, FSYS_YMAP_FLY_17, FSYS_YMAP_FLY_18, FSYS_YMAP_FLY_19, FSYS_YMAP_FLY_20,
            FSYS_YMAP_FLY_21, FSYS_YMAP_FLY_22, FSYS_YMAP_FLY_23, FSYS_YMAP_FLY_24, FSYS_YMAP_FLY_25,
            FSYS_YMAP_FLY_26, FSYS_YMAP_FLY_27, FSYS_YMAP_FLY_28, FSYS_YMAP_FLY_29, FSYS_YMAP_FLY_30,
            FSYS_YMAP_FLY_31, FSYS_YMAP_FLY_32, FSYS_YMAP_FLY_33, FSYS_YMAP_FLY_34, FSYS_YMAP_FLY_35,
            FSYS_YMAP_FLY_MAGATAMA, FSYS_YMAP_FLY_MOKKAN, FSYS_YMAP_FLY_TSURUGI, FSYS_YMAP_FLY_UTSUWA,
            FSYS_YMAP_POKECEN_02, FSYS_YMAP_POKECEN_03, FSYS_YMAP_POKECEN_04, FSYS_YMAP_POKECEN_05,
            FSYS_YMAP_POKECEN_06, FSYS_YMAP_POKECEN_07, FSYS_YMAP_POKECEN_08, FSYS_YMAP_POKECEN_09,
            FSYS_YMAP_POKECEN_10, FSYS_YMAP_POKECEN_11, FSYS_YMAP_POKECEN_12, FSYS_YMAP_POKECEN_13,
            FSYS_YMAP_POKECEN_14, FSYS_YMAP_POKECEN_15, FSYS_YMAP_POKECEN_16, FSYS_YMAP_POKECEN_17,
            FSYS_YMAP_POKECEN_18, FSYS_YMAP_POKECEN_19, FSYS_YMAP_POKECEN_20, FSYS_YMAP_POKECEN_21,
            FSYS_YMAP_POKECEN_22, FSYS_YMAP_POKECEN_23, FSYS_YMAP_POKECEN_24, FSYS_YMAP_POKECEN_25,
            FSYS_YMAP_POKECEN_26, FSYS_YMAP_POKECEN_27, FSYS_YMAP_POKECEN_28, FSYS_YMAP_POKECEN_29,
            FSYS_YMAP_POKECEN_30, FSYS_YMAP_POKECEN_31, FSYS_YMAP_POKECEN_32, FSYS_YMAP_POKECEN_33,
            FSYS_YMAP_POKECEN_34, FSYS_YMAP_POKECEN_35,
            FSYS_YMAP_MAGATAMA, FSYS_YMAP_MOKKAN, FSYS_YMAP_TSURUGI, FSYS_YMAP_UTSUWA,
            FSYS_YMAP_SU1MAP_CHANGE, FSYS_YMAP_FLY_SU1_AREA10, FSYS_YMAP_FLY_SU1_BUSSTOP,
            FSYS_YMAP_FLY_SU1_CENTER01, FSYS_YMAP_FLY_SU1_PLAZA, FSYS_YMAP_FLY_SU1_SPOT01,
            FSYS_YMAP_FLY_SU1_SPOT02, FSYS_YMAP_FLY_SU1_SPOT03, FSYS_YMAP_FLY_SU1_SPOT04,
            FSYS_YMAP_FLY_SU1_SPOT05, FSYS_YMAP_FLY_SU1_SPOT06,
            FSYS_YMAP_S2_MAPCHANGE_ENABLE, FSYS_YMAP_FLY_SU2_DRAGON, FSYS_YMAP_FLY_SU2_ENTRANCE,
            FSYS_YMAP_FLY_SU2_FAIRY, FSYS_YMAP_FLY_SU2_HAGANE, FSYS_YMAP_FLY_SU2_HONOO,
            FSYS_YMAP_FLY_SU2_SPOT01, FSYS_YMAP_FLY_SU2_SPOT02, FSYS_YMAP_FLY_SU2_SPOT03,
            FSYS_YMAP_FLY_SU2_SPOT04, FSYS_YMAP_FLY_SU2_SPOT05, FSYS_YMAP_FLY_SU2_SPOT06,
            FSYS_YMAP_FLY_SU2_SPOT07, FSYS_YMAP_FLY_SU2_SPOT08, FSYS_YMAP_FLY_SU2_SPOT09,
            FSYS_YMAP_FLY_SU2_SPOT10, FSYS_YMAP_FLY_SU2_SPOT11, FSYS_YMAP_POKECEN_SU02,
        ];

        var accessor = _working.Accessor;
        foreach (var hash in flyHashes)
        {
            if (accessor.TryGetBlock(hash, out var block))
                block.ChangeBooleanType(SCTypeCode.Bool2);
        }
    }

    [RelayCommand]
    private void CollectAllStakes()
    {
        _working.CollectAllStakes();
    }

    [RelayCommand]
    private void UnlockAllTMRecipes()
    {
        _working.UnlockAllTMRecipes();
    }

    [RelayCommand]
    private void ActivateSnacksworthLegendaries()
    {
        _working.ActivateSnacksworthLegendaries();
    }

    [RelayCommand]
    private void UnlockAllCoaches()
    {
        _working.UnlockAllCoaches();
    }

    [RelayCommand]
    private void UnlockBikeUpgrades()
    {
        string[] blocks =
        [
            "FSYS_RIDE_DASH_ENABLE",
            "FSYS_RIDE_SWIM_ENABLE",
            "FSYS_RIDE_HIJUMP_ENABLE",
            "FSYS_RIDE_GLIDE_ENABLE",
            "FSYS_RIDE_CLIMB_ENABLE",
        ];

        var accessor = _working.Accessor;
        foreach (var block in blocks)
        {
            if (accessor.TryGetBlock(block, out var value))
                value.ChangeBooleanType(SCTypeCode.Bool2);
        }

        if (accessor.TryGetBlock("FSYS_RIDE_FLIGHT_ENABLE", out var fly))
            fly.ChangeBooleanType(SCTypeCode.Bool2);
    }

    [RelayCommand]
    private void UnlockBaseClothing()
    {
        PlayerFashionUnlock9.UnlockBase(_working.Accessor, _working.Gender);
    }

    [RelayCommand]
    private void UnlockAllThrowStyles()
    {
        _working.UnlockAllThrowStyles();
    }

    #endregion

    #region Commands

    [RelayCommand]
    private void Save()
    {
        SaveCurrency();
        SaveBlueberry();
        _source.CopyChangesFrom(_working);
        _source.State.Edited = true;
        CloseRequested?.Invoke();
    }

    [RelayCommand]
    private void Cancel() => CloseRequested?.Invoke();

    #endregion
}
