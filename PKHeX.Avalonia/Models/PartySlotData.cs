using Avalonia.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;

namespace PKHeX.Avalonia.Models;

/// <summary>
/// Data for a single party slot, displayed in the PartyViewer list.
/// </summary>
public partial class PartySlotData : ObservableObject
{
    [ObservableProperty] private int _slot;
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
    [ObservableProperty] private ushort _currentHp;
    [ObservableProperty] private ushort _maxHp;
    [ObservableProperty] private byte _status; // Status condition
    
    /// <summary>
    /// HP percentage for display.
    /// </summary>
    public double HpPercentage => MaxHp > 0 ? (double)CurrentHp / MaxHp : 0;
    
    public string GenderSymbol => Gender switch
    {
        0 => "♂",
        1 => "♀",
        _ => ""
    };
    
    /// <summary>
    /// Short summary for tooltip.
    /// </summary>
    public string ToolTipSummary => IsEmpty 
        ? "Empty" 
        : $"{Nickname} ({SpeciesName})\nLv. {Level} {GenderSymbol}\nHP: {CurrentHp}/{MaxHp}";
}
