using System.Collections.Generic;
using CommunityToolkit.Mvvm.ComponentModel;
using PKHeX.Application.Abstractions;

namespace PKHeX.Presentation.ViewModels;

public partial class MainWindowViewModel
{
    /// <summary>Product-facing appearance choices shown in the compact shell.</summary>
    public IReadOnlyList<AppTheme> Themes { get; } = [AppTheme.Dark, AppTheme.Light];

    [ObservableProperty]
    private AppTheme _selectedTheme = AppTheme.Dark;

    partial void OnSelectedThemeChanged(AppTheme value)
    {
        if (_themeService.CurrentTheme != value)
            _themeService.ApplyTheme(value);
    }

    /// <summary>Synchronizes the shell picker after Settings applies a theme.</summary>
    public void RefreshThemeSelection() => SelectedTheme = _themeService.CurrentTheme;
}
