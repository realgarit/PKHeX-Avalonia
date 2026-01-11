
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace PKHeX.Avalonia.ViewModels;

public partial class PokemonEditorViewModel
{
    // Group 2: Health & Status
    [ObservableProperty]
    private int _statHPCurrent;

    [ObservableProperty]
    private int _statHPMax;

    [ObservableProperty]
    private int _statNature;

    [ObservableProperty]
    private int _hpType;

    // IVs
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Stat_HP), nameof(Stat_ATK), nameof(Stat_DEF), nameof(Stat_SPA), nameof(Stat_SPD), nameof(Stat_SPE), nameof(IVTotal))]
    private int _ivHP;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Stat_HP), nameof(Stat_ATK), nameof(Stat_DEF), nameof(Stat_SPA), nameof(Stat_SPD), nameof(Stat_SPE), nameof(IVTotal))]
    private int _ivATK;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Stat_HP), nameof(Stat_ATK), nameof(Stat_DEF), nameof(Stat_SPA), nameof(Stat_SPD), nameof(Stat_SPE), nameof(IVTotal))]
    private int _ivDEF;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Stat_HP), nameof(Stat_ATK), nameof(Stat_DEF), nameof(Stat_SPA), nameof(Stat_SPD), nameof(Stat_SPE), nameof(IVTotal))]
    private int _ivSPA;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Stat_HP), nameof(Stat_ATK), nameof(Stat_DEF), nameof(Stat_SPA), nameof(Stat_SPD), nameof(Stat_SPE), nameof(IVTotal))]
    private int _ivSPD;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Stat_HP), nameof(Stat_ATK), nameof(Stat_DEF), nameof(Stat_SPA), nameof(Stat_SPD), nameof(Stat_SPE), nameof(IVTotal))]
    private int _ivSPE;

    // EVs
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Stat_HP), nameof(Stat_ATK), nameof(Stat_DEF), nameof(Stat_SPA), nameof(Stat_SPD), nameof(Stat_SPE), nameof(EVTotal))]
    private int _evHP;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Stat_HP), nameof(Stat_ATK), nameof(Stat_DEF), nameof(Stat_SPA), nameof(Stat_SPD), nameof(Stat_SPE), nameof(EVTotal))]
    private int _evATK;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Stat_HP), nameof(Stat_ATK), nameof(Stat_DEF), nameof(Stat_SPA), nameof(Stat_SPD), nameof(Stat_SPE), nameof(EVTotal))]
    private int _evDEF;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Stat_HP), nameof(Stat_ATK), nameof(Stat_DEF), nameof(Stat_SPA), nameof(Stat_SPD), nameof(Stat_SPE), nameof(EVTotal))]
    private int _evSPA;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Stat_HP), nameof(Stat_ATK), nameof(Stat_DEF), nameof(Stat_SPA), nameof(Stat_SPD), nameof(Stat_SPE), nameof(EVTotal))]
    private int _evSPD;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Stat_HP), nameof(Stat_ATK), nameof(Stat_DEF), nameof(Stat_SPA), nameof(Stat_SPD), nameof(Stat_SPE), nameof(EVTotal))]
    private int _evSPE;

    // Computed Stats
    public int Stat_HP { get { RecalculateStats(); return _pk.Stat_HPMax; } }
    public int Stat_ATK { get { RecalculateStats(); return _pk.Stat_ATK; } }
    public int Stat_DEF { get { RecalculateStats(); return _pk.Stat_DEF; } }
    public int Stat_SPA { get { RecalculateStats(); return _pk.Stat_SPA; } }
    public int Stat_SPD { get { RecalculateStats(); return _pk.Stat_SPD; } }
    public int Stat_SPE { get { RecalculateStats(); return _pk.Stat_SPE; } }

    public int IVTotal => IvHP + IvATK + IvDEF + IvSPA + IvSPD + IvSPE;
    public int EVTotal => EvHP + EvATK + EvDEF + EvSPA + EvSPD + EvSPE;

    private void RecalculateStats()
    {
        if (_isLoading) return; // Don't overwrite _pk during loading

        _pk.Stat_Level = (byte)Level;
        _pk.IV_HP = IvHP;
        _pk.IV_ATK = IvATK;
        _pk.IV_DEF = IvDEF;
        _pk.IV_SPA = IvSPA;
        _pk.IV_SPD = IvSPD;
        _pk.IV_SPE = IvSPE;
        _pk.EV_HP = EvHP;
        _pk.EV_ATK = EvATK;
        _pk.EV_DEF = EvDEF;
        _pk.EV_SPA = EvSPA;
        _pk.EV_SPD = EvSPD;
        _pk.EV_SPE = EvSPE;
        _pk.ResetPartyStats();
    }

    [RelayCommand]
    private void SetMaxIVs()
    {
        IvHP = 31;
        IvATK = 31;
        IvDEF = 31;
        IvSPA = 31;
        IvSPD = 31;
        IvSPE = 31;
    }

    [RelayCommand]
    private void ClearEVs()
    {
        EvHP = 0;
        EvATK = 0;
        EvDEF = 0;
        EvSPA = 0;
        EvSPD = 0;
        EvSPE = 0;
    }

    partial void OnIvHPChanged(int value) { if (!_isLoading) Validate(); }
    partial void OnIvATKChanged(int value) { if (!_isLoading) Validate(); }
    partial void OnIvDEFChanged(int value) { if (!_isLoading) Validate(); }
    partial void OnIvSPAChanged(int value) { if (!_isLoading) Validate(); }
    partial void OnIvSPDChanged(int value) { if (!_isLoading) Validate(); }
    partial void OnIvSPEChanged(int value) { if (!_isLoading) Validate(); }
    partial void OnEvHPChanged(int value) { if (!_isLoading) Validate(); }
    partial void OnEvATKChanged(int value) { if (!_isLoading) Validate(); }
    partial void OnEvDEFChanged(int value) { if (!_isLoading) Validate(); }
    partial void OnEvSPAChanged(int value) { if (!_isLoading) Validate(); }
    partial void OnEvSPDChanged(int value) { if (!_isLoading) Validate(); }
    partial void OnEvSPEChanged(int value) { if (!_isLoading) Validate(); }
}
