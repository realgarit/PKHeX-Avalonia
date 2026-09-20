using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PKHeX.Core;


namespace PKHeX.Presentation.ViewModels;

public partial class RibbonEditorViewModel : ViewModelBase, ICloseableDialog
{
    private readonly PKM _sourcePkm;
    private readonly PKM _workingPkm;

    public Action? CloseRequested { get; set; }

    [ObservableProperty]
    private ObservableCollection<RibbonItemViewModel> _ribbons = new();

    [ObservableProperty]
    private bool _showAll; // Toggle to show all or just valid

    public RibbonEditorViewModel(PKM pkm, Action? closeHelper = null)
    {
        _sourcePkm = pkm;
        _workingPkm = pkm.Clone();
        CloseRequested = closeHelper;
        LoadRibbons();
    }

    private void LoadRibbons()
    {
        // 1. Get all ribbons
        var allRibbons = RibbonInfo.GetRibbonInfo(_workingPkm);
        
        // 2. Verify validity (logic adapted from WinForms RibbonEditor.PopulateRibbons)
        var la = new LegalityAnalysis(_workingPkm);
        Span<RibbonResult> results = stackalloc RibbonResult[allRibbons.Count];
        var args = new RibbonVerifierArguments(_workingPkm, la.EncounterOriginal, la.Info.EvoChainsAllGens);
        var count = RibbonVerifier.GetRibbonResults(args, results);
        var slice = results[..count];
        
        var dict = new Dictionary<string, RibbonResult>(slice.Length);
        foreach (var r in slice)
            dict.TryAdd(r.PropertyName, r);

        // 3. Create ViewModels
        var list = new List<RibbonItemViewModel>();
        foreach (var info in allRibbons)
        {
            var vm = new RibbonItemViewModel(_workingPkm, info);

            // Compute the ribbon icon resource name; the View resolves it to an image asset.
            // Name mapping: lowercase, remove "CountG3" -> "G3".
            var resourceName = info.Name.Replace("CountG3", "G3").ToLowerInvariant();

            // Handle Gold memory ribbons if max count reached
            if (info.Type == RibbonValueType.Byte)
            {
                int max = info.MaxCount;
                if (max == 8 && info.Name == nameof(IRibbonSetMemory6.RibbonCountMemoryBattle) && _workingPkm.Format >= 9)
                    max = 7;

                if ((info.Name == nameof(IRibbonSetMemory6.RibbonCountMemoryBattle) ||
                     info.Name == nameof(IRibbonSetMemory6.RibbonCountMemoryContest)) &&
                     info.RibbonCount == max)
                {
                     resourceName += "2";
                }
            }

            vm.IconResource = resourceName;

            list.Add(vm);
        }

        Ribbons = new ObservableCollection<RibbonItemViewModel>(list);
    }
    
    [RelayCommand]
    private void Save()
    {
        _workingPkm.Data.CopyTo(_sourcePkm.Data);
        _sourcePkm.RefreshChecksum();
        CloseRequested?.Invoke();
    }
    
    [RelayCommand]
    private void GiveAll()
    {
        RibbonApplicator.SetAllValidRibbons(_workingPkm);
        LoadRibbons(); // Reload to reflect changes
    }
    
    [RelayCommand]
    private void RemoveAll()
    {
        RibbonApplicator.RemoveAllValidRibbons(_workingPkm);
        LoadRibbons();
    }

    [RelayCommand]
    private void Cancel() => CloseRequested?.Invoke();
}
