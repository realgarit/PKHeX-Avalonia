using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PKHeX.Core;
using PKHeX.Presentation.Localization;

namespace PKHeX.Presentation.ViewModels;

/// <summary>
/// Gen 6 O-Power editor. Each power is one row: its two stored levels and the unlock state of every
/// level the game defines for it, with readable localized names (for example "Hatching Power Lv. 1")
/// instead of Core's enum identifiers such as <c>Hatching1</c>.
/// </summary>
public partial class OPowerEditorViewModel : ViewModelBase
{
    private readonly OPower6? _block;

    public OPowerEditorViewModel(SaveFile sav)
    {
        if (sav is ISaveBlock6Main main)
        {
            _block = main.OPower;
            IsSupported = true;
            LoadData();
        }
    }

    public bool IsSupported { get; }

    [ObservableProperty]
    private byte _points;

    partial void OnPointsChanged(byte value)
    {
        if (_block is not null)
            _block.Points = value;
    }

    [ObservableProperty]
    private ObservableCollection<OPowerRowViewModel> _fieldPowers = [];

    [ObservableProperty]
    private ObservableCollection<OPowerRowViewModel> _battlePowers = [];

    /// <summary>The O-Power feature itself (<see cref="OPower6Index.Enable"/>).</summary>
    [ObservableProperty]
    private OPowerUnlockViewModel? _enable;

    /// <summary>Full Recovery has a single level and no stored use levels.</summary>
    [ObservableProperty]
    private OPowerUnlockViewModel? _fullRecovery;

    /// <summary>Column captions of the unlock cells: "1", "2", "3", "S", "MAX".</summary>
    public IReadOnlyList<string> UnlockColumns { get; } =
        ["1", "2", "3", LocalizedStrings.Instance["OPowerEditor_TierS"], LocalizedStrings.Instance["OPowerEditor_TierMax"]];

    private void LoadData()
    {
        if (_block is null) return;

        Points = _block.Points;
        Enable = new OPowerUnlockViewModel(LocalizedStrings.Instance["OPowerEditor_Enabled"], string.Empty, OPower6Index.Enable, _block);
        FullRecovery = new OPowerUnlockViewModel(OPowerNames.UnlockName(OPower6Index.FullRecovery), string.Empty, OPower6Index.FullRecovery, _block);

        FieldPowers = new ObservableCollection<OPowerRowViewModel>(Enumerable.Range(0, (int)OPower6FieldType.Count)
            .Select(i => (OPower6FieldType)i)
            .Select(type => new OPowerRowViewModel(OPowerNames.FamilyName(type),
                OPowerNames.UnlocksOf(index => index.GetFieldType() == type, _block),
                () => _block.GetLevel1(type), v => _block.SetLevel1(type, v),
                () => _block.GetLevel2(type), v => _block.SetLevel2(type, v))));

        BattlePowers = new ObservableCollection<OPowerRowViewModel>(Enumerable.Range(0, (int)OPower6BattleType.Count)
            .Select(i => (OPower6BattleType)i)
            .Select(type => new OPowerRowViewModel(OPowerNames.FamilyName(type),
                OPowerNames.UnlocksOf(index => index.GetBattleType() == type, _block),
                () => _block.GetLevel1(type), v => _block.SetLevel1(type, v),
                () => _block.GetLevel2(type), v => _block.SetLevel2(type, v))));
    }

    [RelayCommand]
    private void UnlockAll()
    {
        _block?.UnlockAll();
        LoadData();
    }

    [RelayCommand]
    private void ClearAll()
    {
        _block?.ClearAll();
        LoadData();
    }
}

/// <summary>Readable, localized O-Power names built from the Core power index.</summary>
public static class OPowerNames
{
    /// <summary>Resource key suffix of each power family, in <see cref="OPower6Index"/> order.</summary>
    private static string FamilyKey(OPower6Index index) => index switch
    {
        <= OPower6Index.HatchingMAX and >= OPower6Index.Hatching1 => "Hatching",
        <= OPower6Index.BargainMAX and >= OPower6Index.Bargain1 => "Bargain",
        <= OPower6Index.PrizeMoneyMAX and >= OPower6Index.PrizeMoney1 => "PrizeMoney",
        <= OPower6Index.ExperienceMAX and >= OPower6Index.Experience1 => "Experience",
        <= OPower6Index.CaptureMAX and >= OPower6Index.Capture1 => "Capture",
        <= OPower6Index.Encounter3 and >= OPower6Index.Encounter1 => "Encounter",
        <= OPower6Index.Stealth3 and >= OPower6Index.Stealth1 => "Stealth",
        <= OPower6Index.HPRestoring3 and >= OPower6Index.HPRestoring1 => "HPRestoring",
        <= OPower6Index.PPRestoring3 and >= OPower6Index.PPRestoring1 => "PPRestoring",
        OPower6Index.FullRecovery => "FullRecovery",
        <= OPower6Index.BefriendingMAX and >= OPower6Index.Befriending1 => "Befriending",
        <= OPower6Index.Attack3 and >= OPower6Index.Attack1 => "Attack",
        <= OPower6Index.Defense3 and >= OPower6Index.Defense1 => "Defense",
        <= OPower6Index.SpecialAttack3 and >= OPower6Index.SpecialAttack1 => "Sp_Attack",
        <= OPower6Index.SpecialDefense3 and >= OPower6Index.SpecialDefense1 => "Sp_Defense",
        <= OPower6Index.Speed3 and >= OPower6Index.Speed1 => "Speed",
        <= OPower6Index.Critical3 and >= OPower6Index.Critical1 => "Critical",
        <= OPower6Index.Accuracy3 and >= OPower6Index.Accuracy1 => "Accuracy",
        _ => throw new ArgumentOutOfRangeException(nameof(index), index, null),
    };

    /// <summary>Readable power name, for example "Hatching Power".</summary>
    public static string FamilyName(OPower6FieldType type) => PowerName(type.ToString());

    /// <summary>Readable power name, for example "Attack Power".</summary>
    public static string FamilyName(OPower6BattleType type) => PowerName(type.ToString());

    private static string PowerName(string familyKey) =>
        LocalizedStrings.Instance.Format("OPowerEditor_PowerName", LocalizedStrings.Instance[$"OPowerEditor_Power_{familyKey}"]);

    /// <summary>Tier caption of one unlock: "1", "2", "3", "S" or "MAX" (localized S/MAX).</summary>
    public static string TierCaption(OPower6Index index)
    {
        var name = index.ToString();
        if (name.EndsWith("MAX", StringComparison.Ordinal))
            return LocalizedStrings.Instance["OPowerEditor_TierMax"];
        if (name.EndsWith('S'))
            return LocalizedStrings.Instance["OPowerEditor_TierS"];
        return char.IsDigit(name[^1]) ? name[^1].ToString() : string.Empty;
    }

    /// <summary>Full readable name, for example "Hatching Power Lv. 1", "Bargain Power S" or "Full Recovery Power".</summary>
    public static string UnlockName(OPower6Index index)
    {
        var power = PowerName(FamilyKey(index));
        if (index == OPower6Index.FullRecovery)
            return power;
        var tier = TierCaption(index);
        return char.IsDigit(tier[0])
            ? LocalizedStrings.Instance.Format("OPowerEditor_UnlockLevelName", power, tier)
            : LocalizedStrings.Instance.Format("OPowerEditor_UnlockTierName", power, tier);
    }

    internal static IReadOnlyList<OPowerUnlockViewModel> UnlocksOf(Func<OPower6Index, bool> belongs, OPower6 block) =>
        Enumerable.Range(1, (int)OPower6Index.Count - 1)
            .Select(i => (OPower6Index)i)
            .Where(belongs)
            .Select(index => new OPowerUnlockViewModel(UnlockName(index), TierCaption(index), index, block))
            .ToList();
}

/// <summary>One O-Power family: two stored levels and its unlockable levels.</summary>
public partial class OPowerRowViewModel : ViewModelBase
{
    private readonly Action<byte> _setLevel1;
    private readonly Action<byte> _setLevel2;

    public OPowerRowViewModel(string name, IReadOnlyList<OPowerUnlockViewModel> unlocks,
        Func<byte> getLevel1, Action<byte> setLevel1, Func<byte> getLevel2, Action<byte> setLevel2)
    {
        Name = name;
        Unlocks = unlocks;
        _setLevel1 = setLevel1;
        _setLevel2 = setLevel2;
        _level1 = getLevel1();
        _level2 = getLevel2();
        Level1Name = LocalizedStrings.Instance.Format("OPowerEditor_Level1Name", name);
        Level2Name = LocalizedStrings.Instance.Format("OPowerEditor_Level2Name", name);
    }

    public string Name { get; }
    public string Level1Name { get; }
    public string Level2Name { get; }
    public IReadOnlyList<OPowerUnlockViewModel> Unlocks { get; }

    [ObservableProperty]
    private byte _level1;

    partial void OnLevel1Changed(byte value) => _setLevel1(value);

    [ObservableProperty]
    private byte _level2;

    partial void OnLevel2Changed(byte value) => _setLevel2(value);
}

public partial class OPowerUnlockViewModel : ViewModelBase
{
    private readonly OPower6Index _idx;
    private readonly OPower6 _block;

    public OPowerUnlockViewModel(string name, string tier, OPower6Index idx, OPower6 block)
    {
        Name = name;
        Tier = tier;
        _idx = idx;
        _block = block;
        _isUnlocked = block.GetState(idx) == OPowerFlagState.Unlocked;
    }

    /// <summary>Readable full name, used as the checkbox automation name and tooltip.</summary>
    public string Name { get; }

    /// <summary>Short column caption ("1", "2", "3", "S" or "MAX").</summary>
    public string Tier { get; }

    public OPower6Index Index => _idx;

    [ObservableProperty]
    private bool _isUnlocked;

    partial void OnIsUnlockedChanged(bool value)
    {
        _block.SetState(_idx, value ? OPowerFlagState.Unlocked : OPowerFlagState.Locked);
    }
}
