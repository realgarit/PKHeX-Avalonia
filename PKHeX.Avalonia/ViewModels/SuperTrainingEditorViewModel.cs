using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PKHeX.Core;

namespace PKHeX.Avalonia.ViewModels;

public partial class SuperTrainingEditorViewModel : ViewModelBase
{
    private readonly SaveFile _sav;
    private readonly SuperTrainBlock? _stb;

    public SuperTrainingEditorViewModel(SaveFile sav)
    {
        _sav = sav;

        if (sav is SAV6 sav6 && sav6 is ISaveBlock6Main main)
        {
            _stb = main.SuperTrain;
            IsSupported = true;
            LoadData();
        }
    }

    public bool IsSupported { get; }

    [ObservableProperty]
    private ObservableCollection<TrainingBagViewModel> _bags = [];

    [ObservableProperty]
    private ObservableCollection<TrainingStageViewModel> _stages = [];

    [ObservableProperty]
    private TrainingStageViewModel? _selectedStage;

    private void LoadData()
    {
        if (_stb is null) return;

        // Load training bags
        Bags.Clear();
        var bagNames = GameInfo.Strings.trainingbags;
        for (int i = 0; i < 12; i++)
        {
            var bagIndex = _stb.GetBag(i);
            Bags.Add(new TrainingBagViewModel(i, bagIndex, bagNames, SetBag));
        }

        // Load training stages
        Stages.Clear();
        var stageNames = GameInfo.Strings.trainingstage;
        for (int i = 0; i < 32; i++)
        {
            var holder1 = _stb.GetHolder1(i);
            var holder2 = _stb.GetHolder2(i);
            var time1 = _stb.GetTime1(i);
            var time2 = _stb.GetTime2(i);

            Stages.Add(new TrainingStageViewModel(
                i,
                i < stageNames.Length ? stageNames[i] : $"Stage {i + 1}",
                holder1, holder2,
                time1, time2,
                (idx, h1, h2, t1, t2) => SetStageRecord(idx, h1, h2, t1, t2)
            ));
        }

        if (Stages.Count > 0)
            SelectedStage = Stages[0];
    }

    private void SetBag(int index, byte value)
    {
        _stb?.SetBag(index, value);
    }

    private void SetStageRecord(int index, SuperTrainingSpeciesRecord h1, SuperTrainingSpeciesRecord h2, float t1, float t2)
    {
        if (_stb is null) return;
        _stb.SetTime1(index, t1);
        _stb.SetTime2(index, t2);
    }

    [RelayCommand]
    private void Refresh()
    {
        LoadData();
    }
}

public partial class TrainingBagViewModel : ViewModelBase
{
    private readonly System.Action<int, byte> _onChanged;
    private readonly string[] _bagNames;

    public TrainingBagViewModel(int index, byte bagType, string[] bagNames, System.Action<int, byte> onChanged)
    {
        Index = index;
        _bagType = bagType;
        _bagNames = bagNames;
        _onChanged = onChanged;
    }

    public int Index { get; }
    public string SlotLabel => $"Slot {Index + 1}";

    [ObservableProperty]
    private byte _bagType;

    partial void OnBagTypeChanged(byte value)
    {
        _onChanged(Index, value);
        OnPropertyChanged(nameof(BagName));
    }

    public string BagName => BagType < _bagNames.Length && !string.IsNullOrEmpty(_bagNames[BagType])
        ? _bagNames[BagType]
        : "---";
}

public partial class TrainingStageViewModel : ViewModelBase
{
    private readonly System.Action<int, SuperTrainingSpeciesRecord, SuperTrainingSpeciesRecord, float, float> _onChanged;
    private readonly SuperTrainingSpeciesRecord _holder1;
    private readonly SuperTrainingSpeciesRecord _holder2;

    public TrainingStageViewModel(
        int index,
        string name,
        SuperTrainingSpeciesRecord holder1,
        SuperTrainingSpeciesRecord holder2,
        float time1,
        float time2,
        System.Action<int, SuperTrainingSpeciesRecord, SuperTrainingSpeciesRecord, float, float> onChanged)
    {
        Index = index;
        Name = name;
        _holder1 = holder1;
        _holder2 = holder2;
        _time1 = time1;
        _time2 = time2;
        _onChanged = onChanged;

        _species1 = holder1.Species;
        _species2 = holder2.Species;
    }

    public int Index { get; }
    public string Name { get; }
    public string DisplayName => $"{Index + 1:00} - {Name}";

    [ObservableProperty]
    private ushort _species1;

    partial void OnSpecies1Changed(ushort value)
    {
        _holder1.Species = value;
        OnPropertyChanged(nameof(Species1Name));
        NotifyChanged();
    }

    [ObservableProperty]
    private ushort _species2;

    partial void OnSpecies2Changed(ushort value)
    {
        _holder2.Species = value;
        OnPropertyChanged(nameof(Species2Name));
        NotifyChanged();
    }

    [ObservableProperty]
    private float _time1;

    partial void OnTime1Changed(float value) => NotifyChanged();

    [ObservableProperty]
    private float _time2;

    partial void OnTime2Changed(float value) => NotifyChanged();

    public string Species1Name
    {
        get
        {
            if (Species1 == 0) return "(None)";
            var names = GameInfo.Strings.Species;
            return Species1 < names.Count ? names[Species1] : $"Species #{Species1}";
        }
    }

    public string Species2Name
    {
        get
        {
            if (Species2 == 0) return "(None)";
            var names = GameInfo.Strings.Species;
            return Species2 < names.Count ? names[Species2] : $"Species #{Species2}";
        }
    }

    private void NotifyChanged()
    {
        _onChanged(Index, _holder1, _holder2, Time1, Time2);
    }
}
