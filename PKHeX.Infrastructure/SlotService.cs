using PKHeX.Application.Abstractions;
using PKHeX.Core;

namespace PKHeX.Infrastructure;

/// <summary>Default implementation of <see cref="ISlotService"/>.</summary>
public sealed class SlotService : ISlotService
{
    public Guid SessionId { get; private set; } = Guid.NewGuid();

    public PKM? ClipboardPKM { get; private set; }

    public event Action<SlotLocation>? ViewRequested;
    public event Action<SlotLocation>? SetRequested;
    public event Action<SlotLocation>? DeleteRequested;
    public event Action<SlotLocation, SlotLocation, bool>? MoveRequested;
    public event Func<SlotLocation, PKM, Task>? ReplaceRequested;

    public void SetClipboard(PKM pk) => ClipboardPKM = pk.Clone();

    public void ClearClipboard() => ClipboardPKM = null;

    public void RequestView(SlotLocation location) => ViewRequested?.Invoke(location);

    public void RequestView(Guid sessionId, SlotLocation location)
    {
        if (IsCurrentSession(sessionId))
            RequestView(location);
    }

    public void RequestSet(SlotLocation location) => SetRequested?.Invoke(location);

    public void RequestSet(Guid sessionId, SlotLocation location)
    {
        if (IsCurrentSession(sessionId))
            RequestSet(location);
    }

    public void RequestDelete(SlotLocation location) => DeleteRequested?.Invoke(location);

    public void RequestDelete(Guid sessionId, SlotLocation location)
    {
        if (IsCurrentSession(sessionId))
            RequestDelete(location);
    }

    public void RequestMove(SlotLocation source, SlotLocation destination, bool clone)
        => MoveRequested?.Invoke(source, destination, clone);

    public void RequestMove(Guid sessionId, SlotLocation source, SlotLocation destination, bool clone)
    {
        if (IsCurrentSession(sessionId))
            RequestMove(source, destination, clone);
    }

    public Task RequestReplaceAsync(Guid sessionId, SlotLocation destination, PKM replacement)
    {
        if (!IsCurrentSession(sessionId) || ReplaceRequested is not { } handler)
            return Task.CompletedTask;

        return handler(destination, replacement);
    }

    public void ResetSession() => SessionId = Guid.NewGuid();

    public bool IsCurrentSession(Guid sessionId) => sessionId != Guid.Empty && sessionId == SessionId;
}
