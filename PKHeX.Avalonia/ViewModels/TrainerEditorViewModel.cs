using System.Collections.ObjectModel;
using System.Reflection;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PKHeX.Core;


namespace PKHeX.Avalonia.ViewModels;

public partial class TrainerEditorViewModel : ViewModelBase
{
    private readonly SaveFile _sav;

    public TrainerEditorViewModel(SaveFile sav)
    {
        _sav = sav;
        LoadFromSave();
    }

    // Basic Info
    [ObservableProperty]
    private string _trainerName = string.Empty;

    [ObservableProperty]
    private int _gender;

    [ObservableProperty]
    private uint _money;

    [ObservableProperty]
    private ushort _tid16;

    [ObservableProperty]
    private ushort _sid16;

    [ObservableProperty]
    private int _language;

    // Play Time
    [ObservableProperty]
    private int _playedHours;

    [ObservableProperty]
    private int _playedMinutes;

    [ObservableProperty]
    private int _playedSeconds;

    // Game Info (read-only)
    public string GameVersion => _sav.Version.ToString();
    public int Generation => _sav.Generation;
    public string SaveType => _sav.GetType().Name;

    // Data sources
    public IReadOnlyList<ComboItem> LanguageList { get; private set; } = [];
    public IReadOnlyList<ComboItem> GenderList { get; } = [
        new ComboItem("Male", 0),
        new ComboItem("Female", 1)
    ];

    // Max values for validation
    public int MaxMoney => _sav.MaxMoney;

    // Badges
    [ObservableProperty]
    private bool _hasBadges;

    public ObservableCollection<BadgeItemViewModel> Badges { get; } = [];

    private PropertyInfo? _badgesProperty;
    
    // Adventure Info
    [ObservableProperty]
    private bool _hasAdventureInfo;

    [ObservableProperty]
    private long _secondsToFame;

    [ObservableProperty]
    private long _secondsToStart;

    private PropertyInfo? _secondsToFameProperty;
    private PropertyInfo? _secondsToStartProperty;

    // Map Coordinates
    [ObservableProperty]
    private bool _hasCoordinates;

    [ObservableProperty]
    private double _x;

    [ObservableProperty]
    private double _y;

    [ObservableProperty]
    private double _z;

    private PropertyInfo? _xProperty;
    private PropertyInfo? _yProperty;
    private PropertyInfo? _zProperty;

    private void LoadFromSave()
    {
        TrainerName = _sav.OT;
        Gender = _sav.Gender;
        Money = _sav.Money;
        Tid16 = _sav.TID16;
        Sid16 = _sav.SID16;
        Language = _sav.Language;

        PlayedHours = _sav.PlayedHours;
        PlayedMinutes = _sav.PlayedMinutes;
        PlayedSeconds = _sav.PlayedSeconds;

        // Initialize language list based on generation
        LanguageList = GameInfo.Sources.LanguageDataSource(_sav.Generation, _sav.Context);

        LoadBadges();
        LoadAdventureInfo();
        LoadCoordinates();
    }

    private void LoadCoordinates()
    {
        var type = _sav.GetType();
        _xProperty = type.GetProperty("X");
        _yProperty = type.GetProperty("Y");
        _zProperty = type.GetProperty("Z");

        HasCoordinates = _xProperty != null && _yProperty != null && _zProperty != null;

        if (HasCoordinates)
        {
            X = Convert.ToDouble(_xProperty!.GetValue(_sav));
            Y = Convert.ToDouble(_yProperty!.GetValue(_sav));
            Z = Convert.ToDouble(_zProperty!.GetValue(_sav));
        }
    }

    private void LoadAdventureInfo()
    {
        var type = _sav.GetType();
        _secondsToFameProperty = type.GetProperty("SecondsToFame");
        _secondsToStartProperty = type.GetProperty("SecondsToStart");

        HasAdventureInfo = _secondsToFameProperty != null || _secondsToStartProperty != null;

        if (_secondsToFameProperty != null)
        {
            var val = _secondsToFameProperty.GetValue(_sav);
            SecondsToFame = Convert.ToInt64(val);
        }

        if (_secondsToStartProperty != null)
        {
            var val = _secondsToStartProperty.GetValue(_sav);
            SecondsToStart = Convert.ToInt64(val);
        }
    }

    private void LoadBadges()
    {
        Badges.Clear();
        _badgesProperty = _sav.GetType().GetProperty("Badges");
        
        if (_badgesProperty != null && _badgesProperty.PropertyType == typeof(int))
        {
            HasBadges = true;
            int badgeFlags = (int)_badgesProperty.GetValue(_sav)!;
            
            // Create 16 badge wrappers (standard max, usually 8 used)
            for (int i = 0; i < 16; i++)
            {
                bool isSet = (badgeFlags & (1 << i)) != 0;
                var badge = new BadgeItemViewModel(i + 1, isSet);
                // Listen for changes to update the flag logic later if needed, 
                // but simpler to just rebuild the int on Save.
                Badges.Add(badge);
            }
        }
        else
        {
            HasBadges = false;
        }
    }

    [RelayCommand]
    private void Save()
    {
        _sav.OT = TrainerName;
        _sav.Gender = (byte)Gender;
        _sav.Money = Money;
        _sav.TID16 = Tid16;
        _sav.SID16 = Sid16;
        _sav.Language = Language;

        _sav.PlayedHours = PlayedHours;
        _sav.PlayedMinutes = PlayedMinutes;
        _sav.PlayedSeconds = PlayedSeconds;

        SaveBadges();
        SaveAdventureInfo();
        SaveCoordinates();
    }

    private void SaveCoordinates()
    {
        if (!HasCoordinates) return;

        // X
        object valX = Convert.ChangeType(X, _xProperty!.PropertyType);
        _xProperty.SetValue(_sav, valX);

        // Y
        object valY = Convert.ChangeType(Y, _yProperty!.PropertyType);
        _yProperty.SetValue(_sav, valY);

        // Z
        object valZ = Convert.ChangeType(Z, _zProperty!.PropertyType);
        _zProperty.SetValue(_sav, valZ);
    }

    private void SaveAdventureInfo()
    {
        if (!HasAdventureInfo) return;

        if (_secondsToFameProperty != null)
        {
             // Handle type conversion back to the property's type (could be uint or ulong or int)
             // Core usually uses uint or long. Safe to Convert.ChangeType
             object val = Convert.ChangeType(SecondsToFame, _secondsToFameProperty.PropertyType);
             _secondsToFameProperty.SetValue(_sav, val);
        }

        if (_secondsToStartProperty != null)
        {
             object val = Convert.ChangeType(SecondsToStart, _secondsToStartProperty.PropertyType);
             _secondsToStartProperty.SetValue(_sav, val);
        }
    }

    private void SaveBadges()
    {
        if (!HasBadges || _badgesProperty == null) return;

        int badgeFlags = 0;
        for (int i = 0; i < Badges.Count; i++)
        {
            if (Badges[i].IsObtained)
            {
                badgeFlags |= (1 << i);
            }
        }
        _badgesProperty.SetValue(_sav, badgeFlags);
    }

    [RelayCommand]
    private void MaxMoney_Click()
    {
        Money = (uint)MaxMoney;
    }

    [RelayCommand]
    private void Reset()
    {
        LoadFromSave();
    }
}

public partial class BadgeItemViewModel : ObservableObject
{
    public int Index { get; }
    
    [ObservableProperty]
    private bool _isObtained;

    public string DisplayName => $"Badge {Index}";

    public BadgeItemViewModel(int index, bool isObtained)
    {
        Index = index;
        IsObtained = isObtained;
    }
}
