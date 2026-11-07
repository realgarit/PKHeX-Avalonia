namespace PKHeX.Application.Abstractions;

/// <summary>
/// Shows a ViewModel as a modal dialog window. The host resolves the matching View via a ViewLocator,
/// so the Presentation layer never references View types.
/// </summary>
public interface IWindowService
{
    Task ShowDialogAsync(object viewModel, string title);

    /// <summary>
    /// Shows a ViewModel as a modeless (non-modal) tool window the user can drag around and
    /// operate while interacting with the main window. Re-invoking with the same ViewModel
    /// instance focuses the existing window instead of opening a duplicate. The window remembers
    /// its size/position for the session (keyed by ViewModel type).
    /// </summary>
    void ShowTool(object viewModel, string title);

    /// <summary>
    /// Same as <see cref="ShowTool(object, string)"/>, and runs <paramref name="onClosed"/> once when
    /// that window closes for any reason (the user, <see cref="CloseTool"/> or
    /// <see cref="CloseAllTools"/>). If the window is already open it is focused and the callback
    /// registered when it opened stays in effect.
    /// </summary>
    void ShowTool(object viewModel, string title, Action onClosed);

    /// <summary>Closes the modeless tool window of <paramref name="viewModel"/>, if one is open.</summary>
    void CloseTool(object viewModel);

    /// <summary>Closes all open modeless tool windows (e.g. when the active save changes).</summary>
    void CloseAllTools();
}
