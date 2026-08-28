using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PKHeX.Application.Abstractions;
using PKHeX.Application.Services;
using PKHeX.Application.UseCases;
using PKHeX.Core;

namespace PKHeX.Presentation.ViewModels;

/// <summary>
/// Tool-window ViewModel for the Living Dex generator (Auto-Legality Mod Phase 2, issue #123): fills
/// boxes starting at a user-chosen box with one legal specimen of every species obtainable in the loaded
/// save's game. Generation runs off the UI thread with progress and cancellation; placement refuses
/// cleanly (no partial writes) if there is not enough contiguous empty space, and is recorded as a single
/// undoable operation via <see cref="UndoRedoService"/>.
/// </summary>
public partial class LivingDexGeneratorViewModel : ViewModelBase
{
    private readonly SaveFile _sav;
    private readonly ILivingDexService _service;
    private readonly UndoRedoService _undoRedo;
    private readonly LivingDexPlacementUseCase _placement = new();

    private CancellationTokenSource? _cts;

    /// <summary>Raised once boxes were actually written to, so the host can refresh the box/party viewers.</summary>
    public event Action? BoxesUpdated;

    [ObservableProperty] private bool _includeForms;
    [ObservableProperty] private bool _setShiny;

    [ObservableProperty] private ObservableCollection<string> _boxNames = [];
    [ObservableProperty] private int _selectedBoxIndex;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(GenerateCommand))]
    [NotifyCanExecuteChangedFor(nameof(CancelCommand))]
    private bool _isRunning;

    [ObservableProperty] private double _progress;

    [ObservableProperty] private string _statusMessage = "Choose options and a starting box, then Generate.";

    [ObservableProperty] private string _skippedSpeciesReport = string.Empty;

    public LivingDexGeneratorViewModel(SaveFile sav, ILivingDexService service, UndoRedoService undoRedo)
    {
        _sav = sav;
        _service = service;
        _undoRedo = undoRedo;

        BoxNames = new ObservableCollection<string>(Enumerable.Range(0, sav.BoxCount)
            .Select(b => sav is IBoxDetailNameRead r ? r.GetBoxName(b) : BoxDetailNameExtensions.GetDefaultBoxName(b)));
    }

    private bool CanGenerate => !IsRunning;

    [RelayCommand(CanExecute = nameof(CanGenerate))]
    private async Task GenerateAsync()
    {
        SkippedSpeciesReport = string.Empty;
        IsRunning = true;
        Progress = 0;
        StatusMessage = "Generating a legal Pokémon for every species in this game…";
        _cts = new CancellationTokenSource();

        // Set the moment placement starts writing, and cleared again only once we know it wrote
        // nothing. Anything that reaches the finally block with this still set may have left entities
        // in the boxes, so the host is told to refresh even on the failure paths (issue #262).
        var boxesMayHaveChanged = false;

        try
        {
            var options = new LivingDexOptions(IncludeForms, SetShiny);
            var progress = new Progress<LivingDexGenerationProgress>(p =>
                Progress = p.Total == 0 ? 0 : 100.0 * p.Completed / p.Total);
            var token = _cts.Token;
            var startBox = SelectedBoxIndex;

            var result = await Task.Run(() => _service.Generate(_sav, options, progress, token), token);

            if (result.Cancelled)
            {
                StatusMessage = "Generation cancelled. No changes were made.";
                return;
            }

            if (result.Pokemon.Count == 0)
            {
                StatusMessage = "The engine could not generate any legal Pokémon for this save.";
                ReportSkipped(result);
                return;
            }

            token.ThrowIfCancellationRequested();

            // Placement runs on the UI thread deliberately (issue #262). It mutates the save's box
            // buffer and closes an UndoRedoService batch whose StateChanged event drives UI-bound
            // command state; doing that from a Task.Run worker both raced the UI thread's reads of the
            // same buffer and made Avalonia throw "Call from invalid thread". Only the expensive
            // generation above is offloaded - the same split BatchEditorViewModel already uses (build
            // the plan off-thread, commit it on the UI thread). Placement is a few hundred buffer
            // writes with no legality work, so it is imperceptible next to generation.
            boxesMayHaveChanged = true;
            var placement = _placement.TryPlace(_sav, result.Pokemon, startBox, _undoRedo);
            boxesMayHaveChanged = placement.Status == LivingDexPlacementStatus.Success;

            if (placement.Status == LivingDexPlacementStatus.InsufficientSpace)
            {
                StatusMessage = $"Refused: need {placement.RequiredSlots} contiguous empty slots starting at "
                    + $"\"{BoxNames.ElementAtOrDefault(startBox) ?? $"Box {startBox + 1}"}\", but only "
                    + $"{placement.AvailableSlots} are available there. No changes were made.";
                ReportSkipped(result);
                return;
            }

            StatusMessage = $"Placed {placement.PlacedCount} legal Pokémon starting at "
                + $"\"{BoxNames.ElementAtOrDefault(startBox) ?? $"Box {startBox + 1}"}\".";
            ReportSkipped(result);
        }
        catch (OperationCanceledException)
        {
            // Only reachable before placement begins: TryPlace takes no token and cannot be cancelled.
            StatusMessage = "Generation cancelled. No changes were made.";
        }
        catch (Exception ex)
        {
            // TryPlace writes slot by slot, so a failure partway leaves entities already in the boxes.
            // Say so instead of implying nothing happened; the whole attempt is still one undo step.
            StatusMessage = boxesMayHaveChanged
                ? $"Placement failed partway through: {ex.Message} Some Pokémon were already written to the boxes - use Undo to revert them."
                : $"Unexpected error: {ex.Message}";
        }
        finally
        {
            // Exactly one refresh on every path that may have written to the boxes, success or not.
            // Before #262 this only ran on success, so a mid-placement failure left the box view
            // showing empty slots over a save that had actually been modified.
            if (boxesMayHaveChanged)
                BoxesUpdated?.Invoke();

            IsRunning = false;
            Progress = 0;
            _cts = null;
        }
    }

    private void ReportSkipped(LivingDexGenerationResult result)
    {
        if (result.SkippedSpeciesNames.Count == 0)
            return;

        SkippedSpeciesReport = $"{result.SkippedSpeciesNames.Count} species/forms could not be legalized and were skipped:\n"
            + string.Join(", ", result.SkippedSpeciesNames);
    }

    [RelayCommand(CanExecute = nameof(CanCancel))]
    private void Cancel() => _cts?.Cancel();

    private bool CanCancel() => IsRunning;
}
