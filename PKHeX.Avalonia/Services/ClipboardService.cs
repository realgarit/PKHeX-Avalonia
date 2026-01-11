
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;

namespace PKHeX.Avalonia.Services;

public class ClipboardService : IClipboardService
{
    private global::Avalonia.Input.Platform.IClipboard? clipboard;

    private global::Avalonia.Input.Platform.IClipboard? GetClipboard()
    {
        if (clipboard is not null) return clipboard;

        if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime { MainWindow: { } window })
        {
            clipboard = window.Clipboard;
            return clipboard;
        }
        
        // Fallback for other lifetimes if needed, though mostly Desktop for now
        return TopLevel.GetTopLevel(null)?.Clipboard;
    }

    public async Task<string?> GetTextAsync()
    {
        var cb = GetClipboard();
        if (cb is null) return null;
        return await cb.GetTextAsync();
    }

    public async Task SetTextAsync(string text)
    {
        var cb = GetClipboard();
        if (cb is null) return;
        await cb.SetTextAsync(text);
    }
}
