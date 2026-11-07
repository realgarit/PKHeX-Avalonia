using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Linq;
using PKHeX.Application.Abstractions;

namespace PKHeX.Avalonia.Tests.Harness;

/// <summary>
/// Deterministic <see cref="IDialogService"/> stand-in for the headless harness. Never opens a real
/// picker or message box (there is no interactive user in a headless run); instead it records every
/// call and returns canned results, so a flow that would otherwise block on a dialog stays fast and
/// assertable. Configure <see cref="OpenFileResult"/>/<see cref="ConfirmResult"/> per test as needed.
/// </summary>
public sealed class RecordingDialogService : IDialogService
{
    public List<(string Title, string Message)> Errors { get; } = [];
    public List<(string Title, string Message)> Infos { get; } = [];
    public List<(string Title, string Message)> Confirmations { get; } = [];
    public List<(string Title, string Message)> UnsavedPrompts { get; } = [];
    public UnsavedChangesChoice UnsavedChoice { get; set; } = UnsavedChangesChoice.Cancel;
    public string? SaveFileResult { get; set; }

    /// <summary>Path returned by <see cref="OpenFileAsync"/> (simulating the native picker result).</summary>
    public string? OpenFileResult { get; set; }

    /// <summary>Path returned by <see cref="OpenFolderAsync"/> (simulating the native picker result).</summary>
    public string? OpenFolderResult { get; set; }

    /// <summary>Result returned by <see cref="ShowConfirmationAsync"/>.</summary>
    public bool ConfirmResult { get; set; }

    public Task<string?> OpenFileAsync(string title, string[]? filters = null) => Task.FromResult(OpenFileResult);
    public Task<string?> OpenFolderAsync(string title) => Task.FromResult(OpenFolderResult);
    public Task<string?> SaveFileAsync(string title, string? defaultFileName = null, string[]? filters = null) => Task.FromResult(SaveFileResult);

    public Task ShowErrorAsync(string title, string message)
    {
        Errors.Add((title, message));
        return Task.CompletedTask;
    }

    public Task ShowInformationAsync(string title, string message)
    {
        Infos.Add((title, message));
        return Task.CompletedTask;
    }

    public Task<bool> ShowConfirmationAsync(string title, string message, string confirmText = "Yes", string cancelText = "Cancel")
    {
        Confirmations.Add((title, message));
        return Task.FromResult(ConfirmResult);
    }

    public Task<UnsavedChangesChoice> ShowUnsavedChangesAsync(string title, string message,
        string saveText, string discardText, string cancelText)
    {
        UnsavedPrompts.Add((title, message));
        return Task.FromResult(UnsavedChoice);
    }

    public void RevealInFileManager(string path) { }
    public Task<string?> GetClipboardTextAsync() => Task.FromResult<string?>(null);
    public Task SetClipboardTextAsync(string text) => Task.CompletedTask;
}

/// <summary>
/// No-op <see cref="IWindowService"/> for the headless harness: opening child/tool windows requires
/// a live desktop windowing surface that headless does not provide, so these calls are recorded and
/// ignored rather than actually shown. Tests that need to assert a dialog/tool was requested can read
/// <see cref="ShownDialogs"/>/<see cref="ShownTools"/>.
/// </summary>
public sealed class NoopWindowService : IWindowService
{
    private readonly HashSet<object> _openTools = [];
    private readonly Dictionary<object, Action> _closedCallbacks = [];

    public List<(object ViewModel, string Title)> ShownDialogs { get; } = [];
    public List<(object ViewModel, string Title)> ShownTools { get; } = [];
    public List<object> FocusedTools { get; } = [];
    public IReadOnlyCollection<object> ActiveTools => _openTools;
    public object? FocusedTool { get; private set; }
    public int CloseAllToolsCount { get; private set; }
    public int ActiveToolCount => _openTools.Count;

    public Task ShowDialogAsync(object viewModel, string title)
    {
        ShownDialogs.Add((viewModel, title));
        return Task.CompletedTask;
    }

    public List<object> ClosedTools { get; } = [];

    public void ShowTool(object viewModel, string title, Action onClosed)
    {
        var opened = !_openTools.Contains(viewModel);
        ShowTool(viewModel, title);
        if (opened)
            _closedCallbacks[viewModel] = onClosed;
    }

    public void CloseTool(object viewModel)
    {
        if (!_openTools.Remove(viewModel))
            return;
        if (ReferenceEquals(FocusedTool, viewModel))
            FocusedTool = null;
        ClosedTools.Add(viewModel);
        if (_closedCallbacks.Remove(viewModel, out var callback))
            callback();
    }

    public void ShowTool(object viewModel, string title)
    {
        var alreadyOpen = !_openTools.Add(viewModel);
        FocusedTool = viewModel;
        if (alreadyOpen)
        {
            FocusedTools.Add(viewModel);
            return;
        }

        ShownTools.Add((viewModel, title));
    }

    public void CloseAllTools()
    {
        var callbacks = _closedCallbacks.Values.ToList();
        ClosedTools.AddRange(_openTools);
        _openTools.Clear();
        _closedCallbacks.Clear();
        FocusedTool = null;
        CloseAllToolsCount++;
        foreach (var callback in callbacks)
            callback();
    }
}
