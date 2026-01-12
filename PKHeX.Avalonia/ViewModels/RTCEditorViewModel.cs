using System;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PKHeX.Core;

namespace PKHeX.Avalonia.ViewModels;

public partial class RTCEditorViewModel : ViewModelBase
{
    private readonly SaveFile _sav;
    private readonly IGen3Hoenn? _hoenn;
    private RTC3? _clockInitial;
    private RTC3? _clockElapsed;

    public RTCEditorViewModel(SaveFile sav)
    {
        _sav = sav;
        _hoenn = sav as IGen3Hoenn;
        IsSupported = _hoenn is not null;

        if (IsSupported)
            LoadData();
    }

    public bool IsSupported { get; }

    // Initial Clock
    [ObservableProperty] private int _initialDay;
    [ObservableProperty] private int _initialHour;
    [ObservableProperty] private int _initialMinute;
    [ObservableProperty] private int _initialSecond;

    // Elapsed Clock
    [ObservableProperty] private int _elapsedDay;
    [ObservableProperty] private int _elapsedHour;
    [ObservableProperty] private int _elapsedMinute;
    [ObservableProperty] private int _elapsedSecond;

    private void LoadData()
    {
        if (_hoenn is null) return;

        _clockInitial = _hoenn.ClockInitial;
        _clockElapsed = _hoenn.ClockElapsed;

        InitialDay = _clockInitial.Day;
        InitialHour = Math.Min(23, _clockInitial.Hour);
        InitialMinute = Math.Min(59, _clockInitial.Minute);
        InitialSecond = Math.Min(59, _clockInitial.Second);

        ElapsedDay = _clockElapsed.Day;
        ElapsedHour = Math.Min(23, _clockElapsed.Hour);
        ElapsedMinute = Math.Min(59, _clockElapsed.Minute);
        ElapsedSecond = Math.Min(59, _clockElapsed.Second);
    }

    [RelayCommand]
    private void Save()
    {
        if (_hoenn is null || _clockInitial is null || _clockElapsed is null) return;

        _clockInitial.Day = (ushort)InitialDay;
        _clockInitial.Hour = (byte)InitialHour;
        _clockInitial.Minute = (byte)InitialMinute;
        _clockInitial.Second = (byte)InitialSecond;

        _clockElapsed.Day = (ushort)ElapsedDay;
        _clockElapsed.Hour = (byte)ElapsedHour;
        _clockElapsed.Minute = (byte)ElapsedMinute;
        _clockElapsed.Second = (byte)ElapsedSecond;

        _hoenn.ClockInitial = _clockInitial;
        _hoenn.ClockElapsed = _clockElapsed;
    }

    [RelayCommand]
    private void Reset()
    {
        InitialDay = InitialHour = InitialMinute = InitialSecond = 0;
        ElapsedDay = ElapsedHour = ElapsedMinute = ElapsedSecond = 0;
    }

    [RelayCommand]
    private void BerryFix()
    {
        // Advance elapsed days to fix berry glitch
        ElapsedDay = Math.Max((2 * 366) + 2, ElapsedDay);
    }

    [RelayCommand]
    private void Refresh() => LoadData();
}
