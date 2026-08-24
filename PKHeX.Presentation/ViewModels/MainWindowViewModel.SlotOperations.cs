using PKHeX.Core;

namespace PKHeX.Presentation.ViewModels;

public partial class MainWindowViewModel
{
    private void OnBoxSlotActivated(int box, int slot) => OnBoxViewSlot(box, slot);
    private void OnPartySlotActivated(int slot) => OnPartyViewSlot(slot);

    private void OnViewRequested(SlotLocation location)
    {
        if (location.IsParty) OnPartyViewSlot(location.Slot);
        else OnBoxViewSlot(location.Box, location.Slot);
    }

    private void OnSetRequested(SlotLocation location)
    {
        if (location.IsParty) OnPartySetSlot(location.Slot);
        else OnBoxSetSlot(location.Box, location.Slot);
    }

    private void OnDeleteRequested(SlotLocation location)
    {
        if (location.IsParty) OnPartyDeleteSlot(location.Slot);
        else OnBoxDeleteSlot(location.Box, location.Slot);
    }

    private async void OnMoveRequested(SlotLocation source, SlotLocation destination, bool clone)
    {
        if (CurrentSave is null || source.Equals(destination))
            return;

        if (!IsValidSlot(CurrentSave, source) || !IsValidSlot(CurrentSave, destination))
            return;

        // A party has no sparse slots: moving a member into an empty box slot must compact the
        // remaining party, otherwise SaveFile.SetPartySlotAtIndex(blank, index) lowers PartyCount
        // and hides every member after the removed one.
        var sav = CurrentSave;
        var sourceInfo = CreateSlotInfo(sav, source);
        var destinationInfo = CreateSlotInfo(sav, destination);

        var pkSource = ReadSlot(sav, source);

        if (pkSource.Species == 0)
            return;

        var pkDest = ReadSlot(sav, destination);

        // Do not start a Core capture unless both slots are writable and every entity that will be
        // written is accepted by its destination. ApplyBatch then guarantees that a later write
        // failure restores the complete pre-operation state and creates no history entry.
        if (!sourceInfo.CanWriteTo(sav) || !destinationInfo.CanWriteTo(sav))
            return;
        if (destinationInfo.CanWriteTo(sav, pkSource) != WriteBlockedMessage.None)
            return;
        if (!clone && !(source.IsParty && !destination.IsParty && pkDest.Species == 0)
            && sourceInfo.CanWriteTo(sav, pkDest) != WriteBlockedMessage.None)
            return;

        // Copying onto an occupied destination discards the destination entity. Moves/swaps keep
        // both entities, and copies/moves into empty slots are safe, so only this destructive case
        // asks for confirmation. Re-check the save identity after awaiting in case the user opened
        // another save while a native dialog was visible.
        if (clone && pkDest.Species != 0)
        {
            var confirmed = await _dialogService.ShowConfirmationAsync(
                T("Slot_OverwriteTitle"),
                T("Slot_OverwriteMessage"),
                T("Common_OK"),
                T("Common_Cancel"));
            if (!confirmed || !ReferenceEquals(CurrentSave, sav))
                return;
        }

        try
        {
            var token = _undoRedo.ApplyBatch([sourceInfo, destinationInfo], _ =>
            {
                if (clone)
                {
                    WriteSlot(sav, destination, pkSource.Clone());
                    return;
                }

                if (source.IsParty && !destination.IsParty && pkDest.Species == 0)
                {
                    // Preserve the established party-compaction behavior. A blank party write is
                    // not equivalent for every save format: DeletePartySlot removes the member and
                    // shifts the remaining party entries left.
                    WriteSlot(sav, destination, pkSource.Clone());
                    sav.DeletePartySlot(source.Slot);
                    return;
                }

                WriteSlot(sav, source, pkDest.Clone());
                WriteSlot(sav, destination, pkSource.Clone());
            });

            if (token is null)
                return;
        }
        catch (Exception ex)
        {
            // ApplyBatch disposes its Core Change before the exception reaches this handler, so a
            // partial source/destination write is rolled back and cannot duplicate or lose data.
            System.Diagnostics.Trace.TraceWarning($"Atomic slot move failed: {ex.Message}");
            return;
        }

        BoxViewer?.RefreshCurrentBox();
        PartyViewer?.RefreshParty();
        BatchEditor?.RefreshExternalState();
    }

    private static bool IsValidSlot(SaveFile sav, SlotLocation location) => location.IsParty
        ? sav.HasParty && location.Slot >= 0 && location.Slot < 6
        : sav.HasBox
          && location.Box >= 0 && location.Box < sav.BoxCount
          && location.Slot >= 0 && location.Slot < sav.BoxSlotCount;

    private static ISlotInfo CreateSlotInfo(SaveFile sav, SlotLocation location) => location.IsParty
        ? new SlotInfoParty(location.Slot)
        : new SlotInfoBox(location.Box, location.Slot, sav);

    private static PKM ReadSlot(SaveFile sav, SlotLocation location) => location.IsParty
        ? sav.GetPartySlotAtIndex(location.Slot)
        : sav.GetBoxSlotAtIndex(location.Box, location.Slot);

    private static void WriteSlot(SaveFile sav, SlotLocation location, PKM pk)
    {
        if (location.IsParty)
            sav.SetPartySlotAtIndex(pk, location.Slot);
        else
            sav.SetBoxSlotAtIndex(pk, location.Box, location.Slot);
    }

    private void OnBoxViewSlot(int box, int slot)
    {
        if (CurrentSave is null || CurrentPokemonEditor is null) return;
        var pk = CurrentSave.GetBoxSlotAtIndex(box, slot);
        if (pk.Species != 0) CurrentPokemonEditor.LoadPKM(pk);
    }

    private void OnBoxSetSlot(int box, int slot)
    {
        if (CurrentSave is null || CurrentPokemonEditor is null) return;
        var prepared = CurrentPokemonEditor.PreparePKM();
        if (!TryGetCompatible(prepared, out var pk))
        {
            _ = _dialogService.ShowErrorAsync(T("Slot_IncompatibleFormatTitle"), T("Slot_IncompatibleFormatMessage"));
            return;
        }
        CurrentSave.SetBoxSlotAtIndex(pk, box, slot);
        BoxViewer?.RefreshCurrentBox();
        BatchEditor?.RefreshExternalState();
    }

    private void OnBoxDeleteSlot(int box, int slot)
    {
        if (CurrentSave is null) return;
        if (CurrentSave.GetBoxSlotAtIndex(box, slot).Species == 0) return;
        CurrentSave.SetBoxSlotAtIndex(CurrentSave.BlankPKM, box, slot);
        BoxViewer?.RefreshCurrentBox();
        BatchEditor?.RefreshExternalState();
    }

    private void OnPartyViewSlot(int slot)
    {
        if (CurrentSave is null || CurrentPokemonEditor is null) return;
        var pk = CurrentSave.GetPartySlotAtIndex(slot);
        if (pk.Species != 0) CurrentPokemonEditor.LoadPKM(pk);
    }

    private void OnPartySetSlot(int slot)
    {
        if (CurrentSave is null || PartyViewer is null || CurrentPokemonEditor is null) return;
        var prepared = CurrentPokemonEditor.PreparePKM();
        if (!TryGetCompatible(prepared, out var pk))
        {
            _ = _dialogService.ShowErrorAsync(T("Slot_IncompatibleFormatTitle"), T("Slot_IncompatibleFormatMessage"));
            return;
        }
        CurrentSave.SetPartySlotAtIndex(pk, slot);
        PartyViewer.RefreshParty();
        BatchEditor?.RefreshExternalState();
    }

    /// <summary>
    /// Ensures <paramref name="pk"/> is a type the currently open save file accepts, converting
    /// cross-format entities (e.g. a PK3 loaded via the Encounter Database into a Gen-4 save) when
    /// possible. Returns false if no compatible conversion exists, so the caller can show an error
    /// instead of letting <see cref="SaveFile.SetBoxSlotAtIndex"/>/<see cref="SaveFile.SetPartySlotAtIndex"/>
    /// throw on a format mismatch (see GitHub issue #163).
    /// </summary>
    private bool TryGetCompatible(PKM pk, out PKM result)
    {
        if (CurrentSave is null || pk.GetType() == CurrentSave.PKMType)
        {
            result = pk;
            return true;
        }

        var converted = CurrentSave.GetCompatiblePKM(pk);
        if (converted.Species == 0 && pk.Species != 0)
        {
            result = pk;
            return false;
        }

        result = converted;
        return true;
    }

    private void OnPartyDeleteSlot(int slot)
    {
        if (CurrentSave is null || PartyViewer is null) return;
        _ = _dialogService.ShowErrorAsync(T("Common_Delete"), T("Msg_CannotDeletePartyPokemon"));
    }

    private void OnBatchEditCompleted()
    {
        BoxViewer?.RefreshCurrentBox();
        PartyViewer?.RefreshParty();
    }

    private void OnUndoRedoPerformed(ISlotInfo info)
    {
        if (info is SlotInfoBox) BoxViewer?.RefreshCurrentBox();
        else if (info is SlotInfoParty) PartyViewer?.RefreshParty();
    }
}
