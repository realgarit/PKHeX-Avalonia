using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PKHeX.Core;


namespace PKHeX.Presentation.ViewModels;

public partial class TechRecordItemViewModel : ObservableObject
{
    /// <summary>The zero-based bit index used by the Core record implementation.</summary>
    public int Index { get; init; }
    /// <summary>The user-facing index. Legends: Z-A displays TM entries one-based.</summary>
    public int DisplayIndex { get; init; }
    public string Name { get; init; } = "";
    public string TypeName { get; init; } = "";
    public int TypeId { get; init; }
    public byte[]? TypeIcon { get; init; }
    
    // Status
    [ObservableProperty] private bool _isActive;
    [ObservableProperty] private bool _isDirectlyPermitted;
    [ObservableProperty] private bool _isEvolutionPermitted;
    [ObservableProperty] private bool _isLearned; // If the Pokemon currently knows this move
}

public partial class TechRecordEditorViewModel : ViewModelBase, ICloseableDialog
{
    private readonly PKM _sourcePkm;
    private readonly PKM _workingPkm;
    private readonly ITechRecord _techRecord;
    private readonly LegalityAnalysis _legality;

    public Action? CloseRequested { get; set; }

    [ObservableProperty]
    private ObservableCollection<TechRecordItemViewModel> _records = new();

    public TechRecordEditorViewModel(ITechRecord techRecord, PKM pkm, Action? closeHelper = null)
    {
        _sourcePkm = pkm;
        _workingPkm = pkm.Clone();
        _techRecord = (ITechRecord)_workingPkm;
        _legality = new LegalityAnalysis(_workingPkm);
        CloseRequested = closeHelper;
        
        LoadRecords();
    }

    private void LoadRecords()
    {
        var permit = _techRecord.Permit;
        var indexes = permit.RecordPermitIndexes;
        var context = _workingPkm.Context;
        var displayOffset = context == EntityContext.Gen9a ? 1 : 0;
        var evos = _legality.Info.EvoChainsAllGens.Get(context);
        
        var moveNames = GameInfo.Strings.Move;
        Span<ushort> currentMoves = stackalloc ushort[4];
        _workingPkm.GetMoves(currentMoves);
        
        var list = new List<TechRecordItemViewModel>();

        for (int i = 0; i < indexes.Length; i++)
        {
            var move = indexes[i];
            var type = MoveInfo.GetType(move, context);
            
            bool isDirectlyPermitted = permit.IsRecordPermitted(i);
            bool isEvolutionPermitted = _techRecord.IsRecordPermitted(evos, i);
            bool isActive = _techRecord.GetMoveRecordFlag(i);
            
            bool isLearned = currentMoves.Contains(move);
            
            // Icon
            // TypeSpriteUtil logic to get icon.
            // Need to convert GDI+ bitmap if using PKHeX.Drawing.Misc
            // Or use SpriteLoader if it supports types. SpriteLoader has GetItemSprite but not Type sprite.
            // I'll assume null icon for now or use a placeholder/text color.
            // Actually, TypeSpriteUtil.GetTypeSpriteIconSmall(type) returns Bitmap.
            
            list.Add(new TechRecordItemViewModel
            {
                Index = i,
                DisplayIndex = i + displayOffset,
                Name = moveNames[move],
                TypeId = type,
                TypeName = ((MoveType)type).ToString(),
                IsDirectlyPermitted = isDirectlyPermitted,
                IsEvolutionPermitted = isEvolutionPermitted,
                IsActive = isActive,
                IsLearned = isLearned,
            });
        }
        
        Records = new ObservableCollection<TechRecordItemViewModel>(list);
    }
    
    [RelayCommand]
    private void Save()
    {
        foreach (var item in Records)
        {
            _techRecord.SetMoveRecordFlag(item.Index, item.IsActive);
        }
        _workingPkm.Data.CopyTo(_sourcePkm.Data);
        _sourcePkm.RefreshChecksum();
        CloseRequested?.Invoke();
    }
    
    [RelayCommand]
    private void GiveAll()
    {
        _techRecord.SetRecordFlags(_workingPkm, TechnicalRecordApplicatorOption.LegalAll);
        Reload();
    }

    [RelayCommand]
    private void GiveCurrent()
    {
        _techRecord.SetRecordFlags(_workingPkm, TechnicalRecordApplicatorOption.LegalCurrent);
        Reload();
    }

    [RelayCommand]
    private void ForceAll()
    {
        _techRecord.SetRecordFlags(_workingPkm, TechnicalRecordApplicatorOption.ForceAll);
        Reload();
    }
    
    [RelayCommand]
    private void RemoveAll()
    {
        _techRecord.ClearRecordFlags();
        Reload();
    }
    
    private void Reload()
    {
        // Re-read values
        foreach (var item in Records)
        {
            item.IsActive = _techRecord.GetMoveRecordFlag(item.Index);
        }
    }
    [RelayCommand]
    private void Close() => CloseRequested?.Invoke();
}
