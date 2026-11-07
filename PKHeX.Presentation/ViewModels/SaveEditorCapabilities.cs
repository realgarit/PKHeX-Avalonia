using PKHeX.Core;

namespace PKHeX.Presentation.ViewModels;

/// <summary>
/// Save-capability checks shared by the capability registry (Tools menu, tool launcher, shortcuts)
/// for editors that are not tied to one save class. Each check mirrors the support test of the
/// editor it gates, so a visible entry always opens a working editor (#255).
/// </summary>
public static class SaveEditorCapabilities
{
    /// <summary>Mirrors <see cref="DaycareEditorViewModel.HasDaycare"/>: editable or read-only slots.</summary>
    public static bool HasDaycare(SaveFile save) =>
        save is IDaycareMulti or IDaycareStorage or SAV7b or SAV8SWSH;

    /// <summary>Mirrors <see cref="RecordsEditorViewModel.HasRecords"/>: record storage plus a known record list.</summary>
    public static bool HasRecords(SaveFile save) =>
        (save is SAV5 or ITrainerStatRecord) && (save is SAV8BS || save.Generation is 5 or 6 or 7 or 8);

    /// <summary>Mirrors <see cref="BoxLayoutEditorViewModel.IsSupported"/>: box names or wallpapers.</summary>
    public static bool HasBoxLayout(SaveFile save) => save is IBoxDetailNameRead or IBoxDetailWallpaper;

    /// <summary>
    /// Mirrors <see cref="DonutEditorViewModel.IsSupported"/>: the donut block exists only after the
    /// feature is unlocked in-game; before that the accessor returns a short placeholder block.
    /// </summary>
    public static bool HasDonuts(SaveFile save) =>
        save is SAV9ZA za && za.Donuts.Data.Length >= DonutPocket9a.MaxCount * Donut9a.Size;
}
