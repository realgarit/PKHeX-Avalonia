using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PKHeX.Application.Services;
using PKHeX.Core;

namespace PKHeX.Presentation.ViewModels;

public partial class BatchEditorViewModel : ViewModelBase, IDisposable
{
    private readonly SaveFile _sav;
    private readonly IDialogService _dialogService;
    private readonly UndoRedoService _undoRedo;
    private readonly List<UndoRedoService.BatchToken> _batchTokens = [];
    private readonly IUiDispatcher? _uiDispatcher;
    private CancellationTokenSource? _previewCancellation;
    private CancellationTokenSource? _runCancellation;
    private long _previewVersion;
    private long _stateVersion;
    private bool _disposed;

    public event Action? BatchEditCompleted;

    public BatchEditorViewModel(
        SaveFile sav,
        IDialogService dialogService,
        UndoRedoService? undoRedo = null,
        IUiDispatcher? uiDispatcher = null)
    {
        _sav = sav;
        _dialogService = dialogService;
        _undoRedo = undoRedo ?? new UndoRedoService();
        _uiDispatcher = uiDispatcher;
        if (undoRedo is null)
            _undoRedo.Initialize(sav);
        _undoRedo.StateChanged += OnUndoRedoStateChanged;

        PropertySuggestions = GetCommonPkmProperties();
        RefreshPreview();
    }

    private static List<string> GetCommonPkmProperties()
    {
        var props = typeof(PKM)
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.CanRead && p.CanWrite && p.GetIndexParameters().Length == 0)
            .Select(p => p.Name)
            .OrderBy(n => n, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var priority = new[]
        {
            "Species", "Nickname", "CurrentLevel", "IsShiny", "Nature", "Ability",
            "Gender", "HeldItem", "Ball", "OriginalTrainerFriendship", "IsEgg",
            "IV_HP", "IV_ATK", "IV_DEF", "IV_SPA", "IV_SPD", "IV_SPE",
            "EV_HP", "EV_ATK", "EV_DEF", "EV_SPA", "EV_SPD", "EV_SPE",
            "Move1", "Move2", "Move3", "Move4",
            "OriginalTrainerName", "Language", "Version",
        };

        var result = priority.Where(props.Contains).ToList();
        result.AddRange(props.Except(priority));
        return result;
    }

    public IReadOnlyList<string> PropertySuggestions { get; }

    [ObservableProperty]
    private string _instructions = string.Empty;

    [ObservableProperty]
    private string _results = string.Empty;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(RunBatchCommand))]
    [NotifyCanExecuteChangedFor(nameof(SetMaxIVsCommand))]
    [NotifyCanExecuteChangedFor(nameof(SetMaxEVsCommand))]
    [NotifyCanExecuteChangedFor(nameof(SetShinyCommand))]
    [NotifyCanExecuteChangedFor(nameof(HealAllCommand))]
    private bool _editBoxes = true;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(RunBatchCommand))]
    [NotifyCanExecuteChangedFor(nameof(SetMaxIVsCommand))]
    [NotifyCanExecuteChangedFor(nameof(SetMaxEVsCommand))]
    [NotifyCanExecuteChangedFor(nameof(SetShinyCommand))]
    [NotifyCanExecuteChangedFor(nameof(HealAllCommand))]
    private bool _editParty;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(RunBatchCommand))]
    private int _affectedCount;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(RunBatchCommand))]
    [NotifyCanExecuteChangedFor(nameof(SetMaxIVsCommand))]
    [NotifyCanExecuteChangedFor(nameof(SetMaxEVsCommand))]
    [NotifyCanExecuteChangedFor(nameof(SetShinyCommand))]
    [NotifyCanExecuteChangedFor(nameof(HealAllCommand))]
    private bool _isRunning;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(AddFilterCommand))]
    [NotifyCanExecuteChangedFor(nameof(AddInstructionCommand))]
    private string _selectedProperty = string.Empty;

    [ObservableProperty]
    private string _selectedOperator = "=";

    [ObservableProperty]
    private string _selectedValue = string.Empty;

    public IReadOnlyList<string> Operators { get; } = ["=", "!", ".", "=RNG", "=POKEMON"];

    partial void OnInstructionsChanged(string value) => RefreshPreview();
    partial void OnEditBoxesChanged(bool value) => RefreshPreview();
    partial void OnEditPartyChanged(bool value) => RefreshPreview();

    [RelayCommand(CanExecute = nameof(CanAddInstruction))]
    private void AddInstruction()
    {
        if (string.IsNullOrWhiteSpace(SelectedProperty))
            return;

        var instruction = $".{SelectedProperty}{SelectedOperator}{SelectedValue}";

        if (!string.IsNullOrEmpty(Instructions))
            Instructions += Environment.NewLine;
        Instructions += instruction;
    }

    [RelayCommand(CanExecute = nameof(CanAddInstruction))]
    private void AddFilter()
    {
        if (string.IsNullOrWhiteSpace(SelectedProperty))
            return;

        var filter = $"={SelectedProperty}{SelectedOperator}{SelectedValue}";

        if (!string.IsNullOrEmpty(Instructions))
            Instructions += Environment.NewLine;
        Instructions += filter;
    }

    [RelayCommand]
    private void ClearInstructions()
    {
        Instructions = string.Empty;
        Results = string.Empty;
    }

    [RelayCommand(CanExecute = nameof(CanRunBatch))]
    private async Task RunBatchAsync()
    {
        if (!CanRunBatch())
            return;

        await RunBatchCoreAsync();
    }

    private async Task RunBatchCoreAsync()
    {
        if (IsRunning)
            return;

        IsRunning = true;
        CancelPreview();
        var runCancellation = new CancellationTokenSource();
        _runCancellation = runCancellation;
        var cancellationToken = runCancellation.Token;
        var text = Instructions;
        var editBoxes = EditBoxes;
        var editParty = EditParty;
        var stateVersion = Volatile.Read(ref _stateVersion);
        var historyVersion = _undoRedo.ChangeCount;
        try
        {
            var plan = await Task.Run(
                () => BuildPlan(text, editBoxes, editParty, cancellationToken),
                cancellationToken);
            if (plan is null || plan.Changes.Count == 0)
            {
                RefreshIfActive();
                return;
            }

            if (!CanCommitRun(text, editBoxes, editParty, stateVersion, historyVersion, cancellationToken))
            {
                RefreshIfActive();
                return;
            }

            Results = plan.Editor.GetEditorResults(plan.Sets);
            var undoSlots = GetUndoSlots(plan.Changes);
            var token = _undoRedo.ApplyBatch(undoSlots, _ =>
            {
                foreach (var change in plan.Changes)
                {
                    if (!change.Slot.WriteTo(_sav, change.Pokemon.Clone(), EntityImportSettings.None))
                        throw new InvalidOperationException("Unable to write a batch-edited slot.");
                }
            });

            if (token is null)
            {
                RefreshIfActive();
                return;
            }

            _batchTokens.Add(token);
            BatchEditCompleted?.Invoke();
            RefreshPreview();
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // The save or editor state changed while the background plan was being built.
        }
        catch (Exception ex) when (!_disposed)
        {
            Results = $"Error: {ex.Message}";
        }
        finally
        {
            if (ReferenceEquals(_runCancellation, runCancellation))
            {
                _runCancellation = null;
                runCancellation.Dispose();
            }
            IsRunning = false;
            ResetBatchCommand.NotifyCanExecuteChanged();
        }
    }

    [RelayCommand(CanExecute = nameof(CanResetBatch))]
    private void ResetBatch()
    {
        var resetAny = false;
        while (_batchTokens.Count != 0)
        {
            var token = _batchTokens[^1];
            if (!_undoRedo.TryUndoBatch(token))
                break;

            _batchTokens.RemoveAt(_batchTokens.Count - 1);
            resetAny = true;
        }

        if (!resetAny)
            return;

        Results = string.Empty;
        BatchEditCompleted?.Invoke();
        RefreshPreview();
        ResetBatchCommand.NotifyCanExecuteChanged();
    }

    [RelayCommand(CanExecute = nameof(CanRunQuickAction))]
    private async Task SetMaxIVs()
    {
        Instructions = ".IVs=$suggestPokemon MaxIVs($0)";
        await RunBatchCoreAsync();
    }

    [RelayCommand(CanExecute = nameof(CanRunQuickAction))]
    private async Task SetMaxEVs()
    {
        Instructions = ".EVs=$suggestPokemon MaxEVs($0)";
        await RunBatchCoreAsync();
    }

    [RelayCommand(CanExecute = nameof(CanRunQuickAction))]
    private async Task SetShiny()
    {
        Instructions = ".Shiny=Star";
        await RunBatchCoreAsync();
    }

    [RelayCommand(CanExecute = nameof(CanRunQuickAction))]
    private async Task HealAll()
    {
        Instructions = ".Heal";
        await RunBatchCoreAsync();
    }

    private bool CanAddInstruction() => !string.IsNullOrWhiteSpace(SelectedProperty);
    private bool CanRunBatch() => !IsRunning && AffectedCount > 0;

    private bool CanRunQuickAction() => !IsRunning && GetWritableTargetCount() > 0;

    private bool CanResetBatch() => _batchTokens.Count != 0 && _undoRedo.CanUndoBatch(_batchTokens[^1]);

    private void OnUndoRedoStateChanged(object? sender, EventArgs e) => PostToUi(() =>
    {
        Interlocked.Increment(ref _stateVersion);
        CancelRun();
        ResetBatchCommand.NotifyCanExecuteChanged();
        NotifyTargetCommands();
        RefreshPreview();
    });

    /// <summary>Refreshes preview and command state after a direct save-slot mutation.</summary>
    public void RefreshExternalState()
    {
        if (_disposed)
            return;

        Interlocked.Increment(ref _stateVersion);
        CancelRun();
        NotifyTargetCommands();
        RefreshPreview();
    }

    private void RefreshPreview()
    {
        if (_disposed)
            return;

        CancelPreview();
        var cancellation = new CancellationTokenSource();
        _previewCancellation = cancellation;
        var version = Interlocked.Increment(ref _previewVersion);
        var text = Instructions;
        var editBoxes = EditBoxes;
        var editParty = EditParty;

        if (string.IsNullOrWhiteSpace(text))
        {
            AffectedCount = 0;
            ResetBatchCommand.NotifyCanExecuteChanged();
            return;
        }

        _ = RefreshPreviewAsync(text, editBoxes, editParty, version, cancellation.Token);
    }

    private async Task RefreshPreviewAsync(
        string text,
        bool editBoxes,
        bool editParty,
        long version,
        CancellationToken cancellationToken)
    {
        try
        {
            await Task.Delay(150, cancellationToken);
            var count = await Task.Run(
                () => BuildPlan(text, editBoxes, editParty)?.Changes.Count ?? 0,
                cancellationToken);

            PostToUi(() =>
            {
                if (_disposed || cancellationToken.IsCancellationRequested
                    || version != Volatile.Read(ref _previewVersion))
                    return;

                AffectedCount = count;
                ResetBatchCommand.NotifyCanExecuteChanged();
            });
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // A newer edit or undo/redo request superseded this preview.
        }
        catch
        {
            PostToUi(() =>
            {
                if (_disposed || cancellationToken.IsCancellationRequested
                    || version != Volatile.Read(ref _previewVersion))
                    return;

                AffectedCount = 0;
                ResetBatchCommand.NotifyCanExecuteChanged();
            });
        }
    }

    private void CancelPreview() => _previewCancellation?.Cancel();

    private void CancelRun() => _runCancellation?.Cancel();

    private void RefreshIfActive()
    {
        if (!_disposed)
            RefreshPreview();
    }

    private void NotifyTargetCommands()
    {
        SetMaxIVsCommand.NotifyCanExecuteChanged();
        SetMaxEVsCommand.NotifyCanExecuteChanged();
        SetShinyCommand.NotifyCanExecuteChanged();
        HealAllCommand.NotifyCanExecuteChanged();
    }

    private bool CanCommitRun(
        string text,
        bool editBoxes,
        bool editParty,
        long stateVersion,
        int historyVersion,
        CancellationToken cancellationToken)
        => !_disposed
            && !cancellationToken.IsCancellationRequested
            && stateVersion == Volatile.Read(ref _stateVersion)
            && historyVersion == _undoRedo.ChangeCount
            && string.Equals(text, Instructions, StringComparison.Ordinal)
            && editBoxes == EditBoxes
            && editParty == EditParty;

    private void PostToUi(Action action)
    {
        if (_uiDispatcher is null || _uiDispatcher.CheckAccess())
            action();
        else
            _uiDispatcher.Post(action);
    }

    private int GetWritableTargetCount() => GetTargetSlots(EditBoxes, EditParty)
        .Count(entry => entry.Pokemon.Species != 0 && entry.Slot.CanWriteTo(_sav));

    private BatchEditPlan? BuildPlan(
        string text,
        bool editBoxes,
        bool editParty,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(text))
            return null;

        var lines = text.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries);
        if (lines.Length == 0)
            return null;

        StringInstructionSet[] sets;
        try
        {
            sets = StringInstructionSet.GetBatchSets(lines);
            foreach (var set in sets)
            {
                EntityBatchEditor.ScreenStrings(set.Filters);
                EntityBatchEditor.ScreenStrings(set.Instructions);
            }
        }
        catch
        {
            return null;
        }

        if (sets.Length == 0)
            return null;

        var editor = new EntityBatchProcessor();
        var changes = new List<PlannedChange>();
        foreach (var target in GetTargetSlots(editBoxes, editParty))
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (target.Pokemon.Species == 0 || !target.Slot.CanWriteTo(_sav))
                continue;

            var original = target.Pokemon.Clone();
            original.RefreshChecksum();
            var working = target.Pokemon.Clone();
            var modified = false;
            foreach (var set in sets)
                modified |= editor.Process(working, set.Filters, set.Instructions);

            if (modified && !working.Data.SequenceEqual(original.Data))
                changes.Add(new PlannedChange(target.Slot, working));
        }

        return new BatchEditPlan(sets, editor, changes);
    }

    private IEnumerable<SlotEntry> GetTargetSlots(bool editBoxes, bool editParty)
    {
        if (editBoxes)
        {
            for (var box = 0; box < _sav.BoxCount; box++)
            {
                for (var slot = 0; slot < _sav.BoxSlotCount; slot++)
                {
                    var info = new SlotInfoBox(box, slot, _sav);
                    yield return new SlotEntry(info, info.Read(_sav));
                }
            }
        }

        if (editParty)
        {
            for (var slot = 0; slot < _sav.PartyCount; slot++)
            {
                var info = new SlotInfoParty(slot);
                yield return new SlotEntry(info, info.Read(_sav));
            }
        }
    }

    private static IReadOnlyList<ISlotInfo> GetUndoSlots(IReadOnlyList<PlannedChange> changes)
    {
        var slots = changes
            .Where(change => change.Slot is not SlotInfoParty)
            .Select(change => change.Slot)
            .ToList();

        if (changes.Any(change => change.Slot is SlotInfoParty))
            slots.Add(new SlotInfoParty(0));

        return slots.Distinct().ToArray();
    }

    private sealed record SlotEntry(ISlotInfo Slot, PKM Pokemon);
    private sealed record PlannedChange(ISlotInfo Slot, PKM Pokemon);
    private sealed record BatchEditPlan(StringInstructionSet[] Sets, EntityBatchProcessor Editor, List<PlannedChange> Changes);

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;
        Interlocked.Increment(ref _previewVersion);
        Interlocked.Increment(ref _stateVersion);
        CancelPreview();
        CancelRun();
        _previewCancellation?.Dispose();
        _undoRedo.StateChanged -= OnUndoRedoStateChanged;
    }
}
