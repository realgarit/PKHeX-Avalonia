using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PKHeX.Core;

namespace PKHeX.Avalonia.ViewModels;

public partial class MedalEditorViewModel : ViewModelBase
{
    private readonly SaveFile _sav;
    private readonly MedalList5? _medals;

    private static readonly string[] CategoryNames = ["Special", "Adventure", "Battle", "Entertainment", "Challenge"];

    public MedalEditorViewModel(SaveFile sav)
    {
        _sav = sav;

        if (sav is SAV5B2W2 b2w2)
        {
            _medals = b2w2.Medals;
            IsSupported = true;
            LoadData();
        }
    }

    public bool IsSupported { get; }
    public string GameInfo => $"{_sav.Version} - Medals (255)";

    [ObservableProperty]
    private ObservableCollection<MedalCategoryViewModel> _categories = [];

    [ObservableProperty]
    private int _totalObtained;

    private void LoadData()
    {
        if (_medals is null) return;

        Categories.Clear();
        TotalObtained = 0;

        // Category ranges from MedalList5.GetMedalType
        var ranges = new (int start, int end, string name)[]
        {
            (0, 6, "Special"),
            (7, 104, "Adventure"),
            (105, 160, "Battle"),
            (161, 235, "Entertainment"),
            (236, 254, "Challenge")
        };

        foreach (var (start, end, name) in ranges)
        {
            var cat = new MedalCategoryViewModel(name);
            for (int i = start; i <= end; i++)
            {
                var medal = _medals[i];
                var vm = new MedalItemViewModel(i, medal.State, medal.IsObtained);
                cat.Medals.Add(vm);
                if (medal.IsObtained) TotalObtained++;
            }
            Categories.Add(cat);
        }
    }

    [RelayCommand]
    private void ObtainAll()
    {
        _medals?.ObtainAll(DateOnly.FromDateTime(System.DateTime.Now));
        LoadData();
    }

    [RelayCommand]
    private void ClearAll()
    {
        if (_medals is null) return;
        for (int i = 0; i < 255; i++)
            _medals[i].Clear();
        LoadData();
    }

    [RelayCommand]
    private void Refresh() => LoadData();
}

public partial class MedalCategoryViewModel : ViewModelBase
{
    public MedalCategoryViewModel(string name)
    {
        Name = name;
    }

    public string Name { get; }
    public int ObtainedCount => Medals.Count(m => m.IsObtained);
    public string Summary => $"{ObtainedCount}/{Medals.Count}";

    [ObservableProperty]
    private ObservableCollection<MedalItemViewModel> _medals = [];
}

public class MedalItemViewModel
{
    public MedalItemViewModel(int index, Medal5State state, bool isObtained)
    {
        Index = index;
        State = state;
        IsObtained = isObtained;
    }

    public int Index { get; }
    public Medal5State State { get; }
    public bool IsObtained { get; }

    public string StateText => State switch
    {
        Medal5State.Unobtained => "Not Obtained",
        Medal5State.HintReady => "Hint Ready",
        Medal5State.HintObtained => "Hint Obtained",
        Medal5State.ObtainReady => "Ready to Obtain",
        Medal5State.Obtained => "Obtained ✓",
        _ => State.ToString()
    };
}
