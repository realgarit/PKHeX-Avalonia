using System;
using System.Collections.Generic;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PKHeX.Avalonia.Services;
using PKHeX.Core;

namespace PKHeX.Avalonia.ViewModels;

public partial class TrashEditorViewModel : ViewModelBase
{
    private readonly IStringConverter _converter;
    private readonly byte[] _raw;
    private readonly EntityContext _context;
    private readonly int _generation;

    [ObservableProperty]
    private string _currentText;
    
    [ObservableProperty]
    private string _finalText;

    [ObservableProperty]
    private string _filterText = string.Empty;

    public byte[] FinalBytes { get; private set; }

    public System.Collections.ObjectModel.ObservableCollection<TrashByteViewModel> Bytes { get; } = [];

    [ObservableProperty]
    private System.Collections.Generic.IEnumerable<ComboItem> _speciesList;
    
    [ObservableProperty]
    private ComboItem? _selectedSpecies;

    [ObservableProperty]
    private System.Collections.Generic.IEnumerable<ComboItem> _languageList;

    [ObservableProperty]
    private ComboItem? _selectedLanguage;

    [ObservableProperty]
    private int _selectedGeneration;

    public Action? CloseRequested { get; set; }

    public TrashEditorViewModel(string initialText, byte[]? existingBytes, IStringConverter converter, int generation, EntityContext context)
    {
        _converter = converter;
        _context = context;
        _generation = generation;
        SelectedGeneration = generation;

        _currentText = initialText;
        _finalText = initialText;

        if (existingBytes != null && existingBytes.Length > 0)
        {
            _raw = existingBytes.ToArray();
        }
        else
        {
            // If no existing bytes, maybe determine from text?
            // Usually this is called when specific bytes are provided.
            // If not, we might defaults.
            // For now, assume empty or based on converter.
             Span<byte> temp = stackalloc byte[200]; // arbitrarily large
             int len = converter.SetString(temp, initialText.AsSpan(), initialText.Length, StringConverterOption.None);
             _raw = temp.Slice(0, visibleLength: len).ToArray();
             // Adjust length to be "Trash" length?
             // Usually trash bytes are a specific buffer size. 
             // Logic in WinForms: if raw.Length != 0, use it. Else empty.
             if (_raw.Length == 0) _raw = [];
        }

        FinalBytes = _raw.ToArray();
        
        // Populate Bytes
        for (int i = 0; i < _raw.Length; i++)
        {
            Bytes.Add(new TrashByteViewModel(i, _raw[i], UpdateByte));
        }

        // Setup drop downs
        var source = GameInfo.Sources;
        _speciesList = source.SpeciesDataSource;
        SelectedSpecies = _speciesList.FirstOrDefault();

        _languageList = GameInfo.LanguageDataSource((byte)generation, context);
        SelectedLanguage = _languageList.FirstOrDefault();
    }

    private void UpdateByte(int index, byte val)
    {
        if (index >= 0 && index < FinalBytes.Length)
            FinalBytes[index] = val;
        
        // Update string preview?
        // _converter.GetString(FinalBytes);
        CurrentText = _converter.GetString(FinalBytes);
    }

    [RelayCommand]
    private void Save()
    {
        FinalText = CurrentText;
        CloseRequested?.Invoke();
    }

    [RelayCommand]
    private void ApplyTrash()
    {
        if (SelectedSpecies is null || SelectedLanguage is null) return;
        
        var species = (ushort)SelectedSpecies.Value;
        var lang = SelectedLanguage.Value;
        
        string text = SpeciesName.GetSpeciesNameGeneration(species, lang, (byte)SelectedGeneration);
        if (string.IsNullOrEmpty(text)) text = SelectedSpecies.Text;

        // Convert to bytes
        Span<byte> temp = stackalloc byte[FinalBytes.Length];
        var written = _converter.SetString(temp, text.AsSpan(), text.Length, StringConverterOption.None);
        var data = temp.Slice(0, written);

        // Current text bytes
        Span<byte> currentTemp = stackalloc byte[FinalBytes.Length];
         var currentWritten = _converter.SetString(currentTemp, CurrentText.AsSpan(), CurrentText.Length, StringConverterOption.None);
        var currentData = currentTemp.Slice(0, currentWritten);

        // "Trash byte layer is hidden by current text" logic from WinForms
        if (written <= currentWritten)
        {
            // Alert user?
            return;
        }

        // Apply trash bytes (bytes after the current string null terminator or end)
        for (int i = currentWritten; i < written && i < FinalBytes.Length; i++)
        {
            FinalBytes[i] = data[i];
            Bytes[i].Value = data[i];
        }
    }

    [RelayCommand]
    private void ClearTrash()
    {
        Span<byte> currentTemp = stackalloc byte[FinalBytes.Length];
        var currentWritten = _converter.SetString(currentTemp, CurrentText.AsSpan(), CurrentText.Length, StringConverterOption.None);
        
        for (int i = currentWritten; i < FinalBytes.Length; i++)
        {
            FinalBytes[i] = 0;
            Bytes[i].Value = 0;
        }
    }
}

public partial class TrashByteViewModel : ObservableObject
{
    public int Index { get; }
    
    private readonly Action<int, byte> _callback;

    [ObservableProperty]
    private byte _value;

    [ObservableProperty]
    private string _hexValue;

    public TrashByteViewModel(int index, byte val, Action<int, byte> callback)
    {
        Index = index;
        _value = val;
        _hexValue = $"{val:X2}";
        _callback = callback;
    }

    partial void OnValueChanged(byte value)
    {
        HexValue = $"{value:X2}";
        _callback(Index, value);
    }
    
    partial void OnHexValueChanged(string value)
    {
        if (byte.TryParse(value, System.Globalization.NumberStyles.HexNumber, null, out var b))
        {
            if (b != Value)
            {
                Value = b;
            }
        }
    }
}
