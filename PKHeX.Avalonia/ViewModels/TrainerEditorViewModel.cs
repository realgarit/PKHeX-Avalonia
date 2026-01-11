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
