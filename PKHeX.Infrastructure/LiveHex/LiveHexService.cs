using System;
using System.Threading;
using System.Threading.Tasks;
using PKHeX.Application.Abstractions.LiveHex;
using PKHeX.Core;

namespace PKHeX.Infrastructure.LiveHex;

/// <summary>
/// Infrastructure implementation of <see cref="ILiveHexService"/>. Orchestrates a sys-botbase
/// connection (created via <see cref="IConsoleConnectionFactory"/>), validates the attached game,
/// and bridges box bytes between console RAM and a <see cref="SaveFile"/>.
/// </summary>
public sealed class LiveHexService : ILiveHexService
{
    private const int TimeoutMs = 8000;

    private readonly IConsoleConnectionFactory _factory;
    private readonly object _stateLock = new();
    private readonly SemaphoreSlim _lifecycleGate = new(1, 1);
    private IConsoleConnection? _connection;
    private LiveHexGameProfile? _profile;
    private LiveHexSessionInfo? _session;
    private IConsoleConnection? _pendingConnection;
    private CancellationTokenSource? _connectCancellation;

    public LiveHexService(IConsoleConnectionFactory factory) => _factory = factory;

    public bool IsConnected
    {
        get
        {
            lock (_stateLock)
                return _connection is { Connected: true };
        }
    }

    public LiveHexSessionInfo? Session
    {
        get
        {
            lock (_stateLock)
                return _session;
        }
    }

    public LiveHexGameSupport GetSupport(SaveFile sav)
    {
        var name = LiveHexGameProfiles.GetGameName(sav);
        if (LiveHexGameProfiles.IsSupported(sav))
            return new LiveHexGameSupport(true, name, $"Supported firmware: {LiveHexGameProfiles.GetSupportedVersions(sav)}");
        return new LiveHexGameSupport(false, name,
            "LiveHeX supports Sword/Shield, Brilliant Diamond/Shining Pearl, Legends: Arceus and Scarlet/Violet only.");
    }

    public async Task ConnectAsync(string ip, int port, SaveFile sav, CancellationToken cancellationToken = default)
    {
        using var connectCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        lock (_stateLock)
        {
            var previousAttempt = _connectCancellation;
            _connectCancellation = connectCancellation;
            previousAttempt?.Cancel();
        }

        var entered = false;
        IConsoleConnection? connected = null;
        try
        {
            await _lifecycleGate.WaitAsync(connectCancellation.Token).ConfigureAwait(false);
            entered = true;
            connectCancellation.Token.ThrowIfCancellationRequested();

            DisconnectInternal();
            var result = await Task.Run(
                () => Connect(ip, port, sav, connectCancellation.Token),
                connectCancellation.Token).ConfigureAwait(false);
            connected = result.Connection;
            connectCancellation.Token.ThrowIfCancellationRequested();

            lock (_stateLock)
            {
                if (!ReferenceEquals(_connectCancellation, connectCancellation))
                    throw new OperationCanceledException(connectCancellation.Token);

                _connection = result.Connection;
                _profile = result.Profile;
                _session = result.Session;
                connected = null;
            }
        }
        catch
        {
            connected?.Dispose();
            throw;
        }
        finally
        {
            if (entered)
                _lifecycleGate.Release();

            lock (_stateLock)
            {
                if (ReferenceEquals(_connectCancellation, connectCancellation))
                    _connectCancellation = null;
            }
        }
    }

    private (IConsoleConnection Connection, LiveHexGameProfile Profile, LiveHexSessionInfo Session) Connect(
        string ip,
        int port,
        SaveFile sav,
        CancellationToken cancellationToken)
    {
        if (!LiveHexGameProfiles.IsSupported(sav))
            throw new LiveHexConnectionException("This save type is not supported by LiveHeX.");

        var connection = _factory.Create();
        lock (_stateLock)
            _pendingConnection = connection;

        var success = false;
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            connection.Connect(ip, port, TimeoutMs); // throws LiveHexConnectionException on failure
            cancellationToken.ThrowIfCancellationRequested();
            var botbaseVersion = connection.GetBotbaseVersion();
            cancellationToken.ThrowIfCancellationRequested();
            var titleId = connection.GetTitleId();
            cancellationToken.ThrowIfCancellationRequested();
            if (!LiveHexGameProfiles.TitleMatchesSave(sav, titleId))
            {
                throw new LiveHexConnectionException(
                    $"The console is running a different game (title {titleId}) than the loaded save " +
                    $"({LiveHexGameProfiles.GetGameName(sav)}). Open the matching save and reconnect.");
            }

            var gameVersion = connection.GetGameInfo("version");
            cancellationToken.ThrowIfCancellationRequested();
            var profile = LiveHexGameProfiles.Resolve(sav, titleId, gameVersion)
                ?? throw new LiveHexConnectionException(
                    $"Unsupported game version '{gameVersion}'. Supported versions for " +
                    $"{LiveHexGameProfiles.GetGameName(sav)}: {LiveHexGameProfiles.GetSupportedVersions(sav)}.");

            success = true;
            return (connection, profile, new LiveHexSessionInfo(titleId, botbaseVersion, gameVersion, profile.Label));
        }
        finally
        {
            lock (_stateLock)
            {
                if (ReferenceEquals(_pendingConnection, connection))
                    _pendingConnection = null;
            }

            if (!success)
                connection.Dispose();
        }
    }

    public async Task DisconnectAsync()
    {
        CancellationTokenSource? connectCancellation;
        IConsoleConnection? pending;
        lock (_stateLock)
        {
            connectCancellation = _connectCancellation;
            pending = _pendingConnection;
            connectCancellation?.Cancel();
        }

        // Cancel and dispose the not-yet-published connection before waiting for the lifecycle gate.
        // This wakes socket handshakes that are blocked inside a synchronous protocol call and prevents
        // a late handshake from publishing a session after DisconnectAsync returns.
        pending?.Dispose();

        await _lifecycleGate.WaitAsync().ConfigureAwait(false);
        try
        {
            DisconnectInternal();
        }
        finally
        {
            _lifecycleGate.Release();
        }
    }

    private void DisconnectInternal()
    {
        IConsoleConnection? connection;
        lock (_stateLock)
        {
            connection = _connection;
            _connection = null;
            _profile = null;
            _session = null;
        }

        connection?.Dispose();
    }

    public async Task ReadBoxAsync(SaveFile sav, int box, CancellationToken cancellationToken = default)
    {
        await _lifecycleGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await Task.Run(() =>
            {
                var (connection, profile) = RequireSession(sav, box);
                var bytes = LiveHexBoxAddressing.ReadBox(connection, profile, box, sav.SIZE_BOXSLOT, sav.BoxSlotCount);
                if (!sav.SetBoxBinary(bytes, box))
                {
                    throw new LiveHexConnectionException(
                        "The box data read from the console could not be applied (the box is overwrite-protected).");
                }
            }, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _lifecycleGate.Release();
        }
    }

    public async Task WriteBoxAsync(SaveFile sav, int box, CancellationToken cancellationToken = default)
    {
        await _lifecycleGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await Task.Run(() =>
            {
                var (connection, profile) = RequireSession(sav, box);
                var bytes = sav.GetBoxBinary(box);
                LiveHexBoxAddressing.WriteBox(connection, profile, box, bytes, sav.SIZE_BOXSLOT, sav.BoxSlotCount);
            }, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _lifecycleGate.Release();
        }
    }

    private (IConsoleConnection Connection, LiveHexGameProfile Profile) RequireSession(SaveFile sav, int box)
    {
        lock (_stateLock)
        {
            if (_connection is not { Connected: true } connection || _profile is null)
                throw new LiveHexConnectionException("Not connected to a console.");
            if ((uint)box >= (uint)sav.BoxCount)
                throw new LiveHexConnectionException($"Box {box + 1} is out of range for this save (has {sav.BoxCount} boxes).");
            return (connection, _profile);
        }
    }
}
