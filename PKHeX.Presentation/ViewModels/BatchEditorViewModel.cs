using System.Reflection;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PKHeX.Application.Services;
using PKHeX.Core;

namespace PKHeX.Presentation.ViewModels;

public partial class BatchEditorViewModel : ViewModelBase
{
    private readonly SaveFile _sav;
    private readonly IDialogService _dialogService;
    private readonly UndoRedoService _undoRedo;
    private readonly List<UndoRedoService.BatchToken> _batchTokens = [];

    public event Action? BatchEditCompleted;

    public BatchEditorViewModel(SaveFile sav, IDialogService dialogService, UndoRedoService? undoRedo = null)
    {
        _sav = sav;
        _dialogService = dialogService;
        _undoRedo = undoRedo ?? new UndoRedoService();
        if (undoRedo is null)
            _undoRedo.Initialize(sav);

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
    private string _selectedProperty = string.Empty;

    [ObservableProperty]
    private string _selectedOperator = "=";

    [ObservableProperty]
    private string _selectedValue = string.Empty;

    public IReadOnlyList<string> Operators { get; } = ["=", "!", ".", "=RNG", "=POKEMON"];

    partial void OnInstructionsChanged(string value) => RefreshPreview();
    partial void OnEditBoxesChanged(bool value) => RefreshPreview();
    partial void OnEditPartyChanged(bool value) => RefreshPreview();

    [RelayCommand]
    private void AddInstruction()
    {
        if (string.IsNullOrWhiteSpace(SelectedProperty))
            return;

        var instruction = $".{SelectedProperty}{SelectedOperator}{SelectedValue}";

        if (!string.IsNullOrEmpty(Instructions))
            Instructions += Environment.NewLine;
        Instructions += instruction;
    }

    [RelayCommand]
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

        IsRunning = true;
        try
        {
            var plan = BuildPlan(Instructions);
            if (plan is null || plan.Changes.Count == 0)
                return;

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
                return;

            _batchTokens.Add(token);
            BatchEditCompleted?.Invoke();
            RefreshPreview();
        }
        catch (Exception ex)
        {
            Results = $"Error: {ex.Message}";
        }
        finally
        {
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
        await RunBatchAsync();
    }

    [RelayCommand(CanExecute = nameof(CanRunQuickAction))]
    private async Task SetMaxEVs()
    {
        Instructions = ".EVs=$suggestPokemon MaxEVs($0)";
        await RunBatchAsync();
    }

    [RelayCommand(CanExecute = nameof(CanRunQuickAction))]
    private async Task SetShiny()
    {
        Instructions = ".Shiny=Star";
        await RunBatchAsync();
    }

    [RelayCommand(CanExecute = nameof(CanRunQuickAction))]
    private async Task HealAll()
    {
        Instructions = ".Heal";
        await RunBatchAsync();
    }

    private bool CanRunBatch() => !IsRunning && AffectedCount > 0;

    private bool CanRunQuickAction() => !IsRunning && GetWritableTargetCount() > 0;

    private bool CanResetBatch() => _batchTokens.Count != 0 && _undoRedo.CanUndoBatch(_batchTokens[^1]);

    private void RefreshPreview()
    {
        try
        {
            AffectedCount = BuildPlan(Instructions)?.Changes.Count ?? 0;
        }
        catch
        {
            AffectedCount = 0;
        }
    }

    private int GetWritableTargetCount() => GetTargetSlots().Count(entry => entry.Pokemon.Species != 0 && entry.Slot.CanWriteTo(_sav));

    private BatchEditPlan? BuildPlan(string text)
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
        foreach (var target in GetTargetSlots())
        {
            if (target.Pokemon.Species == 0 || !target.Slot.CanWriteTo(_sav))
                continue;

            var working = target.Pokemon.Clone();
            var modified = false;
            foreach (var set in sets)
                modified |= editor.Process(working, set.Filters, set.Instructions);

            if (modified)
                changes.Add(new PlannedChange(target.Slot, working));
        }

        return new BatchEditPlan(sets, editor, changes);
    }

    private IEnumerable<SlotEntry> GetTargetSlots()
    {
        if (EditBoxes)
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

        if (EditParty)
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
}
