namespace PKHeX.Avalonia.Services;

public interface IDialogService
{
    Task<string?> OpenFileAsync(string title, string[]? filters = null);
    Task<string?> SaveFileAsync(string title, string? defaultFileName = null, string[]? filters = null);
    Task ShowErrorAsync(string title, string message);
}
