using System;
using System.Collections.Generic;
using System.Linq;
using PKHeX.Core;

namespace PKHeX.Application.Services;

/// <summary>
/// Application service wrapping Core's <see cref="SlotChangelog"/>. Exposes undo/redo state via a
/// plain <see cref="StateChanged"/> event (no MVVM-framework dependency).
/// </summary>
/// <remarks>
/// Supports grouping several <see cref="AddChange"/> calls (via <see cref="BeginBatch"/>/<see cref="EndBatch"/>)
/// into one logical operation, so a single <see cref="Undo"/>/<see cref="Redo"/> call reverts/reapplies the
/// whole group atomically (e.g. a multi-slot Living Dex fill, issue #123).
///
/// Ungrouped (single) changes delegate straight to Core's <see cref="SlotChangelog"/>. Grouped changes are
/// tracked entirely on this side instead: each group entry captures its own before/after snapshot per slot
/// and is undone/redone by writing those snapshots back directly, so one <see cref="Undo"/> reverts the whole
/// group no matter how Core stacks the individual entries.
/// </remarks>
public sealed class UndoRedoService
{
    private SlotChangelog? _changelog;
    private SaveFile? _sav;
    private int _changeCount;

    private readonly Stack<UndoUnit> _undoStack = new();
    private readonly Stack<UndoUnit> _redoStack = new();
    private List<(ISlotInfo Info, PKM Before)>? _pendingBatch;

    public int ChangeCount => _changeCount;
    public bool CanUndo => _undoStack.Count != 0;
    public bool CanRedo => _redoStack.Count != 0;

    /// <summary>Raised whenever undo/redo availability may have changed.</summary>
    public event EventHandler? StateChanged;
    public event Action<ISlotInfo>? UndoPerformed;
    public event Action<ISlotInfo>? RedoPerformed;

    /// <summary>Identifies a native Core-backed batch that can be reset while it remains the latest edit.</summary>
    public sealed class BatchToken;

    private void SetChangeCount(int value)
    {
        _changeCount = value;
        StateChanged?.Invoke(this, EventArgs.Empty);
    }

    public void Initialize(SaveFile sav)
    {
        _sav = sav;
        _changelog = new SlotChangelog(sav);
        _undoStack.Clear();
        _redoStack.Clear();
        _pendingBatch = null;
        SetChangeCount(0);
    }

    public void Clear()
    {
        _sav = null;
        _changelog = null;
        _undoStack.Clear();
        _redoStack.Clear();
        _pendingBatch = null;
        SetChangeCount(0);
    }

    /// <summary>
    /// Starts grouping subsequent <see cref="AddChange"/> calls into one logical operation. Call
    /// <see cref="EndBatch"/> to close the group. Not reentrant/nestable — only one batch may be open
    /// at a time.
    /// </summary>
    public void BeginBatch() => _pendingBatch = [];

    /// <summary>
    /// Closes a batch started with <see cref="BeginBatch"/>. A no-op if no changes were added during the
    /// batch (nothing is pushed onto the undo stack).
    /// </summary>
    public void EndBatch()
    {
        var batch = _pendingBatch;
        _pendingBatch = null;
        if (batch is not { Count: > 0 } || _sav is null)
            return;

        var items = new (ISlotInfo Info, PKM Before, PKM After)[batch.Count];
        for (var i = 0; i < batch.Count; i++)
        {
            var (info, before) = batch[i];
            items[i] = (info, before, info.Read(_sav));
        }

        _undoStack.Push(new GroupUnit(items));
        _redoStack.Clear();
        SetChangeCount(_changeCount + 1);
    }

    /// <summary>
    /// Applies several slot writes as one Core-backed undo operation. If the action throws, Core rolls
    /// the save back to the state captured before the action and no history entry is created.
    /// </summary>
    public BatchToken? ApplyBatch(IEnumerable<ISlotInfo> slots, Action<IReadOnlyList<ISlotInfo>> action)
    {
        if (_sav is null || _changelog is null)
            throw new InvalidOperationException("Undo/redo has not been initialized with a save file.");

        var affected = slots.Distinct().ToArray();
        if (affected.Length == 0)
            return null;
        if (affected.Any(slot => !slot.CanWriteTo(_sav)))
            return null;

        using var change = _changelog.Begin(affected);
        action(affected);
        change.Commit();

        var token = new BatchToken();
        _undoStack.Push(new CoreBatchUnit(token));
        _redoStack.Clear();
        SetChangeCount(_changeCount + 1);
        return token;
    }

    /// <summary>Returns whether <paramref name="token"/> is still the latest undoable operation.</summary>
    public bool CanUndoBatch(BatchToken token) => _changelog?.CanUndo == true
        && _undoStack.TryPeek(out var unit)
        && unit is CoreBatchUnit batch
        && ReferenceEquals(batch.Token, token);

    /// <summary>
    /// Reverts a batch only when it is still the latest operation. The reverted operation moves to the
    /// redo stack, preserving coherent application history for the main Undo/Redo commands.
    /// </summary>
    public bool TryUndoBatch(BatchToken token)
    {
        if (!CanUndoBatch(token) || _changelog is null)
            return false;

        var unit = _undoStack.Pop();
        foreach (var info in _changelog.Undo())
            UndoPerformed?.Invoke(info);

        _redoStack.Push(unit);
        SetChangeCount(_changeCount + 1);
        return true;
    }

    public void AddChange(ISlotInfo info)
    {
        if (_pendingBatch is { } batch && _sav is not null)
        {
            // Grouped: captured entirely on this side (see class remarks) — deliberately does not touch
            // Core's SlotChangelog. EndBatch() records the group and notifies listeners.
            batch.Add((info, info.Read(_sav)));
            return;
        }

        if (_changelog is not null)
        {
            // Core's Begin() snapshots the slot's current (pre-mutation) state; Commit() pushes that snapshot
            // onto Core's undo stack and clears its redo stack. Same contract as the removed AddNewChange.
            using var change = _changelog.Begin(info);
            change.Commit();
        }

        _undoStack.Push(SingleUnit.Instance);
        _redoStack.Clear();
        SetChangeCount(_changeCount + 1);
    }

    public void Undo()
    {
        if (_sav is null || _undoStack.Count == 0) return;

        var unit = _undoStack.Pop();
        if (unit is SingleUnit or CoreBatchUnit)
        {
            if (_changelog is null || !_changelog.CanUndo) return;
            foreach (var info in _changelog.Undo())
                UndoPerformed?.Invoke(info);
        }
        else if (unit is GroupUnit group)
        {
            foreach (var item in group.Items)
            {
                item.Info.WriteTo(_sav, item.Before, EntityImportSettings.None);
                UndoPerformed?.Invoke(item.Info);
            }
        }

        _redoStack.Push(unit);
        SetChangeCount(_changeCount + 1);
    }

    public void Redo()
    {
        if (_sav is null || _redoStack.Count == 0) return;

        var unit = _redoStack.Pop();
        if (unit is SingleUnit or CoreBatchUnit)
        {
            if (_changelog is null || !_changelog.CanRedo) return;
            foreach (var info in _changelog.Redo())
                RedoPerformed?.Invoke(info);
        }
        else if (unit is GroupUnit group)
        {
            foreach (var item in group.Items)
            {
                item.Info.WriteTo(_sav, item.After, EntityImportSettings.None);
                RedoPerformed?.Invoke(item.Info);
            }
        }

        _undoStack.Push(unit);
        SetChangeCount(_changeCount + 1);
    }

    /// <summary>Common type for entries on <see cref="_undoStack"/>/<see cref="_redoStack"/>.</summary>
    private abstract class UndoUnit;

    /// <summary>Marker for a change that lives in Core's own <see cref="SlotChangelog"/> stacks.</summary>
    private sealed class SingleUnit : UndoUnit
    {
        public static readonly SingleUnit Instance = new();
        private SingleUnit() { }
    }

    /// <summary>Marker for a native Core composite change, which shares the regular undo/redo path.</summary>
    private sealed class CoreBatchUnit(BatchToken token) : UndoUnit
    {
        public BatchToken Token { get; } = token;
    }

    /// <summary>A batched change: its own independent before/after snapshot per slot (see class remarks).</summary>
    private sealed class GroupUnit((ISlotInfo Info, PKM Before, PKM After)[] items) : UndoUnit
    {
        public (ISlotInfo Info, PKM Before, PKM After)[] Items { get; } = items;
    }
}
