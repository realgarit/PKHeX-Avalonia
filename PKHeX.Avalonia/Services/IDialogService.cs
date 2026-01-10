using Avalonia.Controls;

namespace PKHeX.Avalonia.Services;

public interface IDialogService
{
    Task<string?> OpenFileAsync(string title, string[]? filters = null);
    Task<string?> SaveFileAsync(string title, string? defaultFileName = null, string[]? filters = null);
    Task ShowErrorAsync(string title, string message);
    Task<bool> ShowDialogAsync(Control content, string title);
    Task<string?> GetClipboardTextAsync();
    Task SetClipboardTextAsync(string text);
}
