using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PKHeX.Core;
using PKHeX.Drawing.Misc;

namespace PKHeX.Avalonia.ViewModels;

public partial class RibbonEditorViewModel : ViewModelBase
{
    private readonly PKM _pkm;
    private readonly Action? _closeRequested;

    [ObservableProperty]
    private ObservableCollection<RibbonItemViewModel> _ribbons = new();

    [ObservableProperty]
    private bool _showAll; // Toggle to show all or just valid

    public RibbonEditorViewModel(PKM pkm, Action? closeHelper = null)
    {
        _pkm = pkm;
        _closeRequested = closeHelper;
        LoadRibbons();
    }

    private void LoadRibbons()
    {
        // 1. Get all ribbons
        var allRibbons = RibbonInfo.GetRibbonInfo(_pkm);
        
        // 2. Verify validity (logic adapted from WinForms RibbonEditor.PopulateRibbons)
        var la = new LegalityAnalysis(_pkm);
        Span<RibbonResult> results = stackalloc RibbonResult[allRibbons.Count];
        var args = new RibbonVerifierArguments(_pkm, la.EncounterOriginal, la.Info.EvoChainsAllGens);
        var count = RibbonVerifier.GetRibbonResults(args, results);
        var slice = results[..count];
        
        var dict = new Dictionary<string, RibbonResult>(slice.Length);
        foreach (var r in slice)
            dict.TryAdd(r.PropertyName, r);

        // 3. Create ViewModels
        var list = new List<RibbonItemViewModel>();
        foreach (var info in allRibbons)
        {
            var vm = new RibbonItemViewModel(_pkm, info);
            
            // Load Icon (Convert GDI+ Bitmap to Avalonia Bitmap)
            // Note: RibbonSpriteUtil.GetRibbonSprite returns System.Drawing.Bitmap
            // MaxCount logic for memory ribbons from WinForms
            int max = info.MaxCount;
            if (max == 8 && info.Name == nameof(IRibbonSetMemory6.RibbonCountMemoryBattle) && _pkm.Format >= 9)
                max = 7;
                
            var sysBmp = RibbonSpriteUtil.GetRibbonSprite(info.Name, max, info.RibbonCount);
            vm.Icon = ConvertBitmap(sysBmp);

            // Determine if valid (simple check for now, can extend VM if we need coloring)
            // WinForms colors based on 'IsMissing' or 'IsInvalid'.
            // For now we just load them.
            
            list.Add(vm);
        }

        Ribbons = new ObservableCollection<RibbonItemViewModel>(list);
    }
    
    private static Bitmap? ConvertBitmap(System.Drawing.Bitmap? bmp)
    {
        if (bmp == null) return null;
        using var ms = new MemoryStream();
        bmp.Save(ms, System.Drawing.Imaging.ImageFormat.Png);
        ms.Position = 0;
        return new Bitmap(ms);
    }

    [RelayCommand]
    private void Save()
    {
        // Changes are already applied to _pkm via RibbonItemViewModel bindings.
        _closeRequested?.Invoke();
    }
    
    [RelayCommand]
    private void GiveAll()
    {
        RibbonApplicator.SetAllValidRibbons(_pkm);
        LoadRibbons(); // Reload to reflect changes
    }
    
    [RelayCommand]
    private void RemoveAll()
    {
        RibbonApplicator.RemoveAllValidRibbons(_pkm);
        LoadRibbons();
    }
}
