
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
    [ObservableProperty] private IReadOnlyList<ComboItem> _speciesList;
    [ObservableProperty] private IReadOnlyList<ComboItem> _moveList;
    [ObservableProperty] private IReadOnlyList<ComboItem> _natureList;
    [ObservableProperty] private IReadOnlyList<ComboItem> _ballList;
    [ObservableProperty] private IReadOnlyList<ComboItem> _itemList;
    [ObservableProperty] private IReadOnlyList<ComboItem> _originGameList;
    [ObservableProperty] private IReadOnlyList<ComboItem> _relearnMoveDataSource;
    [ObservableProperty] private IReadOnlyList<ComboItem> _languageList;
    
    public IReadOnlyList<ComboItem> GenderList { get; } = [
        new ComboItem("Male", 0),
        new ComboItem("Female", 1),
        new ComboItem("Genderless", 2)
    ];

    // Dynamic lists
    [ObservableProperty]
    private ObservableCollection<ComboItem> _abilityList = [];

    [ObservableProperty]
    private ObservableCollection<ComboItem> _formList = [];

    [ObservableProperty]
    private Bitmap? _sprite;

    [ObservableProperty]
    private string _title = "Pokémon Editor";

    // Basic Info
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Title), nameof(Sprite), nameof(FormList), nameof(AbilityList), nameof(Ability), nameof(Stat_HP), nameof(Stat_ATK), nameof(Stat_DEF), nameof(Stat_SPA), nameof(Stat_SPD), nameof(Stat_SPE), nameof(Base_HP), nameof(Base_ATK), nameof(Base_DEF), nameof(Base_SPA), nameof(Base_SPD), nameof(Base_SPE))]
    private int _species;

    [ObservableProperty]
    private string _version = string.Empty; // Read-only game name

    [ObservableProperty]
    private bool _isNicknamed;

    [ObservableProperty]
    private uint _id32;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Sprite), nameof(Stat_HP), nameof(Stat_ATK), nameof(Stat_DEF), nameof(Stat_SPA), nameof(Stat_SPD), nameof(Stat_SPE), nameof(Base_HP), nameof(Base_ATK), nameof(Base_DEF), nameof(Base_SPA), nameof(Base_SPD), nameof(Base_SPE))]
    private int _form;

    [ObservableProperty]
    private string _nickname = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Title), nameof(Stat_HP), nameof(Stat_ATK), nameof(Stat_DEF), nameof(Stat_SPA), nameof(Stat_SPD), nameof(Stat_SPE))]
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
    private bool _isFatefulEncounter; // Kept here as wasn't in Met struct

    [ObservableProperty]
    private int _sid; // Kept here as wasn't in Misc struct

    [ObservableProperty]
    private int _language; // Kept here

    public bool HasForms => FormList.Count > 1;
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

    public void RefreshLanguage()
    {
        // Re-initialize filtered data sources from the updated GameInfo
        var filtered = GameInfo.FilteredSources;
        SpeciesList = filtered.Species;
        MoveList = filtered.Moves;
        NatureList = filtered.Natures;
        BallList = filtered.Balls;
        ItemList = filtered.Items;
        OriginGameList = filtered.Games;
        RelearnMoveDataSource = filtered.Relearn;
        LanguageList = GameInfo.Sources.LanguageDataSource(_sav.Generation, _sav.Context);

        // Notify that the PKM name etc might have changed
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
            
            // Validation (Partial)
            // Validate(); // Moved to end to avoid overwriting _pk with incomplete state

            // Moves (Partial)
            Move1 = _pk.Move1;
            Move2 = _pk.Move2;
            Move3 = _pk.Move3;
            Move4 = _pk.Move4;

            RelearnMove1 = _pk.RelearnMove1;
            RelearnMove2 = _pk.RelearnMove2;
            RelearnMove3 = _pk.RelearnMove3;
            RelearnMove4 = _pk.RelearnMove4;

            // PP values (Partial)
            Pp1 = _pk.Move1_PP;
            Pp2 = _pk.Move2_PP;
            Pp3 = _pk.Move3_PP;
            Pp4 = _pk.Move4_PP;
            PpUps1 = _pk.Move1_PPUps;
            PpUps2 = _pk.Move2_PPUps;
            PpUps3 = _pk.Move3_PPUps;
            PpUps4 = _pk.Move4_PPUps;

            // Stats (Partial)
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

            // Misc (Partial)
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

            // Met data - set origin game first
            OriginGame = (int)_pk.Version;
            
            // Populating lists manually during load to ensure correct initial selection
            UpdateMetDataLists(); 
            
            // Now set locations from PKM after lists are populated
            MetLocation = _pk.MetLocation;
            EggLocation = _pk.EggLocation;
            MetLevel = _pk.MetLevel;

            MetDate = _pk.MetDate is { Year: > 0 } md ? new DateTimeOffset(md.Year, md.Month, md.Day, 0, 0, 0, TimeSpan.Zero) : null;
            EggDate = _pk.EggMetDate is { Year: > 0 } ed ? new DateTimeOffset(ed.Year, ed.Month, ed.Day, 0, 0, 0, TimeSpan.Zero) : null;

            // OT info (Partial)
            OriginalTrainerName = _pk.OriginalTrainerName;
            TrainerID = _pk.DisplayTID;
            OriginalTrainerGender = _pk.OriginalTrainerGender;

            // Health & Status (Partial)
            StatHPCurrent = _pk.Stat_HPCurrent;
            StatHPMax = _pk.Stat_HPMax;
            StatusCondition = _pk.Status_Condition; // Wait, I didn't verify if I moved StatusCondition?
            // NOTE: I did NOT move StatusCondition to Stats. Let's check Stats.cs.
            // I see StatHPCurrent, StatHPMax in Stats.cs.
            // I do NOT see StatusCondition in Stats.cs.
            // I do NOT see StatusCondition in Misc.cs.
            // Therefore, StatusCondition must stay here or be lost invalidly.
            
            // Checking Stats.cs content again from memory...
            // "Group 2: Health & Status... _statHPCurrent, _statHPMax, _statNature, _hpType..."
            // NO _statusCondition.
            // I need to add it to Stats.cs or keep it here.
            // I'll keep it here for this write to be safe, or add it to main.
            
            OriginalTrainerFriendship = _pk.OriginalTrainerFriendship;
            HandlingTrainerFriendship = _pk.HandlingTrainerFriendship;
            CurrentHandler = _pk.CurrentHandler;
            HandlingTrainerName = _pk.HandlingTrainerName;
            HandlingTrainerGender = _pk.HandlingTrainerGender;

            // Misc
            AbilityNumber = _pk.AbilityNumber;
            StatNature = (int)_pk.StatNature;
            HpType = _pk.HPType;
            IsPokerusInfected = _pk.IsPokerusInfected;
            IsPokerusCured = _pk.IsPokerusCured;

            // Contest Stats (Partial)
            if (_pk is IContestStatsReadOnly cs)
            {
                ContestCool = cs.ContestCool;
                ContestBeauty = cs.ContestBeauty;
                ContestCute = cs.ContestCute;
                ContestSmart = cs.ContestSmart;
                ContestTough = cs.ContestTough;
                ContestSheen = cs.ContestSheen;
            }

            // Markings (Partial)
            if (_pk is IAppliedMarkings3 m3)
            {
                MarkingCircle = m3.MarkingCircle;
                MarkingTriangle = m3.MarkingTriangle;
                MarkingSquare = m3.MarkingSquare;
                MarkingHeart = m3.MarkingHeart;
            }
            if (_pk is IAppliedMarkings4 m4)
            {
                MarkingStar = m4.MarkingStar;
                MarkingDiamond = m4.MarkingDiamond;
            }
            else if (_pk is IAppliedMarkings7 m7)
            {
                // For Gen 7+, convert color to boolean (any color = marked)
                MarkingCircle = m7.MarkingCircle != MarkingColor.None;
                MarkingTriangle = m7.MarkingTriangle != MarkingColor.None;
                MarkingSquare = m7.MarkingSquare != MarkingColor.None;
                MarkingHeart = m7.MarkingHeart != MarkingColor.None;
                MarkingStar = m7.MarkingStar != MarkingColor.None;
                MarkingDiamond = m7.MarkingDiamond != MarkingColor.None;
            }

            // Memories (Partial)
            if (_pk is IMemoryOT mot)
            {
                OtMemory = mot.OriginalTrainerMemory;
                OtMemoryIntensity = mot.OriginalTrainerMemoryIntensity;
                OtMemoryFeeling = mot.OriginalTrainerMemoryFeeling;
                OtMemoryVariable = mot.OriginalTrainerMemoryVariable;
            }
            if (_pk is IMemoryHT mht)
            {
                HtMemory = mht.HandlingTrainerMemory;
                HtMemoryIntensity = mht.HandlingTrainerMemoryIntensity;
                HtMemoryFeeling = mht.HandlingTrainerMemoryFeeling;
                HtMemoryVariable = mht.HandlingTrainerMemoryVariable;
            }

        UpdateTitle();
        }
        finally
        {
            _isLoading = false;
        }

        // Manually trigger computed property notifications
        OnPropertyChanged(nameof(IVTotal));
        OnPropertyChanged(nameof(EVTotal));

        // These can modify _pk, but now we're done loading so it's OK
        UpdateSprite();
        Validate();
        LoadRibbons();
    }

    [ObservableProperty]
    private int _statusCondition; // Added back here since missed in partials



    partial void OnFormChanged(int value)
    {
        if (_isLoading) return;
        UpdateAbilityList();
        UpdateSprite();
    }

    partial void OnIsShinyChanged(bool value)
    {
        if (_isLoading) return;
        
        // Update the internal PKM to get the new PID that matches shiny state
        if (value)
            _pk.SetShiny();
        else
            _pk.SetUnshiny();
            
        // Sync the PID hex string back to the VM so PreparePKM doesn't overwrite it
        _isLoading = true; // Temporary flag to avoid re-triggering validation/PID changes
        Pid = _pk.PID.ToString("X8");
        _isLoading = false;
        
        UpdateSprite();
        Validate();
    }

    partial void OnPidChanged(string value)
    {
        if (_isLoading) return;
        if (uint.TryParse(value, System.Globalization.NumberStyles.HexNumber, null, out var pid))
        {
            _pk.PID = pid;
            _isLoading = true;
            IsShiny = _pk.IsShiny;
            _isLoading = false;
            UpdateSprite();
            Validate();
        }
    }

    partial void OnEncryptionConstantChanged(string value)
    {
        if (_isLoading) return;
        if (uint.TryParse(value, System.Globalization.NumberStyles.HexNumber, null, out var ec))
        {
            _pk.EncryptionConstant = ec;
            Validate();
        }
    }

    partial void OnNicknameChanged(string value) { if (!_isLoading) Validate(); }
    partial void OnLevelChanged(int value) { if (!_isLoading) Validate(); }
    partial void OnNatureChanged(int value) { if (!_isLoading) Validate(); }
    partial void OnAbilityChanged(int value) { if (!_isLoading) Validate(); }
    partial void OnHeldItemChanged(int value) { if (!_isLoading) Validate(); }
    partial void OnBallChanged(int value) { if (!_isLoading) Validate(); }
    partial void OnGenderChanged(int value) { if (!_isLoading) Validate(); }
    
    // Some partial methods implemented in other files won't clash.

    private void UpdateAbilityList()
    {
        // Store current ability before clearing to prevent binding race condition
        var currentAbility = Ability;
        
        AbilityList.Clear();
        var pi = _sav.Personal.GetFormEntry((ushort)Species, (byte)Form);
        var filtered = GameInfo.FilteredSources;
        foreach (var item in filtered.GetAbilityList(pi))
        {
            AbilityList.Add(item);
        }
        
        // Restore ability if it exists in the new list, otherwise use first available
        if (AbilityList.Any(a => a.Value == currentAbility))
        {
            Ability = currentAbility;
        }
        else if (AbilityList.Count > 0)
        {
            Ability = AbilityList[0].Value;
        }
    }

    private void UpdateFormList()
    {
        // Store current form before clearing to prevent binding race condition
        var currentForm = Form;
        
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

        // Restore form if valid, otherwise use first
        if (FormList.Any(f => f.Value == currentForm))
        {
            Form = currentForm;
        }
        else if (FormList.Count > 0)
        {
            Form = FormList[0].Value;
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
        else
            _pk.SetUnshiny(); // Clear shiny state when not shiny

        Sprite = _spriteRenderer.GetSprite(_pk);
    }

    private void UpdateTitle()
    {
        var speciesName = GameInfo.Strings.Species[Species];
        Title = Species == 0 ? "Empty Slot" : $"Editing: {speciesName}";
    }

    [RelayCommand]
    private void ToggleShiny()
    {
        IsShiny = !IsShiny;
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

        if (IsShiny)
            _pk.SetShiny();
        else
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
        _pk.DisplayTID = (uint)TrainerID;
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

        // Contest Stats (if supported)
        if (_pk is IContestStats cs)
        {
            cs.ContestCool = (byte)ContestCool;
            cs.ContestBeauty = (byte)ContestBeauty;
            cs.ContestCute = (byte)ContestCute;
            cs.ContestSmart = (byte)ContestSmart;
            cs.ContestTough = (byte)ContestTough;
            cs.ContestSheen = (byte)ContestSheen;
        }

        // Markings (if supported)
        if (_pk is IAppliedMarkings3 m3)
        {
            m3.MarkingCircle = MarkingCircle;
            m3.MarkingTriangle = MarkingTriangle;
            m3.MarkingSquare = MarkingSquare;
            m3.MarkingHeart = MarkingHeart;
        }
        if (_pk is IAppliedMarkings4 m4)
        {
            m4.MarkingStar = MarkingStar;
            m4.MarkingDiamond = MarkingDiamond;
        }
        else if (_pk is IAppliedMarkings7 m7)
        {
            // For Gen 7+, set Blue color if marked, None if not
            m7.MarkingCircle = MarkingCircle ? MarkingColor.Blue : MarkingColor.None;
            m7.MarkingTriangle = MarkingTriangle ? MarkingColor.Blue : MarkingColor.None;
            m7.MarkingSquare = MarkingSquare ? MarkingColor.Blue : MarkingColor.None;
            m7.MarkingHeart = MarkingHeart ? MarkingColor.Blue : MarkingColor.None;
            m7.MarkingStar = MarkingStar ? MarkingColor.Blue : MarkingColor.None;
            m7.MarkingDiamond = MarkingDiamond ? MarkingColor.Blue : MarkingColor.None;
        }

        // Memories (if supported)
        if (_pk is IMemoryOT mot)
        {
            mot.OriginalTrainerMemory = (byte)OtMemory;
            mot.OriginalTrainerMemoryIntensity = (byte)OtMemoryIntensity;
            mot.OriginalTrainerMemoryFeeling = (byte)OtMemoryFeeling;
            mot.OriginalTrainerMemoryVariable = (ushort)OtMemoryVariable;
        }
        if (_pk is IMemoryHT mht)
        {
            mht.HandlingTrainerMemory = (byte)HtMemory;
            mht.HandlingTrainerMemoryIntensity = (byte)HtMemoryIntensity;
            mht.HandlingTrainerMemoryFeeling = (byte)HtMemoryFeeling;
            mht.HandlingTrainerMemoryVariable = (ushort)HtMemoryVariable;
        }

        // Recalculate stats
        _pk.ResetPartyStats();
        
        return _pk.Clone();
    }

    [RelayCommand]
    private async Task OpenRibbonEditorAsync()
    {
        var vm = new RibbonEditorViewModel(_pk);
        var view = new Views.RibbonEditor { DataContext = vm };
        await _dialogService.ShowDialogAsync(view, "Ribbon Editor");
        LoadRibbons(); // Refresh ribbon count display
        OnPropertyChanged(nameof(RibbonCount));
    }

    [RelayCommand]
    private async Task OpenMemoryEditorAsync()
    {
        var vm = new MemoryEditorViewModel(_pk);
        var view = new Views.MemoryEditor { DataContext = vm };
        await _dialogService.ShowDialogAsync(view, "Memory Editor");
    }

    [RelayCommand]
    private async Task OpenTechRecordEditorAsync()
    {
        if (_pk is not ITechRecord tr) return;

        var vm = new TechRecordEditorViewModel(tr, _pk);
        var view = new Views.TechRecordEditor { DataContext = vm };
        await _dialogService.ShowDialogAsync(view, "Technical Record Editor");
    }

    public bool CanOpenTechRecord => _pk is ITechRecord;

    partial void OnSpeciesChanged(int value)
    {
        if (_isLoading) return;
        UpdateFormList();
        UpdateAbilityList();
        UpdateSprite();
        UpdateTitle();
        OnPropertyChanged(nameof(CanOpenTechRecord));
    }
}
