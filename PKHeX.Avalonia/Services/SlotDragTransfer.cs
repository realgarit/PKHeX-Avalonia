using System;
using System.IO;
using Avalonia.Input;
using Avalonia.Platform.Storage;
using PKHeX.Application.Abstractions;
using PKHeX.Application.UseCases;
using PKHeX.Core;
using PKHeX.Presentation.Models;

namespace PKHeX.Avalonia.Services;

/// <summary>
/// Builds and reads the <see cref="DataTransfer"/> payload used for Pokémon slot
/// drag-and-drop. Avalonia 11.3's data-transfer model only supports byte/string
/// application formats (it no longer carries arbitrary CLR objects), so the
/// <see cref="SlotLocation"/> is serialized to a compact string.
/// </summary>
internal static class SlotDragTransfer
{
    private static readonly DataFormat<string> Format =
        DataFormat.CreateStringApplicationFormat("PKHeX.SlotDragData");

    // The Pokémon editor sprite is a drag source too. Its payload only names the save session:
    // the drop target asks the host to Set the slot from the editor, the same as Shift+Click.
    private static readonly DataFormat<string> EditorFormat =
        DataFormat.CreateStringApplicationFormat("PKHeX.EditorDragData");

    /// <summary>Creates the drag payload for the given slot data.</summary>
    public static DataTransfer Create(SlotDragData data)
    {
        var transfer = new DataTransfer();
        transfer.Add(DataTransferItem.Create(Format, Serialize(data)));
        return transfer;
    }

    /// <summary>
    /// Creates the drag payload for the given slot, additionally attaching a decrypted entity
    /// file (e.g. ".pk9") so the OS receives a real file when the slot is dragged out to the
    /// desktop/Finder/Explorer.
    ///
    /// This is intentionally synchronous: on macOS, <c>DragDrop.DoDragDropAsync</c> must be
    /// invoked from within the live pointer-moved frame with no prior <c>await</c>, otherwise
    /// AppKit's <c>[NSApp currentEvent]</c> is no longer the originating mouse-down event and the
    /// native drag session silently fails to start. The temp file write is a few hundred bytes
    /// (fine as a blocking call), and <see cref="IStorageProvider.TryGetFileFromPathAsync"/> is
    /// only consulted if it happens to complete synchronously; otherwise the OS file attachment
    /// is skipped and the payload degrades gracefully to an in-app-only drag (no exception).
    /// </summary>
    public static DataTransfer Create(SlotDragData data, PKM? pk, IStorageProvider? storageProvider)
    {
        var transfer = Create(data);
        AttachEntityFile(transfer, pk, storageProvider);
        return transfer;
    }

    /// <summary>
    /// Creates the drag payload for the Pokémon editor sprite. Like the slot payload, it also
    /// carries the exported entity file when one can be attached synchronously, so the editor
    /// Pokémon can be dragged out to the desktop.
    /// </summary>
    public static DataTransfer CreateEditor(Guid sessionId, PKM? pk, IStorageProvider? storageProvider)
    {
        var transfer = new DataTransfer();
        transfer.Add(DataTransferItem.Create(EditorFormat, sessionId.ToString("N")));
        AttachEntityFile(transfer, pk, storageProvider);
        return transfer;
    }

    /// <summary>
    /// Returns whether the transfer is an editor-sprite drag from the supplied save session.
    /// An empty session never matches, so a drag started without a loaded save does nothing.
    /// </summary>
    public static bool IsEditorPayload(IDataTransfer? transfer, Guid expectedSessionId)
        => expectedSessionId != Guid.Empty
            && transfer?.TryGetValue(EditorFormat) is { } raw
            && Guid.TryParseExact(raw, "N", out var sessionId)
            && sessionId == expectedSessionId;

    /// <summary>Returns whether a transfer contains an editor-sprite payload, even if its session is stale.</summary>
    public static bool HasEditorPayload(IDataTransfer? transfer) => transfer?.TryGetValue(EditorFormat) is not null;

    private static void AttachEntityFile(DataTransfer transfer, PKM? pk, IStorageProvider? storageProvider)
    {
        if (pk is null || storageProvider is null)
            return;

        var exported = new ExportEntityToFileUseCase().Execute(pk);
        if (exported is not { } file)
            return;

        try
        {
            var tempPath = Path.Combine(Path.GetTempPath(), file.FileName);
            File.WriteAllBytes(tempPath, file.Data);

            var fileTask = storageProvider.TryGetFileFromPathAsync(new Uri(tempPath));
            var storageFile = fileTask.IsCompletedSuccessfully ? fileTask.Result : null;
            if (storageFile is not null)
                transfer.Add(DataTransferItem.CreateFile(storageFile));
        }
        catch
        {
            // OS file drag-out isn't supported on this platform/backend; the in-app drag payload
            // added by the caller still allows in-app drops to work.
        }
    }

    /// <summary>Reads the slot data back from a drop, or null if it isn't present/valid.</summary>
    public static SlotDragData? TryGet(IDataTransfer? transfer)
    {
        if (transfer?.TryGetValue(Format) is { } raw && TryDeserialize(raw, out var data))
            return data;
        return null;
    }

    /// <summary>
    /// Reads the payload only when it belongs to the supplied save session. This keeps stale native
    /// drag data from reaching a viewer after the active save has changed.
    /// </summary>
    public static SlotDragData? TryGet(IDataTransfer? transfer, Guid expectedSessionId)
    {
        var data = TryGet(transfer);
        return data is not null && data.SessionId == expectedSessionId ? data : null;
    }

    /// <summary>Returns whether a transfer contains a PKHeX slot payload, even if its session is stale.</summary>
    public static bool HasCustomPayload(IDataTransfer? transfer)
        => transfer?.TryGetValue(Format) is not null || HasEditorPayload(transfer);

    /// <summary>
    /// Allows the exported entity file from a different save session to use the ordinary import
    /// and conversion path. Same-session drags must keep using the slot move/copy path, and stale
    /// slot payloads without exactly one exported entity file are not actionable.
    /// </summary>
    public static bool HasCrossSessionEntityFile(IDataTransfer? transfer, Guid expectedSessionId)
    {
        var data = TryGet(transfer);
        return data is not null
            && data.SessionId != expectedSessionId
            && transfer?.TryGetFiles() is { Length: 1 };
    }

    /// <summary>Maps a valid slot payload and pointer modifiers to the operation the drop will perform.</summary>
    public static DragDropEffects GetDropEffect(SlotDragData data, SlotLocation destination, KeyModifiers modifiers)
    {
        if (data.Source.Equals(destination))
            return DragDropEffects.None;

        return modifiers.HasFlag(KeyModifiers.Control)
            ? DragDropEffects.Copy
            : DragDropEffects.Move;
    }

    private static string Serialize(SlotDragData data)
        => $"{data.SessionId:N}:{(data.Source.IsParty ? 1 : 0)}:{data.Source.Box}:{data.Source.Slot}";

    private static bool TryDeserialize(string raw, out SlotDragData data)
    {
        data = default!;
        var parts = raw.Split(':');
        var offset = 0;
        var sessionId = Guid.Empty;
        if (parts.Length == 4)
        {
            if (!Guid.TryParseExact(parts[0], "N", out sessionId))
                return false;
            offset = 1;
        }

        if (parts.Length != offset + 3
            || !int.TryParse(parts[offset], out var isParty)
            || !int.TryParse(parts[offset + 1], out var box)
            || !int.TryParse(parts[offset + 2], out var slot))
        {
            return false;
        }

        var source = new SlotLocation { Box = box, Slot = slot, IsParty = isParty != 0 };
        data = new SlotDragData(source, sessionId);
        return true;
    }
}
