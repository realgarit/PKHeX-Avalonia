using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PKHeX.Core;

namespace PKHeX.Presentation.ViewModels;

public partial class PokebeanEditorViewModel : ViewModelBase, ICloseableDialog
{
    private readonly SaveFile _sav;
    private readonly ResortSave7? _resortSave;

    public PokebeanEditorViewModel(SaveFile sav)
    {
        _sav = sav;

        if (sav is SAV7 sav7)
        {
            _resortSave = sav7.ResortSave;
            IsSupported = true;
            LoadBeans();
        }
    }

    public bool IsSupported { get; }
    public System.Action? CloseRequested { get; set; }

    [ObservableProperty]
    private ObservableCollection<BeanSlotViewModel> _beans = [];

    private void LoadBeans()
    {
        Beans.Clear();
        if (_resortSave is null) return;

        var names = ResortSave7.GetBeanIndexNames();
        var beanValues = _resortSave.GetBeans();

        for (int i = 0; i < beanValues.Length; i++)
        {
            Beans.Add(new BeanSlotViewModel(i, names[i], beanValues[i]));
        }
    }

    [RelayCommand]
    private void Save()
    {
        if (_resortSave is null) return;
        var beans = _resortSave.GetBeans();
        foreach (var bean in Beans)
            beans[bean.Index] = bean.Count;
        _sav.State.Edited = true;
        CloseRequested?.Invoke();
    }

    [RelayCommand]
    private void Cancel()
    {
        LoadBeans();
        CloseRequested?.Invoke();
    }

    [RelayCommand]
    private void FillAll()
    {
        foreach (var bean in Beans)
            bean.Count = byte.MaxValue;
    }

    [RelayCommand]
    private void ClearAll()
    {
        foreach (var bean in Beans)
            bean.Count = 0;
    }

    [RelayCommand]
    private void Refresh()
    {
        LoadBeans();
    }
}

public partial class BeanSlotViewModel : ViewModelBase
{
    public BeanSlotViewModel(int index, string name, byte count)
    {
        Index = index;
        Name = name;
        _count = count;
    }

    public int Index { get; }
    public string Name { get; }

    [ObservableProperty]
    private byte _count;

}
