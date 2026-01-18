using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Messaging;
using PKHeX.Core;

namespace PKHeX.Avalonia.Services;

/// <summary>
/// Manages application display language and provides localized string resources.
/// </summary>
public partial class LanguageService : ObservableObject
{
    private static readonly string[] SupportedLanguages = ["en", "ja", "fr", "it", "de", "es", "ko", "zh-Hans", "zh-Hant"];
    private static readonly string[] LanguageNames = ["English", "日本語", "Français", "Italiano", "Deutsch", "Español", "한국어", "简体中文", "繁體中文"];

    [ObservableProperty]
    private string _currentLanguage = "en";

    public IReadOnlyList<LanguageOption> AvailableLanguages { get; }

    public event Action? LanguageChanged;

    public LanguageService()
    {
        AvailableLanguages = SupportedLanguages
            .Select((code, i) => new LanguageOption(code, LanguageNames[i]))
            .ToList();
    }

    public void SetLanguage(string languageCode)
    {
        if (!SupportedLanguages.Contains(languageCode))
            languageCode = "en";

        CurrentLanguage = languageCode;
        
        // Update PKHeX.Core's GameInfo to use this language
        GameInfo.CurrentLanguage = languageCode;
        GameInfo.Strings = GameInfo.GetStrings(languageCode);
        
        // Update FilteredSources if we have a save context (this is a bit hacky but keeps it central)
        // Ideally we'd have a reference to the current save here, but we can rely on MainWindowViewModel to do it too.
        
        LanguageChanged?.Invoke();
        WeakReferenceMessenger.Default.Send(new LanguageChangedMessage(languageCode));
    }
}

public record LanguageChangedMessage(string LanguageCode);

public record LanguageOption(string Code, string Name)
{
    public override string ToString() => Name;
}
