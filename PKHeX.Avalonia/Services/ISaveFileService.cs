using PKHeX.Core;

namespace PKHeX.Avalonia.Services;

public interface ISaveFileService
{
    SaveFile? CurrentSave { get; }
    bool HasSave { get; }

    Task<bool> LoadSaveFileAsync(string path);
    Task<bool> SaveFileAsync(string? path = null);
    void CloseSave();

    event Action<SaveFile?>? SaveFileChanged;
}
