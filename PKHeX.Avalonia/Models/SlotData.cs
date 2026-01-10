using Avalonia.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;

namespace PKHeX.Avalonia.Models;

/// <summary>
/// Data for a single box slot, displayed in the BoxViewer grid.
/// </summary>
public partial class SlotData : ObservableObject
{
    [ObservableProperty] private int _slot;
    [ObservableProperty] private int _box;
    [ObservableProperty] private ushort _species;
    [ObservableProperty] private Bitmap? _sprite;
    [ObservableProperty] private bool _isEmpty;
    [ObservableProperty] private bool _isShiny;
    [ObservableProperty] private bool _isSelected;
    [ObservableProperty] private string _nickname = string.Empty;
    [ObservableProperty] private string _speciesName = string.Empty;
    [ObservableProperty] private byte _level;
    [ObservableProperty] private byte _gender; // 0=Male, 1=Female, 2=Genderless
    [ObservableProperty] private ushort _heldItem;
    [ObservableProperty] private string _heldItemName = string.Empty;
    [ObservableProperty] private bool _isEgg;
    [ObservableProperty] private byte _form;
    [ObservableProperty] private ushort _ability;
    [ObservableProperty] private string _abilityName = string.Empty;
    [ObservableProperty] private byte _nature;
    [ObservableProperty] private string _natureName = string.Empty;
    
    /// <summary>
    /// Short summary for tooltip.
    /// </summary>
    public string ToolTipSummary => IsEmpty 
        ? "Empty" 
        : $"{Nickname} ({SpeciesName})\nLv. {Level}\n{GenderSymbol}";
    
    public string GenderSymbol => Gender switch
    {
        0 => "♂",
        1 => "♀",
        _ => ""
    };
}
