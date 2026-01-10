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

    // Data sources from GameInfo
    public IReadOnlyList<ComboItem> SpeciesList { get; }
    public IReadOnlyList<ComboItem> MoveList { get; }
    public IReadOnlyList<ComboItem> NatureList { get; }
    public IReadOnlyList<ComboItem> AbilityList { get; }
    public IReadOnlyList<ComboItem> BallList { get; }
    public IReadOnlyList<ComboItem> ItemList { get; }

    [ObservableProperty]
    private Bitmap? _sprite;

    [ObservableProperty]
    private string _title = "Pokémon Editor";

    // Basic Info
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Sprite))]
    private ushort _species;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Sprite))]
    private byte _form;

    [ObservableProperty]
    private string _nickname = string.Empty;

    [ObservableProperty]
    private byte _level;

    [ObservableProperty]
    private int _nature;

    [ObservableProperty]
    private int _ability;

    [ObservableProperty]
    private int _heldItem;

    [ObservableProperty]
    private byte _ball;

    [ObservableProperty]
    private byte _gender;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Sprite))]
    private bool _isShiny;

    [ObservableProperty]
    private bool _isEgg;

    // Moves
    [ObservableProperty]
    private ushort _move1;

    [ObservableProperty]
    private ushort _move2;

    [ObservableProperty]
    private ushort _move3;

    [ObservableProperty]
    private ushort _move4;

    // IVs
    [ObservableProperty]
    private int _ivHP;

    [ObservableProperty]
    private int _ivATK;

    [ObservableProperty]
    private int _ivDEF;

    [ObservableProperty]
    private int _ivSPA;

    [ObservableProperty]
    private int _ivSPD;

    [ObservableProperty]
    private int _ivSPE;

    // EVs
    [ObservableProperty]
    private int _evHP;

    [ObservableProperty]
    private int _evATK;

    [ObservableProperty]
    private int _evDEF;

    [ObservableProperty]
    private int _evSPA;

    [ObservableProperty]
    private int _evSPD;

    [ObservableProperty]
    private int _evSPE;

    // Computed Stats
    public int Stat_HP => _pk.Stat_HPMax;
    public int Stat_ATK => _pk.Stat_ATK;
    public int Stat_DEF => _pk.Stat_DEF;
    public int Stat_SPA => _pk.Stat_SPA;
    public int Stat_SPD => _pk.Stat_SPD;
    public int Stat_SPE => _pk.Stat_SPE;

    public int IVTotal => IvHP + IvATK + IvDEF + IvSPA + IvSPD + IvSPE;
    public int EVTotal => EvHP + EvATK + EvDEF + EvSPA + EvSPD + EvSPE;

    // OT Info
    [ObservableProperty]
    private string _originalTrainerName = string.Empty;

    [ObservableProperty]
    private uint _trainerID;

    [ObservableProperty]
    private byte _originalTrainerGender;

    // Form options for current species
    [ObservableProperty]
    private ObservableCollection<ComboItem> _formList = [];

    public bool HasForms => FormList.Count > 1;

    // Exposed for "Set" operations
    public PKM TargetPKM => _pk;

    public PokemonEditorViewModel(PKM pk, SaveFile sav, ISpriteRenderer spriteRenderer)
    {
        _pk = pk.Clone(); // Always work on a copy
        _sav = sav;
        _spriteRenderer = spriteRenderer;
        
        // Initialize data sources
        var sources = GameInfo.Sources;
        SpeciesList = sources.SpeciesDataSource;
        MoveList = sources.LegalMoveDataSource;
        NatureList = sources.NatureDataSource;
        AbilityList = sources.AbilityDataSource;
        BallList = sources.BallDataSource;
        ItemList = sources.GetItemDataSource(_sav.Version, _sav.Context, [], HaX: true);

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
        Species = _pk.Species;
        Form = _pk.Form;
        Nickname = _pk.Nickname;
        Level = _pk.Stat_Level;
        Nature = (int)_pk.Nature;
        Ability = _pk.Ability;
        HeldItem = _pk.HeldItem;
        Ball = _pk.Ball;
        Gender = _pk.Gender;
        IsShiny = _pk.IsShiny;
        IsEgg = _pk.IsEgg;

        Move1 = _pk.Move1;
        Move2 = _pk.Move2;
        Move3 = _pk.Move3;
        Move4 = _pk.Move4;

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

        OriginalTrainerName = _pk.OriginalTrainerName;
        TrainerID = _pk.ID32;
        OriginalTrainerGender = _pk.OriginalTrainerGender;

        UpdateFormList();
        UpdateSprite();
        UpdateTitle();
    }

    partial void OnSpeciesChanged(ushort value)
    {
        UpdateFormList();
        UpdateSprite();
        UpdateTitle();
    }

    partial void OnFormChanged(byte value)
    {
        UpdateSprite();
    }

    partial void OnIsShinyChanged(bool value)
    {
        UpdateSprite();
    }

    private void UpdateFormList()
    {
        FormList.Clear();
        var pi = PersonalTable.USUM.GetFormEntry(Species, 0);
        var formCount = pi.FormCount;

        if (formCount <= 1)
        {
            FormList.Add(new ComboItem("Normal", 0));
        }
        else
        {
            var formNames = FormConverter.GetFormList(Species, GameInfo.Strings.Types, GameInfo.Strings.forms, [], _sav.Context);
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
        _pk.Species = Species;
        _pk.Form = Form;
        if (IsShiny)
            _pk.SetShiny();

        Sprite = _spriteRenderer.GetSprite(_pk);
    }

    private void UpdateTitle()
    {
        var speciesName = SpeciesName.GetSpeciesName(Species, 2); // English
        Title = Species == 0 ? "Empty Slot" : $"Editing: {speciesName}";
    }

    /// <summary>
    /// Applies current ViewModel state to the internal PKM and returns it.
    /// </summary>
    public PKM PreparePKM()
    {
        // Apply changes to PKM
        _pk.Species = Species;
        _pk.Form = Form;
        _pk.Nickname = Nickname;
        _pk.Stat_Level = Level;
        _pk.Nature = (Nature)Nature;
        _pk.Ability = Ability;
        _pk.HeldItem = HeldItem;
        _pk.Ball = Ball;
        _pk.Gender = Gender;
        _pk.IsEgg = IsEgg;

        if (IsShiny && !_pk.IsShiny)
            _pk.SetShiny();
        else if (!IsShiny && _pk.IsShiny)
            _pk.SetUnshiny();

        _pk.Move1 = Move1;
        _pk.Move2 = Move2;
        _pk.Move3 = Move3;
        _pk.Move4 = Move4;

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
        _pk.OriginalTrainerGender = OriginalTrainerGender;

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
    private void ToggleShiny()
    {
        IsShiny = !IsShiny;
    }
}
