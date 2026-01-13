using System;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PKHeX.Core;

namespace PKHeX.Avalonia.ViewModels;

/// <summary>
/// RTC (Real Time Clock) editor for Gen 3 Hoenn saves (RSE).
/// </summary>
public partial class RTC3EditorViewModel : ViewModelBase
{
    private readonly SaveFile _sav;
    private readonly IGen3Hoenn _clone;
    private readonly RTC3 _clockInitial;
    private readonly RTC3 _clockElapsed;

    // Initial Clock
    [ObservableProperty]
    private int _initialDay;

    [ObservableProperty]
    private int _initialHour;

    [ObservableProperty]
    private int _initialMinute;

    [ObservableProperty]
    private int _initialSecond;

    // Elapsed Clock
    [ObservableProperty]
    private int _elapsedDay;

    [ObservableProperty]
    private int _elapsedHour;

    [ObservableProperty]
    private int _elapsedMinute;

    [ObservableProperty]
    private int _elapsedSecond;

    public RTC3EditorViewModel(SaveFile sav)
    {
        _sav = sav;
        _clone = (IGen3Hoenn)sav.Clone();
        _clockInitial = _clone.ClockInitial;
        _clockElapsed = _clone.ClockElapsed;

        LoadData();
    }

    private void LoadData()
    {
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
    private void Reset()
    {
        InitialDay = 0;
        InitialHour = 0;
        InitialMinute = 0;
        InitialSecond = 0;

        ElapsedDay = 0;
        ElapsedHour = 0;
        ElapsedMinute = 0;
        ElapsedSecond = 0;
    }

    [RelayCommand]
    private void BerryFix()
    {
        // The berry glitch fix requires elapsed days to be at least 2 years + 2 days
        ElapsedDay = Math.Max((2 * 366) + 2, ElapsedDay);
    }

    [RelayCommand]
    private void Save()
    {
        _clockInitial.Day = InitialDay;
        _clockInitial.Hour = InitialHour;
        _clockInitial.Minute = InitialMinute;
        _clockInitial.Second = InitialSecond;

        _clockElapsed.Day = ElapsedDay;
        _clockElapsed.Hour = ElapsedHour;
        _clockElapsed.Minute = ElapsedMinute;
        _clockElapsed.Second = ElapsedSecond;

        _clone.ClockInitial = _clockInitial;
        _clone.ClockElapsed = _clockElapsed;

        _sav.CopyChangesFrom((SaveFile)_clone);
    }
}
