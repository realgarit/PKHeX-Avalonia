using System.Collections.ObjectModel;
using Avalonia.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PKHeX.Avalonia.Services;
using PKHeX.Core;

namespace PKHeX.Avalonia.ViewModels;

public partial class PokemonEditorViewModel : ViewModelBase
{
    private PKM _pk;
    private readonly SaveFile _sav;
    private readonly ISpriteRenderer _spriteRenderer;
    private readonly IDialogService _dialogService;
    private bool _isLoading; // Flag to prevent modifying _pk during load

    // Data sources (mostly filtered by SaveFile context)
    public IReadOnlyList<ComboItem> SpeciesList { get; }
    public IReadOnlyList<ComboItem> MoveList { get; }
    public IReadOnlyList<ComboItem> NatureList { get; }
    public IReadOnlyList<ComboItem> BallList { get; }
    public IReadOnlyList<ComboItem> ItemList { get; }
    public IReadOnlyList<ComboItem> OriginGameList { get; }
    public IReadOnlyList<ComboItem> RelearnMoveDataSource { get; }
    public IReadOnlyList<ComboItem> GenderList { get; } = [
        new ComboItem("Male", 0),
        new ComboItem("Female", 1),
        new ComboItem("Genderless", 2)
    ];

    public IReadOnlyList<ComboItem> LanguageList { get; }

    // Dynamic lists
    [ObservableProperty]
    private ObservableCollection<ComboItem> _abilityList = [];

    [ObservableProperty]
    private ObservableCollection<ComboItem> _formList = [];

    [ObservableProperty]
    private ObservableCollection<ComboItem> _metLocationList = [];

    [ObservableProperty]
    private ObservableCollection<ComboItem> _eggLocationList = [];

    [ObservableProperty]
    private bool _isLegal;

    [ObservableProperty]
    private string _legalityReport = string.Empty;

    [ObservableProperty]
    private Bitmap? _sprite;

    [ObservableProperty]
    private string _title = "Pokémon Editor";

    // Basic Info
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Sprite))]
    private int _species;

    [ObservableProperty]
    private string _version = string.Empty; // Read-only game name

    [ObservableProperty]
    private bool _valid; // Legality fast-check

    [ObservableProperty]
    private bool _isNicknamed;

    [ObservableProperty]
    private uint _id32;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Sprite))]
    private int _form;

    [ObservableProperty]
    private string _nickname = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Stat_HP), nameof(Stat_ATK), nameof(Stat_DEF), nameof(Stat_SPA), nameof(Stat_SPD), nameof(Stat_SPE))]
    private int _level;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Stat_HP), nameof(Stat_ATK), nameof(Stat_DEF), nameof(Stat_SPA), nameof(Stat_SPD), nameof(Stat_SPE))]
    private int _nature;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Stat_HP), nameof(Stat_ATK), nameof(Stat_DEF), nameof(Stat_SPA), nameof(Stat_SPD), nameof(Stat_SPE))]
    private int _ability;

    [ObservableProperty]
    private int _heldItem;

    [ObservableProperty]
    private int _ball;

    [ObservableProperty]
    private int _gender;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Sprite))]
    private bool _isShiny;

    [ObservableProperty]
    private bool _isEgg;

    [ObservableProperty]
    private bool _isFatefulEncounter;

    [ObservableProperty]
    private int _happiness;

    [ObservableProperty]
    private int _sid;

    // Group 2: Health & Status
    [ObservableProperty]
    private int _statHPCurrent;

    [ObservableProperty]
    private int _statHPMax;

    [ObservableProperty]
    private int _statusCondition;

    // Group 3: Trainer & Friendship
    [ObservableProperty]
    private int _originalTrainerFriendship;

    [ObservableProperty]
    private string _handlingTrainerName = string.Empty;

    [ObservableProperty]
    private int _handlingTrainerGender;

    [ObservableProperty]
    private int _handlingTrainerFriendship;

    [ObservableProperty]
    private int _currentHandler;

    // Group 4: Misc
    [ObservableProperty]
    private int _abilityNumber;

    [ObservableProperty]
    private int _statNature;

    [ObservableProperty]
    private int _hpType;

    [ObservableProperty]
    private bool _isPokerusInfected;

    [ObservableProperty]
    private bool _isPokerusCured;

    // PID/EC
    [ObservableProperty]
    private string _pid = string.Empty;

    [ObservableProperty]
    private string _encryptionConstant = string.Empty;

    // EXP
    [ObservableProperty]
    private long _exp;

    // Language
    [ObservableProperty]
    private int _language;

    // Pokerus
    [ObservableProperty]
    private int _pkrsStrain;

    [ObservableProperty]
    private int _pkrsDays;

    // PP
    [ObservableProperty]
    private int _pp1;

    [ObservableProperty]
    private int _pp2;

    [ObservableProperty]
    private int _pp3;

    [ObservableProperty]
    private int _pp4;

    [ObservableProperty]
    private int _ppUps1;

    [ObservableProperty]
    private int _ppUps2;

    [ObservableProperty]
    private int _ppUps3;

    [ObservableProperty]
    private int _ppUps4;

    // Met Info
    [ObservableProperty]
    private int _originGame;

    [ObservableProperty]
    private int _metLocation;

    [ObservableProperty]
    private int _eggLocation;

    [ObservableProperty]
    private int _metLevel;

    [ObservableProperty]
    private DateTimeOffset? _metDate;

    [ObservableProperty]
    private DateTimeOffset? _eggDate;

    // Moves
    [ObservableProperty]
    private int _move1;

    [ObservableProperty]
    private int _move2;

    [ObservableProperty]
    private int _move3;

    [ObservableProperty]
    private int _move4;

    // Relearn Moves
    [ObservableProperty]
    private int _relearnMove1;

    [ObservableProperty]
    private int _relearnMove2;

    [ObservableProperty]
    private int _relearnMove3;

    [ObservableProperty]
    private int _relearnMove4;

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
    // Computed Stats (ensure they refresh by calling RecalculateStats)
    public int Stat_HP { get { RecalculateStats(); return _pk.Stat_HPMax; } }
    public int Stat_ATK { get { RecalculateStats(); return _pk.Stat_ATK; } }
    public int Stat_DEF { get { RecalculateStats(); return _pk.Stat_DEF; } }
    public int Stat_SPA { get { RecalculateStats(); return _pk.Stat_SPA; } }
    public int Stat_SPD { get { RecalculateStats(); return _pk.Stat_SPD; } }
    public int Stat_SPE { get { RecalculateStats(); return _pk.Stat_SPE; } }

    public int IVTotal => IvHP + IvATK + IvDEF + IvSPA + IvSPD + IvSPE;
    public int EVTotal => EvHP + EvATK + EvDEF + EvSPA + EvSPD + EvSPE;

    // OT Info
    [ObservableProperty]
    private string _originalTrainerName = string.Empty;

    [ObservableProperty]
    private long _trainerID;

    [ObservableProperty]
    private int _originalTrainerGender;

    public bool HasForms => FormList.Count > 1;

    // Exposed for "Set" operations
    public PKM TargetPKM => _pk;

    public PokemonEditorViewModel(PKM pk, SaveFile sav, ISpriteRenderer spriteRenderer, IDialogService dialogService)
    {
        _pk = pk.Clone(); // Always work on a copy
        _sav = sav;
        _spriteRenderer = spriteRenderer;
        _dialogService = dialogService;
        
        // Initialize filtered data sources
        var filtered = GameInfo.FilteredSources;
        SpeciesList = filtered.Species;
        MoveList = filtered.Moves;
        NatureList = filtered.Natures;
        BallList = filtered.Balls;
        ItemList = filtered.Items;
        OriginGameList = filtered.Games;
        RelearnMoveDataSource = filtered.Relearn;
        LanguageList = GameInfo.Sources.LanguageDataSource(sav.Generation, sav.Context);

        // Load PKM data into view model
        LoadFromPKM();
    }

    public void LoadPKM(PKM pk)
    {
        _pk = pk.Clone();
        LoadFromPKM();
    }

    private void LoadFromPKM()
    {
        _isLoading = true;
        try
        {
            // First set species/form to populate dynamic lists
            Species = _pk.Species;
            Form = _pk.Form;

            // Update dynamic lists BEFORE setting their selected values
            UpdateFormList();
            UpdateAbilityList();

            // Now set the values that depend on those lists
            Ability = _pk.Ability;

            // Basic info
            Nickname = _pk.Nickname;
            IsNicknamed = _pk.IsNicknamed;
            Level = _pk.CurrentLevel;
            Nature = (int)_pk.Nature;
            HeldItem = _pk.HeldItem;
            Ball = _pk.Ball;
            Gender = _pk.Gender;
            IsShiny = _pk.IsShiny;
            IsEgg = _pk.IsEgg;

            // New Group 1: Identity & Metadata
            Id32 = _pk.ID32;
            Version = _pk.Version.ToString();
            Valid = new LegalityAnalysis(_pk).Valid;

            // Moves
            Move1 = _pk.Move1;
            Move2 = _pk.Move2;
            Move3 = _pk.Move3;
            Move4 = _pk.Move4;

            RelearnMove1 = _pk.RelearnMove1;
            RelearnMove2 = _pk.RelearnMove2;
            RelearnMove3 = _pk.RelearnMove3;
            RelearnMove4 = _pk.RelearnMove4;

            // PP values
            Pp1 = _pk.Move1_PP;
            Pp2 = _pk.Move2_PP;
            Pp3 = _pk.Move3_PP;
            Pp4 = _pk.Move4_PP;
            PpUps1 = _pk.Move1_PPUps;
            PpUps2 = _pk.Move2_PPUps;
            PpUps3 = _pk.Move3_PPUps;
            PpUps4 = _pk.Move4_PPUps;

            // Stats
            IvHP = _pk.IV_HP;
            IvATK = _pk.IV_ATK;
            IvDEF = _pk.IV_DEF;
            IvSPA = _pk.IV_SPA;
            IvSPD = _pk.IV_SPD;
            IvSPE = _pk.IV_SPE;

            EvHP = _pk.EV_HP;
            EvATK = _pk.EV_ATK;
            EvDEF = _pk.EV_DEF;
            EvSPA = _pk.EV_SPA;
            EvSPD = _pk.EV_SPD;
            EvSPE = _pk.EV_SPE;

            // Misc
            IsFatefulEncounter = _pk.FatefulEncounter;
            Happiness = _pk.CurrentFriendship;
            Sid = (int)_pk.DisplaySID;

            // PID/EC
            Pid = _pk.PID.ToString("X8");
            EncryptionConstant = _pk.EncryptionConstant.ToString("X8");

            // EXP
            Exp = _pk.EXP;

            // Language
            Language = _pk.Language;

            // Pokerus
            PkrsStrain = _pk.PokerusStrain;
            PkrsDays = _pk.PokerusDays;

            // Met data - set origin game first to populate location lists
            OriginGame = (int)_pk.Version;
            UpdateMetDataLists();

            // Now set locations after lists are populated
            MetLocation = _pk.MetLocation;
            EggLocation = _pk.EggLocation;
            MetLevel = _pk.MetLevel;

            MetDate = _pk.MetDate is { } md ? new DateTimeOffset(md.Year, md.Month, md.Day, 0, 0, 0, TimeSpan.Zero) : null;
            EggDate = _pk.EggMetDate is { } ed ? new DateTimeOffset(ed.Year, ed.Month, ed.Day, 0, 0, 0, TimeSpan.Zero) : null;

            // OT info
            OriginalTrainerName = _pk.OriginalTrainerName;
            TrainerID = _pk.DisplayTID;
            OriginalTrainerGender = _pk.OriginalTrainerGender;

            // Group 2: Health & Status
            StatHPCurrent = _pk.Stat_HPCurrent;
            StatHPMax = _pk.Stat_HPMax;
            StatusCondition = _pk.Status_Condition;

            // Group 3: Trainer & Friendship
            OriginalTrainerFriendship = _pk.OriginalTrainerFriendship;
            HandlingTrainerFriendship = _pk.HandlingTrainerFriendship;
            CurrentHandler = _pk.CurrentHandler;
            HandlingTrainerName = _pk.HandlingTrainerName;
            HandlingTrainerGender = _pk.HandlingTrainerGender;

            // Group 4: Misc
            AbilityNumber = _pk.AbilityNumber;
            StatNature = (int)_pk.StatNature;
            HpType = _pk.HPType;
            IsPokerusInfected = _pk.IsPokerusInfected;
            IsPokerusCured = _pk.IsPokerusCured;

            UpdateTitle();
        }
        finally
        {
            _isLoading = false;
        }

        // These can modify _pk, but now we're done loading so it's OK
        UpdateSprite();
        Validate();
    }

    partial void OnSpeciesChanged(int value)
    {
        if (_isLoading) return;
        UpdateFormList();
        UpdateAbilityList();
        UpdateSprite();
        UpdateTitle();
    }

    partial void OnFormChanged(int value)
    {
        if (_isLoading) return;
        UpdateAbilityList();
        UpdateSprite();
    }

    partial void OnOriginGameChanged(int value)
    {
        if (_isLoading) return;
        UpdateMetDataLists();
    }

    private void UpdateMetDataLists()
    {
        MetLocationList.Clear();
        var context = _sav.Context;
        var locations = GameInfo.Sources.Met.GetLocationList((GameVersion)OriginGame, context);
        foreach (var item in locations)
            MetLocationList.Add(item);

        EggLocationList.Clear();
        var eggLocations = GameInfo.Sources.Met.GetLocationList((GameVersion)OriginGame, context, egg: true);
        foreach (var item in eggLocations)
            EggLocationList.Add(item);
    }

    partial void OnIsShinyChanged(bool value)
    {
        if (_isLoading) return;
        UpdateSprite();
        Validate();
    }

    partial void OnNicknameChanged(string value) { if (!_isLoading) Validate(); }
    partial void OnLevelChanged(int value) { if (!_isLoading) Validate(); }
    partial void OnNatureChanged(int value) { if (!_isLoading) Validate(); }
    partial void OnAbilityChanged(int value) { if (!_isLoading) Validate(); }
    partial void OnHeldItemChanged(int value) { if (!_isLoading) Validate(); }
    partial void OnBallChanged(int value) { if (!_isLoading) Validate(); }
    partial void OnMove1Changed(int value) { if (!_isLoading) Validate(); }
    partial void OnMove2Changed(int value) { if (!_isLoading) Validate(); }
    partial void OnMove3Changed(int value) { if (!_isLoading) Validate(); }
    partial void OnMove4Changed(int value) { if (!_isLoading) Validate(); }
    partial void OnRelearnMove1Changed(int value) { if (!_isLoading) Validate(); }
    partial void OnRelearnMove2Changed(int value) { if (!_isLoading) Validate(); }
    partial void OnRelearnMove3Changed(int value) { if (!_isLoading) Validate(); }
    partial void OnRelearnMove4Changed(int value) { if (!_isLoading) Validate(); }
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
    partial void OnMetLocationChanged(int value) { if (!_isLoading) Validate(); }
    partial void OnMetLevelChanged(int value) { if (!_isLoading) Validate(); }
    partial void OnMetDateChanged(DateTimeOffset? value) { if (!_isLoading) Validate(); }
    partial void OnEggLocationChanged(int value) { if (!_isLoading) Validate(); }
    partial void OnEggDateChanged(DateTimeOffset? value) { if (!_isLoading) Validate(); }
    partial void OnIsFatefulEncounterChanged(bool value) { if (!_isLoading) Validate(); }
    partial void OnIsEggChanged(bool value) { if (!_isLoading) Validate(); }
    partial void OnGenderChanged(int value) { if (!_isLoading) Validate(); }
    partial void OnOriginalTrainerGenderChanged(int value) { if (!_isLoading) Validate(); }
    partial void OnTrainerIDChanged(long value) { if (!_isLoading) Validate(); }

    private void Validate()
    {
        var pk = PreparePKM();
        var la = new LegalityAnalysis(pk, _sav.Personal);
        IsLegal = la.Valid;
        LegalityReport = la.Report();
    }

    private void UpdateAbilityList()
    {
        AbilityList.Clear();
        var pi = _sav.Personal.GetFormEntry((ushort)Species, (byte)Form);
        var filtered = GameInfo.FilteredSources;
        foreach (var item in filtered.GetAbilityList(pi))
        {
            AbilityList.Add(item);
        }
    }

    private void UpdateFormList()
    {
        FormList.Clear();
        var pi = _sav.Personal.GetFormEntry((ushort)Species, 0);
        var formCount = pi.FormCount;

        if (formCount <= 1)
        {
            FormList.Add(new ComboItem("Normal", 0));
        }
        else
        {
            var formNames = FormConverter.GetFormList((ushort)Species, GameInfo.Strings.Types, GameInfo.Strings.forms, [], _sav.Context);
            for (int i = 0; i < formCount && i < formNames.Length; i++)
            {
                var name = string.IsNullOrWhiteSpace(formNames[i]) ? $"Form {i}" : formNames[i];
                FormList.Add(new ComboItem(name, i));
            }
        }

        OnPropertyChanged(nameof(HasForms));
    }

    private void UpdateSprite()
    {
        // Create a temporary PKM with current values to render sprite
        _pk.Species = (ushort)Species;
        _pk.Form = (byte)Form;
        if (IsShiny)
            _pk.SetShiny();

        Sprite = _spriteRenderer.GetSprite(_pk);
    }

    private void UpdateTitle()
    {
        var speciesName = GameInfo.Strings.Species[Species];
        Title = Species == 0 ? "Empty Slot" : $"Editing: {speciesName}";
    }

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

    /// <summary>
    /// Applies current ViewModel state to the internal PKM and returns it.
    /// </summary>
    public PKM PreparePKM()
    {
        // Apply changes to PKM
        _pk.Species = (ushort)Species;
        _pk.Form = (byte)Form;
        _pk.Nickname = Nickname;
        _pk.Stat_Level = (byte)Level;
        _pk.Nature = (Nature)Nature;
        _pk.Ability = Ability;
        _pk.HeldItem = HeldItem;
        _pk.Ball = (byte)Ball;
        _pk.Gender = (byte)Gender;
        _pk.IsEgg = IsEgg;

        if (IsShiny && !_pk.IsShiny)
            _pk.SetShiny();
        else if (!IsShiny && _pk.IsShiny)
            _pk.SetUnshiny();

        _pk.Move1 = (ushort)Move1;
        _pk.Move2 = (ushort)Move2;
        _pk.Move3 = (ushort)Move3;
        _pk.Move4 = (ushort)Move4;

        _pk.RelearnMove1 = (ushort)RelearnMove1;
        _pk.RelearnMove2 = (ushort)RelearnMove2;
        _pk.RelearnMove3 = (ushort)RelearnMove3;
        _pk.RelearnMove4 = (ushort)RelearnMove4;

        _pk.Move1_PP = Pp1;
        _pk.Move2_PP = Pp2;
        _pk.Move3_PP = Pp3;
        _pk.Move4_PP = Pp4;
        _pk.Move1_PPUps = PpUps1;
        _pk.Move2_PPUps = PpUps2;
        _pk.Move3_PPUps = PpUps3;
        _pk.Move4_PPUps = PpUps4;

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

        _pk.OriginalTrainerName = OriginalTrainerName;
        _pk.OriginalTrainerGender = (byte)OriginalTrainerGender;
        _pk.ID32 = (uint)TrainerID;
        _pk.DisplaySID = (uint)Sid;
        _pk.CurrentFriendship = (byte)Happiness;
        _pk.FatefulEncounter = IsFatefulEncounter;

        // PID/EC
        if (uint.TryParse(Pid, System.Globalization.NumberStyles.HexNumber, null, out var pid))
            _pk.PID = pid;
        if (uint.TryParse(EncryptionConstant, System.Globalization.NumberStyles.HexNumber, null, out var ec))
            _pk.EncryptionConstant = ec;

        // EXP
        _pk.EXP = (uint)Exp;

        // Language
        _pk.Language = Language;

        // Pokerus
        _pk.PokerusStrain = PkrsStrain;
        _pk.PokerusDays = PkrsDays;

        _pk.Version = (GameVersion)OriginGame;
        _pk.MetLocation = (ushort)MetLocation;
        _pk.EggLocation = (ushort)EggLocation;
        _pk.MetLevel = (byte)MetLevel;

        _pk.MetDate = MetDate is { } md ? new DateOnly(md.Year, md.Month, md.Day) : null;
        _pk.EggMetDate = EggDate is { } ed ? new DateOnly(ed.Year, ed.Month, ed.Day) : null;

        // Recalculate stats
        _pk.ResetPartyStats();
        
        return _pk;
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

    [RelayCommand]
    private async Task ImportShowdown()
    {
        var text = await _dialogService.GetClipboardTextAsync();
        if (string.IsNullOrWhiteSpace(text)) return;

        if (ShowdownParsing.TryParseAnyLanguage(text, out var set))
        {
            _pk.ApplySetDetails(set);
            LoadFromPKM();
        }
        else
        {
            await _dialogService.ShowErrorAsync("Import Failed", "Could not parse Showdown text.");
        }
    }

    [RelayCommand]
    private async Task ExportShowdown()
    {
        var pk = PreparePKM();
        var set = new ShowdownSet(pk);
        await _dialogService.SetClipboardTextAsync(set.Text);
    }

    [RelayCommand]
    private void SuggestRelearnMoves()
    {
        var pk = PreparePKM();
        var la = new LegalityAnalysis(pk, _sav.Personal);
        Span<ushort> moves = stackalloc ushort[4];
        la.GetSuggestedRelearnMovesFromEncounter(moves);
        
        RelearnMove1 = moves[0];
        RelearnMove2 = moves[1];
        RelearnMove3 = moves[2];
        RelearnMove4 = moves[3];
        
        Validate();
    }

    [RelayCommand]
    private void SuggestCurrentMoves()
    {
        var pk = PreparePKM();
        var la = new LegalityAnalysis(pk, _sav.Personal);
        Span<ushort> moves = stackalloc ushort[4];
        la.GetSuggestedCurrentMoves(moves);
        
        Move1 = moves[0];
        Move2 = moves[1];
        Move3 = moves[2];
        Move4 = moves[3];
        
        Validate();
    }

    [RelayCommand]
    private void ToggleShiny()
    {
        IsShiny = !IsShiny;
    }
}
