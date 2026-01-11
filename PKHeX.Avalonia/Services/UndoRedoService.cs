using System;
using CommunityToolkit.Mvvm.ComponentModel;
using PKHeX.Core;

namespace PKHeX.Avalonia.Services;

/// <summary>
/// Wraps SlotChangelog from PKHeX.Core with observable properties for UI binding.
/// </summary>
public partial class UndoRedoService : ObservableObject
{
    private SlotChangelog? _changelog;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanUndo))]
    [NotifyPropertyChangedFor(nameof(CanRedo))]
    private int _changeCount;

    public bool CanUndo => _changelog?.CanUndo ?? false;
    public bool CanRedo => _changelog?.CanRedo ?? false;

    public event Action<ISlotInfo>? UndoPerformed;
    public event Action<ISlotInfo>? RedoPerformed;

    public void Initialize(SaveFile sav)
    {
        _changelog = new SlotChangelog(sav);
        ChangeCount = 0;
        OnPropertyChanged(nameof(CanUndo));
        OnPropertyChanged(nameof(CanRedo));
    }

    public void Clear()
    {
        _changelog = null;
        ChangeCount = 0;
    }

    public void AddChange(ISlotInfo info)
    {
        _changelog?.AddNewChange(info);
        ChangeCount++;
    }

    public void Undo()
    {
        if (_changelog is null || !_changelog.CanUndo) return;
        
        var info = _changelog.Undo();
        ChangeCount++;
        UndoPerformed?.Invoke(info);
    }

    public void Redo()
    {
        if (_changelog is null || !_changelog.CanRedo) return;
        
        var info = _changelog.Redo();
        ChangeCount++;
        RedoPerformed?.Invoke(info);
    }
}
