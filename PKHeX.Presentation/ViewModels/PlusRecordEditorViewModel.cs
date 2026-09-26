using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PKHeX.Core;

namespace PKHeX.Presentation.ViewModels;

public partial class PlusRecordEditorViewModel : ViewModelBase, ICloseableDialog
{
    private readonly PKM _source;
    private readonly PKM _working;
    private readonly IPlusRecord _record;
    private readonly IPermitPlus _permit;
    public Action? CloseRequested { get; set; }
    public IReadOnlyList<PlusRecordItemViewModel> Records { get; }

    public PlusRecordEditorViewModel(PKM entity)
    {
        _source = entity;
        _working = entity.Clone();
        _record = (IPlusRecord)_working;
        _permit = (IPermitPlus)_working.PersonalInfo;
        var indexes = _permit.PlusMoveIndexes;
        var records = new List<PlusRecordItemViewModel>();
        for (int i = 0; i < _permit.PlusCountUsed; i++)
            records.Add(new PlusRecordItemViewModel(i, GameInfo.Strings.Move[indexes[i]], _record.GetMovePlusFlag(i)));
        Records = records;
    }

    [RelayCommand]
    private void SetLegal()
    {
        _record.SetPlusFlags(_working, _permit, PlusRecordApplicatorOption.LegalCurrent);
        Reload();
    }

    [RelayCommand]
    private void Clear()
    {
        _record.ClearPlusFlags(_permit.PlusCountTotal);
        Reload();
    }

    private void Reload()
    {
        foreach (var item in Records) item.IsActive = _record.GetMovePlusFlag(item.Index);
    }

    [RelayCommand]
    private void Save()
    {
        foreach (var item in Records) _record.SetMovePlusFlag(item.Index, item.IsActive);
        // Commit only Plus flags, leaving independent TM records and other metadata intact.
        var target = (IPlusRecord)_source;
        for (int i = 0; i < _permit.PlusCountTotal; i++)
            target.SetMovePlusFlag(i, _record.GetMovePlusFlag(i));
        _source.RefreshChecksum();
        CloseRequested?.Invoke();
    }

    [RelayCommand]
    private void Cancel() => CloseRequested?.Invoke();
}

public partial class PlusRecordItemViewModel(int index, string name, bool active) : ViewModelBase
{
    public int Index { get; } = index;
    public string Name { get; } = name;
    [ObservableProperty] private bool _isActive = active;
}
