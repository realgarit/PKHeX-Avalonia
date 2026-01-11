using global::Avalonia;
using global::Avalonia.Controls;
using global::Avalonia.Controls.ApplicationLifetimes;
using global::Avalonia.Layout;
using global::Avalonia.Media;
using global::Avalonia.Platform.Storage;

namespace PKHeX.Avalonia.Services;

public sealed class DialogService : IDialogService
{
    private Window? GetMainWindow()
    {
        if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            return desktop.MainWindow;
        return null;
    }

    public async Task<string?> OpenFileAsync(string title, string[]? filters = null)
    {
        var window = GetMainWindow();
        if (window is null) return null;

        var fileTypes = new List<FilePickerFileType>
        {
            new("All Files") { Patterns = ["*.*"] }
        };

        if (filters is not null)
        {
            fileTypes.Insert(0, new FilePickerFileType("Supported Files") { Patterns = filters });
        }

        var options = new FilePickerOpenOptions
        {
            Title = title,
            AllowMultiple = false,
            FileTypeFilter = fileTypes
        };

        var result = await window.StorageProvider.OpenFilePickerAsync(options);
        var file = result.FirstOrDefault();

        if (file is null)
            return null;

        // On macOS, TryGetLocalPath may be needed
        if (file.TryGetLocalPath() is { } localPath)
            return localPath;

        // Fallback to Uri path
        return file.Path.LocalPath;
    }
    public async Task<string?> OpenFolderAsync(string title)
    {
        var window = GetMainWindow();
        if (window is null) return null;

        var options = new FolderPickerOpenOptions
        {
            Title = title,
            AllowMultiple = false
        };

        var result = await window.StorageProvider.OpenFolderPickerAsync(options);
        var folder = result.FirstOrDefault();

        if (folder is null)
            return null;

        if (folder.TryGetLocalPath() is { } localPath)
            return localPath;

        return folder.Path.LocalPath;
    }

    public async Task<string?> SaveFileAsync(string title, string? defaultFileName = null, string[]? filters = null)
    {
        var window = GetMainWindow();
        if (window is null) return null;

        var options = new FilePickerSaveOptions
        {
            Title = title,
            SuggestedFileName = defaultFileName
        };

        var result = await window.StorageProvider.SaveFilePickerAsync(options);
        return result?.Path.LocalPath;
    }

    public async Task ShowErrorAsync(string title, string message) => await ShowMessageBoxAsync(title, message);
    public async Task ShowInformationAsync(string title, string message) => await ShowMessageBoxAsync(title, message);

    private async Task ShowMessageBoxAsync(string title, string message)
    {
        var window = GetMainWindow();
        if (window is null) return;

        var dialog = new Window
        {
            Title = title,
            Width = 350,
            Height = 150,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            CanResize = false,
            Content = new StackPanel
            {
                Margin = new Thickness(20),
                Spacing = 16,
                Children =
                {
                    new TextBlock
                    {
                        Text = message,
                        TextWrapping = TextWrapping.Wrap
                    },
                    new Button
                    {
                        Content = "OK",
                        HorizontalAlignment = HorizontalAlignment.Center,
                        MinWidth = 80
                    }
                }
            }
        };

        var button = ((StackPanel)dialog.Content).Children[1] as Button;
        button!.Click += (_, _) => dialog.Close();

        await dialog.ShowDialog(window);
    }

    public async Task<bool> ShowDialogAsync(Control content, string title)
    {
        var window = GetMainWindow();
        if (window is null) return false;

        var result = false;

        var dialog = new Window
        {
            Title = title,
            Content = content,
            SizeToContent = SizeToContent.WidthAndHeight,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            CanResize = false
        };



        await dialog.ShowDialog(window);
        return result;
    }

    public async Task<string?> GetClipboardTextAsync()
    {
        var window = GetMainWindow();
        if (window?.Clipboard is { } clipboard)
            return await clipboard.GetTextAsync();
        return null;
    }

    public async Task SetClipboardTextAsync(string text)
    {
        var window = GetMainWindow();
        if (window?.Clipboard is { } clipboard)
            await clipboard.SetTextAsync(text);
    }
}
