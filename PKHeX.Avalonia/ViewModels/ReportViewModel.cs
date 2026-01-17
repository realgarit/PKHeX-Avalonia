using System;
using System.Collections.Generic;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PKHeX.Avalonia.Services;
using PKHeX.Core;

namespace PKHeX.Avalonia.ViewModels;

public partial class ReportViewModel : ViewModelBase
{
    private readonly SaveFile _sav;
    
    public System.Collections.ObjectModel.ObservableCollection<ReportEntryViewModel> Items { get; } = [];

    public ReportViewModel(SaveFile sav)
    {
        _sav = sav;
        GenerateReport();
    }

    private void GenerateReport()
    {
        // Scan all boxes
        for (int i = 0; i < _sav.BoxCount * _sav.BoxSlotCount; i++)
        {
            var pk = _sav.GetBoxSlotAtIndex(i);
            if (pk.Species == 0) continue;
            
            // For a 'Report', usually we want a summary. The WinForms version uses EntitySummaryImage.
            // We can reuse logic or just pull key properties.
            // WinForms ReportGrid adds columns dynamically?
            // "EntitySummaryImage" has properties: Species, Nickname, Gender, Level, etc.
            
            Items.Add(new ReportEntryViewModel(pk));
        }
    }
}

public partial class ReportEntryViewModel : ObservableObject
{
    // Common properties for a report
    public string Species { get; }
    public string Nickname { get; }
    public int Level { get; }
    public string Nature { get; }
    public string Ability { get; }
    public string HeldItem { get; }
    public string Move1 { get; }
    public string Move2 { get; }
    public string Move3 { get; }
    public string Move4 { get; }
    public int IV_HP { get; }
    public int IV_Atk { get; }
    public int IV_Def { get; }
    public int IV_SpA { get; }
    public int IV_SpD { get; }
    public int IV_Spe { get; }
    public int EV_HP { get; }
    public int EV_Atk { get; }
    public int EV_Def { get; }
    public int EV_SpA { get; }
    public int EV_SpD { get; }
    public int EV_Spe { get; }


    public ReportEntryViewModel(PKM pk)
    {
        Species = SpeciesName.GetSpeciesName(pk.Species, 0); // Default lang
        Nickname = pk.Nickname;
        Level = pk.CurrentLevel;
        Nature = ((Nature)pk.Nature).ToString();
        
        var strings = GameInfo.Strings;
        Ability = strings.Ability[pk.Ability];
        HeldItem = strings.Item[pk.HeldItem];
        Move1 = strings.Move[pk.Move1];
        Move2 = strings.Move[pk.Move2];
        Move3 = strings.Move[pk.Move3];
        Move4 = strings.Move[pk.Move4];
        
        IV_HP = pk.IV_HP;
        IV_Atk = pk.IV_ATK;
        IV_Def = pk.IV_DEF;
        IV_SpA = pk.IV_SPA;
        IV_SpD = pk.IV_SPD;
        IV_Spe = pk.IV_SPE;
        
        EV_HP = pk.EV_HP;
        EV_Atk = pk.EV_ATK;
        EV_Def = pk.EV_DEF;
        EV_SpA = pk.EV_SPA;
        EV_SpD = pk.EV_SPD;
        EV_Spe = pk.EV_SPE;
    }
}
