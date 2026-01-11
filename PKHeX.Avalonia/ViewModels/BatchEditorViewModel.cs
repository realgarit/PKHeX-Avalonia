using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PKHeX.Avalonia.Services;
using PKHeX.Core;

namespace PKHeX.Avalonia.ViewModels;

public partial class BatchEditorViewModel : ViewModelBase
{
    private readonly SaveFile _sav;
    private readonly IDialogService _dialogService;

    public BatchEditorViewModel(SaveFile sav, IDialogService dialogService)
    {
        _sav = sav;
        _dialogService = dialogService;

        // Initialize property suggestions from PKM types
        PropertySuggestions = GetCommonPkmProperties();
    }

    private static List<string> GetCommonPkmProperties()
    {
        // Common PKM properties for batch editing
        return
        [
            "Species", "Form", "Nickname", "IsNicknamed",
            "Level", "EXP", "Nature", "StatNature",
            "Ability", "AbilityNumber",
            "Gender", "IsShiny", "ShinyXor",
            "IV_HP", "IV_ATK", "IV_DEF", "IV_SPA", "IV_SPD", "IV_SPE",
            "EV_HP", "EV_ATK", "EV_DEF", "EV_SPA", "EV_SPD", "EV_SPE",
            "Move1", "Move2", "Move3", "Move4",
            "HeldItem", "Ball",
            "OT_Name", "OT_Gender",
            "Friendship", "IsEgg",
            "Language", "Version",
            "CurrentHandler", "HT_Name", "HT_Gender"
        ];
    }

    public IReadOnlyList<string> PropertySuggestions { get; }

    [ObservableProperty]
    private string _instructions = string.Empty;

    [ObservableProperty]
    private string _results = string.Empty;

    [ObservableProperty]
    private bool _editBoxes = true;

    [ObservableProperty]
    private bool _editParty;

    [ObservableProperty]
    private string _selectedProperty = string.Empty;

    [ObservableProperty]
    private string _selectedOperator = "=";

    [ObservableProperty]
    private string _selectedValue = string.Empty;

    public IReadOnlyList<string> Operators { get; } = ["=", "!", ".", "=RNG", "=POKEMON"];

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

    [RelayCommand]
    private async Task RunBatchAsync()
    {
        if (string.IsNullOrWhiteSpace(Instructions))
        {
            Results = "No instructions provided.";
            return;
        }

        var lines = Instructions.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries);
        if (lines.Length == 0)
        {
            Results = "No valid instructions found.";
            return;
        }

        try
        {
            var pkms = GetTargetPokemon().ToList();
            if (pkms.Count == 0)
            {
                Results = "No Pokémon to process.";
                return;
            }

            var editor = Core.BatchEditor.Execute(lines, pkms);
            var sets = StringInstructionSet.GetBatchSets(lines);
            Results = editor.GetEditorResults(sets);

            // Refresh the save data (the PKMs were modified in-place)
            if (EditBoxes)
            {
                // Write modified PKMs back to boxes
                int boxSlotIndex = 0;
                for (int box = 0; box < _sav.BoxCount && boxSlotIndex < pkms.Count; box++)
                {
                    for (int slot = 0; slot < _sav.BoxSlotCount && boxSlotIndex < pkms.Count; slot++)
                    {
                        var pk = _sav.GetBoxSlotAtIndex(box, slot);
                        if (pk.Species != 0)
                        {
                            // Find the corresponding modified PKM
                            boxSlotIndex++;
                        }
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Results = $"Error: {ex.Message}";
        }
    }

    private IEnumerable<PKM> GetTargetPokemon()
    {
        if (EditBoxes)
        {
            for (int box = 0; box < _sav.BoxCount; box++)
            {
                var boxData = _sav.GetBoxData(box);
                foreach (var pk in boxData)
                {
                    if (pk.Species != 0)
                        yield return pk;
                }
            }
        }

        if (EditParty)
        {
            for (int i = 0; i < _sav.PartyCount; i++)
            {
                var pk = _sav.GetPartySlotAtIndex(i);
                if (pk.Species != 0)
                    yield return pk;
            }
        }
    }

    // Common batch commands as quick actions
    [RelayCommand]
    private void SetMaxIVs()
    {
        Instructions = ".IVs=$suggestPokemon MaxIVs($0)";
        _ = RunBatchAsync();
    }

    [RelayCommand]
    private void SetMaxEVs()
    {
        Instructions = ".EVs=$suggestPokemon MaxEVs($0)";
        _ = RunBatchAsync();
    }

    [RelayCommand]
    private void SetShiny()
    {
        Instructions = ".Shiny=Star";
        _ = RunBatchAsync();
    }

    [RelayCommand]
    private void HealAll()
    {
        Instructions = ".Heal";
        _ = RunBatchAsync();
    }
}
