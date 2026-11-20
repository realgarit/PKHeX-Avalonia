using System;
using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PKHeX.Core;
using PKHeX.Presentation.Localization;

namespace PKHeX.Presentation.ViewModels;

public partial class SuperTrainingEditorViewModel : ViewModelBase
{
    private readonly SAV6 _sav;
    private readonly SuperTrainBlock _block;

    public SuperTrainingEditorViewModel(SaveFile sav)
    {
        _sav = (SAV6)sav;
        _block = ((ISaveBlock6Main)_sav).SuperTrain;
        IsSupported = true;

        LoadAll();
    }

    /// <summary>The block's counter tops out at 10 (Core clamps it); the tutorial tracker at 6.</summary>
    public int CounterMaximum => 10;
    public int TutorialIndexMaximum => 6;

    [ObservableProperty]
    private int _counter;

    [ObservableProperty]
    private int _tutorialIndex;

    // Loading must not write back: an out-of-range stored tutorial value is shown clamped but kept as-is.
    private bool _loading;

    partial void OnCounterChanged(int value)
    {
        if (!_loading)
            _block.Counter = (byte)Math.Clamp(value, 0, CounterMaximum);
    }

    partial void OnTutorialIndexChanged(int value)
    {
        if (!_loading)
            _block.TutorialIndex = (uint)Math.Clamp(value, 0, TutorialIndexMaximum);
    }

    // Distribution regimens (6 event stages, separate from the 48 regular ones)
    public ObservableCollection<SuperTrainDistributionViewModel> Distributions { get; } = [];

    private void LoadAll()
    {
        _loading = true;
        try
        {
            Counter = _block.Counter;
            TutorialIndex = (int)Math.Min(_block.TutorialIndex, (uint)TutorialIndexMaximum);
        }
        finally
        {
            _loading = false;
        }
        LoadBags();
        LoadRecords();
        LoadStages();
        LoadDistributions();
    }

    public void LoadDistributions()
    {
        Distributions.Clear();
        for (int i = 0; i < SuperTrainDistributionViewModel.Count; i++)
            Distributions.Add(new SuperTrainDistributionViewModel(i, _block.GetIsDistributionUnlocked(i), _block));
    }

    public bool IsSupported { get; }

    // Bags
    public ObservableCollection<TrainingBagViewModel> Bags { get; } = [];
    public string[] BagNames { get; } = GameInfo.Strings.trainingbags;

    // Stages
    public ObservableCollection<SuperTrainStageViewModel> Stages { get; } = [];

    // Records
    public ObservableCollection<SuperTrainRecordViewModel> Records { get; } = [];
    
    [ObservableProperty]
    private SuperTrainRecordViewModel? _selectedRecord;

    public void LoadBags()
    {
        Bags.Clear();
        for (int i = 0; i < 12; i++)
        {
            Bags.Add(new TrainingBagViewModel(i, _block.GetBag(i), _block));
        }
    }

    public void LoadStages()
    {
        Stages.Clear();
        string[] stages = GameInfo.Strings.trainingstage;
        for (int i = 0; i < 48; i++)
        {
            if (i >= stages.Length) break;
            Stages.Add(new SuperTrainStageViewModel(i, stages[i], _block.GetIsRegimenUnlocked(i), _block));
        }
    }

    public void LoadRecords()
    {
        Records.Clear();
        string[] stages = GameInfo.Strings.trainingstage;
        for (int i = 0; i < 48; i++)
        {
            if (i >= stages.Length) break;
            Records.Add(new SuperTrainRecordViewModel(i, stages[i], _block));
        }
        if (Records.Count > 0)
            SelectedRecord = Records[0];
    }

    [RelayCommand]
    private void UnlockAll()
    {
        _block.UnlockAllStages(true);
        LoadStages();
        LoadDistributions();
    }

    /// <summary>Zeroes the whole Super Training block (stages, records, bags, counters), then reloads every tab.</summary>
    [RelayCommand]
    private void ClearAllData()
    {
        _block.ClearBlock();
        LoadAll();
    }

    [RelayCommand]
    private void Save()
    {
        // Many properties are updated via partial methods in sub-ViewModels
        _sav.State.Edited = true;
    }
}

public partial class TrainingBagViewModel : ViewModelBase
{
    private readonly int _index;
    private readonly SuperTrainBlock _block;

    public TrainingBagViewModel(int index, byte bag, SuperTrainBlock block)
    {
        _index = index;
        _bagId = bag;
        _block = block;
    }

    public string SlotName => (_index + 1).ToString();

    [ObservableProperty]
    private byte _bagId;

    partial void OnBagIdChanged(byte value) => _block.SetBag(_index, value);
}

public partial class SuperTrainStageViewModel : ViewModelBase
{
    private readonly int _index;
    private readonly SuperTrainBlock _block;

    public SuperTrainStageViewModel(int index, string name, bool isUnlocked, SuperTrainBlock block)
    {
        _index = index;
        Name = name;
        _isUnlocked = isUnlocked;
        _block = block;
    }

    public string Name { get; }

    [ObservableProperty]
    private bool _isUnlocked;

    partial void OnIsUnlockedChanged(bool value) => _block.SetIsRegimenUnlocked(_index, value);
}

public partial class SuperTrainRecordViewModel : ViewModelBase
{
    private readonly int _index;
    private readonly SuperTrainBlock _block;

    public SuperTrainRecordViewModel(int index, string name, SuperTrainBlock block)
    {
        _index = index;
        Name = name;
        _block = block;

        _time1 = block.GetTime1(index);
        _time2 = block.GetTime2(index);
        
        var h1 = block.GetHolder1(index);
        _species1 = h1.Species;
        _form1 = h1.Form;
        _gender1 = h1.Gender;

        var h2 = block.GetHolder2(index);
        _species2 = h2.Species;
        _form2 = h2.Form;
        _gender2 = h2.Gender;
    }

    public string Name { get; }

    // Record 1
    [ObservableProperty] private float _time1;
    [ObservableProperty] private ushort _species1;
    [ObservableProperty] private byte _form1;
    [ObservableProperty] private byte _gender1;

    // Record 2
    [ObservableProperty] private float _time2;
    [ObservableProperty] private ushort _species2;
    [ObservableProperty] private byte _form2;
    [ObservableProperty] private byte _gender2;

    partial void OnTime1Changed(float value) => _block.SetTime1(_index, value);
    partial void OnTime2Changed(float value) => _block.SetTime2(_index, value);

    partial void OnSpecies1Changed(ushort value) { var h = _block.GetHolder1(_index); h.Species = value; }
    partial void OnForm1Changed(byte value) { var h = _block.GetHolder1(_index); h.Form = value; }
    partial void OnGender1Changed(byte value) { var h = _block.GetHolder1(_index); h.Gender = value; }

    partial void OnSpecies2Changed(ushort value) { var h = _block.GetHolder2(_index); h.Species = value; }
    partial void OnForm2Changed(byte value) { var h = _block.GetHolder2(_index); h.Form = value; }
    partial void OnGender2Changed(byte value) { var h = _block.GetHolder2(_index); h.Gender = value; }

    [RelayCommand]
    private void ClearRecord1()
    {
        _block.ClearRecord1(_index);
        Time1 = 0;
        Species1 = 0;
        Form1 = 0;
        Gender1 = 0;
    }

    [RelayCommand]
    private void ClearRecord2()
    {
        _block.ClearRecord2(_index);
        Time2 = 0;
        Species2 = 0;
        Form2 = 0;
        Gender2 = 0;
    }
}

public partial class SuperTrainDistributionViewModel : ViewModelBase
{
    /// <summary>Number of distribution regimen flags in the block.</summary>
    public const int Count = 6;

    private readonly int _index;
    private readonly SuperTrainBlock _block;

    public SuperTrainDistributionViewModel(int index, bool isUnlocked, SuperTrainBlock block)
    {
        _index = index;
        _block = block;
        _isUnlocked = isUnlocked;
    }

    public string Name => LocalizedStrings.Instance.Format("SuperTrainingEditor_Distribution", _index + 1);

    [ObservableProperty]
    private bool _isUnlocked;

    partial void OnIsUnlockedChanged(bool value) => _block.SetIsDistributionUnlocked(_index, value);
}
