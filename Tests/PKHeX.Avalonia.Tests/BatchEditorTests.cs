using CommunityToolkit.Mvvm.Input;
using Moq;
using PKHeX.Application.Services;
using PKHeX.Avalonia.Services;
using PKHeX.Presentation.ViewModels;
using PKHeX.Core;
using Xunit.Abstractions;

namespace PKHeX.Avalonia.Tests;

/// <summary>
/// Behavioral tests for BatchEditorViewModel.
/// The batch editor parses text instructions and applies them to all non-empty
/// PKM in boxes/party. RunBatchAsync is async; tests await IAsyncRelayCommand.
/// </summary>
public class BatchEditorTests(ITestOutputHelper output)
{
    private static Mock<IDialogService> DialogMock() => new();

    // -----------------------------------------------------------------------
    // 1. PropertySuggestions is populated with PKM properties
    // -----------------------------------------------------------------------

    [Fact]
    public void BatchEditor_PropertySuggestions_Populated()
    {
        var sav = new SAV6XY();
        var vm = new BatchEditorViewModel(sav, DialogMock().Object);

        Assert.NotEmpty(vm.PropertySuggestions);
        Assert.Contains("Species",      vm.PropertySuggestions);
        Assert.Contains("Nickname",     vm.PropertySuggestions);
        Assert.Contains("CurrentLevel", vm.PropertySuggestions);
        output.WriteLine($"PropertySuggestions: {vm.PropertySuggestions.Count} entries ✓");
    }

    // -----------------------------------------------------------------------
    // 2. Priority properties appear first in PropertySuggestions
    // -----------------------------------------------------------------------

    [Fact]
    public void BatchEditor_PropertySuggestions_PriorityFirst()
    {
        var sav = new SAV6XY();
        var vm = new BatchEditorViewModel(sav, DialogMock().Object);

        // "Species" is first in the priority list
        Assert.Equal("Species", vm.PropertySuggestions[0]);
        output.WriteLine("PropertySuggestions: Species is first ✓");
    }

    // -----------------------------------------------------------------------
    // 3. AddInstruction builds correct instruction string
    // -----------------------------------------------------------------------

    [Fact]
    public void BatchEditor_AddInstruction_BuildsCorrectString()
    {
        var sav = new SAV6XY();
        var vm = new BatchEditorViewModel(sav, DialogMock().Object);

        vm.SelectedProperty = "CurrentLevel";
        vm.SelectedOperator = "=";
        vm.SelectedValue    = "50";

        vm.AddInstructionCommand.Execute(null);

        Assert.Equal(".CurrentLevel=50", vm.Instructions);
        output.WriteLine($"AddInstruction: '{vm.Instructions}' ✓");
    }

    // -----------------------------------------------------------------------
    // 4. Multiple AddInstruction calls join with newlines
    // -----------------------------------------------------------------------

    [Fact]
    public void BatchEditor_AddInstruction_MultipleLines_JoinedWithNewline()
    {
        var sav = new SAV6XY();
        var vm = new BatchEditorViewModel(sav, DialogMock().Object);

        vm.SelectedProperty = "CurrentLevel";
        vm.SelectedOperator = "=";
        vm.SelectedValue    = "50";
        vm.AddInstructionCommand.Execute(null);

        vm.SelectedProperty = "IsShiny";
        vm.SelectedOperator = "=";
        vm.SelectedValue    = "true";
        vm.AddInstructionCommand.Execute(null);

        var lines = vm.Instructions.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries);
        Assert.Equal(2, lines.Length);
        Assert.Equal(".CurrentLevel=50", lines[0]);
        Assert.Equal(".IsShiny=true", lines[1]);
        output.WriteLine($"AddInstruction x2: '{vm.Instructions.Replace(Environment.NewLine, "|")}' ✓");
    }

    // -----------------------------------------------------------------------
    // 5. AddFilter builds correct filter string (= prefix)
    // -----------------------------------------------------------------------

    [Fact]
    public void BatchEditor_AddFilter_BuildsCorrectString()
    {
        var sav = new SAV6XY();
        var vm = new BatchEditorViewModel(sav, DialogMock().Object);

        vm.SelectedProperty = "Species";
        vm.SelectedOperator = "=";
        vm.SelectedValue    = "25";

        vm.AddFilterCommand.Execute(null);

        Assert.Equal("=Species=25", vm.Instructions);
        output.WriteLine($"AddFilter: '{vm.Instructions}' ✓");
    }

    // -----------------------------------------------------------------------
    // 6. ClearInstructions empties both Instructions and Results
    // -----------------------------------------------------------------------

    [Fact]
    public void BatchEditor_ClearInstructions_ClearsAll()
    {
        var sav = new SAV6XY();
        var vm = new BatchEditorViewModel(sav, DialogMock().Object);

        vm.Instructions = ".Level=50";
        vm.Results = "some results";

        vm.ClearInstructionsCommand.Execute(null);

        Assert.Empty(vm.Instructions);
        Assert.Empty(vm.Results);
        output.WriteLine("ClearInstructions: Instructions and Results cleared ✓");
    }

    // -----------------------------------------------------------------------
    // 7. RunBatch with empty instructions is gated off
    // -----------------------------------------------------------------------

    [Fact]
    public async Task BatchEditor_RunBatch_EmptyInstructions_IsDisabled()
    {
        var sav = new SAV6XY();
        var vm = new BatchEditorViewModel(sav, DialogMock().Object);

        vm.Instructions = string.Empty;
        await ((IAsyncRelayCommand)vm.RunBatchCommand).ExecuteAsync(null);

        Assert.False(vm.RunBatchCommand.CanExecute(null));
        Assert.Empty(vm.Results);
        output.WriteLine("RunBatch(empty): command disabled ✓");
    }

    // -----------------------------------------------------------------------
    // 8. RunBatch processes PKM in boxes (Gen6)
    // -----------------------------------------------------------------------

    [Fact]
    public async Task BatchEditor_RunBatch_ModifiesBoxPokemon()
    {
        var sav = new SAV6XY();

        // Inject a PK6 into box 0
        var pk = new PK6 { Species = 1 }; pk.CurrentLevel = 5;
        sav.SetBoxSlotAtIndex(pk, 0);

        var vm = new BatchEditorViewModel(sav, DialogMock().Object);
        vm.EditBoxes = true;
        vm.EditParty = false;
        vm.Instructions = ".CurrentLevel=50";
        await WaitForAsync(() => vm.AffectedCount == 1);

        await ((IAsyncRelayCommand)vm.RunBatchCommand).ExecuteAsync(null);

        var resultPk = sav.GetBoxSlotAtIndex(0, 0);
        Assert.Equal(50, resultPk.CurrentLevel);
        Assert.False(string.IsNullOrEmpty(vm.Results));
        output.WriteLine($"RunBatch: Bulbasaur Level 5→50 ✓ Results='{vm.Results}'");
    }

    // -----------------------------------------------------------------------
    // 9. BatchEditCompleted event fires after successful batch
    // -----------------------------------------------------------------------

    [Fact]
    public async Task BatchEditor_RunBatch_FiresBatchEditCompleted()
    {
        var sav = new SAV6XY();
        var pk = new PK6 { Species = 25 }; pk.CurrentLevel = 5;
        sav.SetBoxSlotAtIndex(pk, 0);

        var vm = new BatchEditorViewModel(sav, DialogMock().Object);
        vm.Instructions = ".Nickname=Pika";
        await WaitForAsync(() => vm.AffectedCount == 1);

        bool eventFired = false;
        vm.BatchEditCompleted += () => eventFired = true;

        await ((IAsyncRelayCommand)vm.RunBatchCommand).ExecuteAsync(null);

        Assert.True(eventFired, "BatchEditCompleted should fire after successful batch");
        output.WriteLine("BatchEditCompleted event fired ✓");
    }

    [Fact]
    public async Task BatchEditor_AffectedCount_TracksCurrentInstructionsAndTargets()
    {
        var sav = new SAV6XY();
        sav.SetBoxSlotAtIndex(new PK6 { Species = 1, CurrentLevel = 5 }, 0, 0);
        sav.SetBoxSlotAtIndex(new PK6 { Species = 25, CurrentLevel = 5 }, 0, 1);

        var vm = new BatchEditorViewModel(sav, DialogMock().Object);
        vm.Instructions = "=Species=1" + Environment.NewLine + ".CurrentLevel=50";
        await WaitForAsync(() => vm.AffectedCount == 1);

        Assert.Equal(1, vm.AffectedCount);
        Assert.True(vm.RunBatchCommand.CanExecute(null));

        vm.Instructions = "=Species=999" + Environment.NewLine + ".CurrentLevel=50";
        await WaitForAsync(() => vm.AffectedCount == 0);

        Assert.Equal(0, vm.AffectedCount);
        Assert.False(vm.RunBatchCommand.CanExecute(null));

        vm.EditBoxes = false;
        vm.EditParty = true;
        await WaitForAsync(() => vm.AffectedCount == 0);

        Assert.Equal(0, vm.AffectedCount);
        Assert.False(vm.RunBatchCommand.CanExecute(null));
    }

    [Fact]
    public void BatchEditor_QuickActions_AreGatedByTheirOwnAffectedCount()
    {
        var sav = new SAV6XY();
        sav.SetBoxSlotAtIndex(new PK6 { Species = 1 }, 0, 0);
        var vm = new BatchEditorViewModel(sav, DialogMock().Object);

        Assert.True(vm.SetShinyCommand.CanExecute(null));

        vm.EditBoxes = false;
        vm.EditParty = false;

        Assert.False(vm.SetShinyCommand.CanExecute(null));
    }

    [Fact]
    public async Task BatchEditor_RunBatch_IsOneUndoableOperation()
    {
        var sav = new SAV6XY();
        sav.SetBoxSlotAtIndex(new PK6 { Species = 1, CurrentLevel = 5 }, 0, 0);
        sav.SetBoxSlotAtIndex(new PK6 { Species = 25, CurrentLevel = 5 }, 0, 1);
        var undoRedo = new UndoRedoService();
        undoRedo.Initialize(sav);
        var vm = new BatchEditorViewModel(sav, DialogMock().Object, undoRedo)
        {
            Instructions = ".CurrentLevel=50",
        };
        await WaitForAsync(() => vm.AffectedCount == 2);

        await ((IAsyncRelayCommand)vm.RunBatchCommand).ExecuteAsync(null);

        Assert.Equal(50, sav.GetBoxSlotAtIndex(0, 0).CurrentLevel);
        Assert.Equal(50, sav.GetBoxSlotAtIndex(0, 1).CurrentLevel);
        Assert.True(undoRedo.CanUndo);

        undoRedo.Undo();

        Assert.Equal(5, sav.GetBoxSlotAtIndex(0, 0).CurrentLevel);
        Assert.Equal(5, sav.GetBoxSlotAtIndex(0, 1).CurrentLevel);
        Assert.False(undoRedo.CanUndo);
    }

    [Fact]
    public async Task BatchEditor_ResetRestoresStateAndDiscardsBatchUndo()
    {
        var sav = new SAV6XY();
        sav.SetBoxSlotAtIndex(new PK6 { Species = 1, CurrentLevel = 5 }, 0, 0);
        var undoRedo = new UndoRedoService();
        undoRedo.Initialize(sav);
        var vm = new BatchEditorViewModel(sav, DialogMock().Object, undoRedo)
        {
            Instructions = ".CurrentLevel=50",
        };
        await WaitForAsync(() => vm.AffectedCount == 1);

        await ((IAsyncRelayCommand)vm.RunBatchCommand).ExecuteAsync(null);
        vm.ResetBatchCommand.Execute(null);

        Assert.Equal(5, sav.GetBoxSlotAtIndex(0, 0).CurrentLevel);
        Assert.False(undoRedo.CanUndo);
    }

    [Fact]
    public async Task BatchEditor_SharedUndoRedo_RefreshesOtherEditorPreview()
    {
        var sav = new SAV6XY();
        sav.SetBoxSlotAtIndex(new PK6 { Species = 1, CurrentLevel = 5 }, 0, 0);
        var undoRedo = new UndoRedoService();
        undoRedo.Initialize(sav);
        var vm1 = new BatchEditorViewModel(sav, DialogMock().Object, undoRedo)
        {
            Instructions = "=CurrentLevel=5" + Environment.NewLine + ".CurrentLevel=50",
        };
        var vm2 = new BatchEditorViewModel(sav, DialogMock().Object, undoRedo)
        {
            Instructions = "=CurrentLevel=5" + Environment.NewLine + ".CurrentLevel=50",
        };

        await WaitForAsync(() => vm1.AffectedCount == 1 && vm2.AffectedCount == 1);
        await ((IAsyncRelayCommand)vm1.RunBatchCommand).ExecuteAsync(null);
        Assert.Equal(50, sav.GetBoxSlotAtIndex(0, 0).CurrentLevel);
        await WaitForAsync(() => vm2.AffectedCount == 0);

        undoRedo.Undo();
        await WaitForAsync(() => vm2.AffectedCount == 1);
        undoRedo.Redo();
        await WaitForAsync(() => vm2.AffectedCount == 0);
    }

    [Fact]
    public async Task BatchEditor_Preview_UsesLatestInstructions()
    {
        var sav = new SAV6XY();
        sav.SetBoxSlotAtIndex(new PK6 { Species = 1, CurrentLevel = 5 }, 0, 0);
        sav.SetBoxSlotAtIndex(new PK6 { Species = 25, CurrentLevel = 5 }, 0, 1);
        var vm = new BatchEditorViewModel(sav, DialogMock().Object);

        vm.Instructions = ".CurrentLevel=50";
        await WaitForAsync(() => vm.AffectedCount == 2);
        vm.Instructions = "=Species=999" + Environment.NewLine + ".CurrentLevel=50";
        await WaitForAsync(() => vm.AffectedCount == 0);
    }

    [Fact]
    public async Task BatchEditor_RefreshExternalState_UpdatesPreviewAndQuickActions()
    {
        var sav = new SAV6XY();
        sav.SetBoxSlotAtIndex(new PK6 { Species = 1, CurrentLevel = 5 }, 0, 0);
        var vm = new BatchEditorViewModel(sav, DialogMock().Object)
        {
            Instructions = "=Species=1" + Environment.NewLine + ".CurrentLevel=50",
        };
        await WaitForAsync(() => vm.AffectedCount == 1);

        var commandNotifications = 0;
        vm.SetShinyCommand.CanExecuteChanged += (_, _) => commandNotifications++;
        sav.SetBoxSlotAtIndex(sav.BlankPKM, 0, 0);
        vm.RefreshExternalState();

        await WaitForAsync(() => vm.AffectedCount == 0);
        Assert.True(commandNotifications > 0);
        Assert.False(vm.SetShinyCommand.CanExecute(null));
    }

    [Fact]
    public void BatchEditor_InstructionBuilderCommands_RequireAProperty()
    {
        var sav = new SAV6XY();
        var vm = new BatchEditorViewModel(sav, DialogMock().Object);

        Assert.False(vm.AddFilterCommand.CanExecute(null));
        Assert.False(vm.AddInstructionCommand.CanExecute(null));

        vm.SelectedProperty = "Species";
        Assert.True(vm.AddFilterCommand.CanExecute(null));
        Assert.True(vm.AddInstructionCommand.CanExecute(null));

        vm.SelectedProperty = string.Empty;
        Assert.False(vm.AddFilterCommand.CanExecute(null));
        Assert.False(vm.AddInstructionCommand.CanExecute(null));
    }

    // -----------------------------------------------------------------------
    // 10. Operators list contains expected operators
    // -----------------------------------------------------------------------

    [Fact]
    public void BatchEditor_Operators_ContainsExpected()
    {
        var sav = new SAV6XY();
        var vm = new BatchEditorViewModel(sav, DialogMock().Object);

        Assert.Contains("=", vm.Operators);
        Assert.Contains("!", vm.Operators);
        output.WriteLine($"Operators: [{string.Join(", ", vm.Operators)}] ✓");
    }

    private static async Task WaitForAsync(Func<bool> condition)
    {
        var deadline = DateTime.UtcNow.AddSeconds(5);
        while (!condition())
        {
            if (DateTime.UtcNow >= deadline)
                throw new TimeoutException("The batch editor preview did not reach the expected state.");

            await Task.Delay(20);
        }
    }
}
