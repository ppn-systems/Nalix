// Copyright (c) 2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using Nalix.Common.Networking.Packets;
using Nalix.Framework.Configuration;
using Nalix.Framework.Injection;
using Nalix.Framework.Random;
using Nalix.SDK.Configuration;
using Nalix.SDK.Transport.Internal;

namespace Nalix.SDK.Transport;

/// <summary>
/// Provides a TCP session implementation optimized for IoT scenarios,
/// including thread-safe connect logic, event-driven frame handling,
/// simple bandwidth tracking, and automatic reconnection.
/// </summary>
[System.Diagnostics.CodeAnalysis.DynamicallyAccessedMembers(
    System.Diagnostics.CodeAnalysis.DynamicallyAccessedMemberTypes.NonPublicMethods |
    System.Diagnostics.CodeAnalysis.DynamicallyAccessedMemberTypes.PublicParameterlessConstructor)]
public sealed class IoTTcpSession : TcpSessionBase, System.IDisposable
{
    #region Fields

    private System.String? _host;
    private System.UInt16? _port;
    internal System.Int64 _bytesSent = 0;
    private System.Int32 _reconnecting = 0;
    internal System.Int64 _bytesReceived = 0;
    private System.Int32 _hasEverConnected = 0;

    /// <summary>
    /// Serializes Connect/Disconnect operations — prevents concurrent reconnect races.
    /// Disposed in <see cref="Dispose"/>.
    /// </summary>
    private readonly System.Threading.SemaphoreSlim _connectLock = new(1, 1);

    #endregion Fields

    #region Properties

    /// <summary>
    /// Gets the total number of bytes sent.
    /// </summary>
    public System.Int64 BytesSent => System.Threading.Interlocked.Read(ref _bytesSent);

    /// <summary>
    /// Gets the total number of bytes received.
    /// </summary>
    public System.Int64 BytesReceived => System.Threading.Interlocked.Read(ref _bytesReceived);

    #endregion Properties

    #region Constructors

    /// <summary>
    /// Initializes a new instance of <see cref="IoTTcpSession"/> using
    /// <see cref="ConfigurationManager"/> and <see cref="InstanceManager"/>.
    /// </summary>
    /// <exception cref="System.InvalidOperationException">
    /// Thrown when <see cref="IPacketRegistry"/> is not registered
    /// or <see cref="TransportOptions"/> fails validation.
    /// </exception>
    public IoTTcpSession() : base()
    {
        Catalog = InstanceManager.Instance.GetExistingInstance<IPacketRegistry>()
            ?? throw new System.InvalidOperationException(
                $"[SDK.{nameof(IoTTcpSession)}] IPacketRegistry not found in InstanceManager.");

        Options = ConfigurationManager.Instance.Get<TransportOptions>();
        Options.Validate();

        Logging?.Info($"[SDK.{nameof(IoTTcpSession)}] Created, options validated.");
    }

    /// <summary>
    /// Initializes a new instance of <see cref="IoTTcpSession"/> with explicit dependencies.
    /// </summary>
    /// <param name="options">Transport configuration. Must not be <see langword="null"/>.</param>
    /// <param name="registry">Packet registry. Must not be <see langword="null"/>.</param>
    public IoTTcpSession(TransportOptions options, IPacketRegistry registry) : base()
    {
        Options = options;
        Catalog = registry;

        System.ArgumentNullException.ThrowIfNull(Options);
        System.ArgumentNullException.ThrowIfNull(Catalog);
    }

    #endregion Constructors

    #region Overrides

    /// <inheritdoc/>
    protected override void InitializeFrame()
    {
        i_sender = new FRAME_SENDER(RequireConnectedSocket, Options, ReportBytesSent, HandleSendError);
        i_receiver = new FRAME_READER(RequireConnectedSocket, Options, HandleReceiveMessage, HandleReceiveError, ReportBytesReceived);

        Logging?.Debug($"[SDK.{nameof(IoTTcpSession)}] Frame helpers created.");
    }

    /// <inheritdoc/>
    protected override void StartReceiveWorker(System.Threading.CancellationToken loopToken)
    {
        if (i_receiver is null)
        {
            return;
        }

        // IoT target: no TaskManager dependency — plain Task.Run is intentional here.
        // The task is fire-and-forget; errors are routed through HANDLE_RECEIVE_ERROR.
        _ = System.Threading.Tasks.Task.Run(async () =>
        {
            try
            {
                Logging?.Info($"[SDK.{nameof(IoTTcpSession)}] Receive worker started.");
                await i_receiver.ReceiveLoopAsync(loopToken).ConfigureAwait(false);
            }
            catch (System.OperationCanceledException)
            {
                // Normal shutdown — do not log as error.
            }
            catch (System.Exception ex)
            {
                Logging?.Error($"[SDK.{nameof(IoTTcpSession)}] Receive worker crashed: {ex.Message}", ex);
                HandleReceiveError(ex);
            }
        }, System.Threading.CancellationToken.None);
    }

    /// <inheritdoc/>
    public override async System.Threading.Tasks.Task ConnectAsync(
        System.String? host = null,
        System.UInt16? port = null,
        System.Threading.CancellationToken ct = default)
    {
        System.ObjectDisposedException.ThrowIf(
            System.Threading.Volatile.Read(ref _disposed) == 1, nameof(IoTTcpSession));

        // Serialize concurrent connect calls — important for IoT auto-reconnect racing.
        await _connectLock.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            System.String? effectiveHost = System.String.IsNullOrWhiteSpace(host) ? Options.Address : host;
            System.UInt16 effectivePort = port ?? Options.Port;

            if (System.String.IsNullOrWhiteSpace(effectiveHost))
            {
                throw new System.ArgumentException("Host is required.", nameof(host));
            }

            // Already connected to the same endpoint — nothing to do.
            if (IsConnected &&
                System.String.Equals(_host, effectiveHost, System.StringComparison.OrdinalIgnoreCase) &&
                _port == effectivePort)
            {
                Logging?.Debug($"[SDK.{nameof(IoTTcpSession)}] Already connected to {effectiveHost}:{effectivePort}.");
                return;
            }

            if (IsConnected)
            {
                Logging?.Debug($"[SDK.{nameof(IoTTcpSession)}] Cleaning up existing connection.");
                TearDownConnection();
            }

            lock (i_sync)
            {
                if (i_loopCts is not null)
                {
                    CancelAndDispose(ref i_loopCts);
                }
            }

            SetState(TcpSessionState.Connecting);

            // IoT networks are often slow — always apply a connect timeout.
            System.Int32 timeoutMs = Options.ConnectTimeoutMillis > 0
                ? Options.ConnectTimeoutMillis
                : 15_000; // 15 s default for IoT

            using System.Threading.CancellationTokenSource connectCts =
                System.Threading.CancellationTokenSource.CreateLinkedTokenSource(ct);
            connectCts.CancelAfter(timeoutMs);

            // Fast path: skip DNS if already an IP address.
            System.Net.IPAddress[] addrs = System.Net.IPAddress.TryParse(effectiveHost, out System.Net.IPAddress? ip)
                ? [ip]
                : await System.Net.Dns.GetHostAddressesAsync(effectiveHost, connectCts.Token)
                                      .ConfigureAwait(false);

            System.Exception? lastEx = null;

            foreach (System.Net.IPAddress addr in addrs)
            {
                System.Net.Sockets.Socket s = new(
                    addr.AddressFamily,
                    System.Net.Sockets.SocketType.Stream,
                    System.Net.Sockets.ProtocolType.Tcp);

                try
                {
                    s.NoDelay = Options.NoDelay;
                    s.SendBufferSize = Options.BufferSize;
                    s.ReceiveBufferSize = Options.BufferSize;

                    await s.ConnectAsync(
                        new System.Net.IPEndPoint(addr, effectivePort),
                        connectCts.Token).ConfigureAwait(false);

                    System.Threading.CancellationToken loopToken;

                    lock (i_sync)
                    {
                        i_socket = s;
                        i_loopCts = new System.Threading.CancellationTokenSource();
                        loopToken = i_loopCts.Token;
                        _host = effectiveHost;
                        _port = effectivePort;
                    }

                    InitializeFrame();

                    System.Boolean isReconnect =
                        System.Threading.Interlocked.Exchange(ref _hasEverConnected, 1) == 1;

                    if (isReconnect)
                    {
                        RaiseConnected();
                        RaiseReconnected(0);
                    }
                    else
                    {
                        Logging?.Info($"[SDK.{nameof(IoTTcpSession)}] Connected to {effectiveHost}:{effectivePort}.");
                        RaiseConnected();
                    }

                    StartReceiveWorker(loopToken);
                    System.Threading.Interlocked.Exchange(ref _reconnecting, 0);

                    return;
                }
                catch (System.OperationCanceledException)
                {
                    try { s.Dispose(); } catch { }
                    Logging?.Info($"[SDK.{nameof(IoTTcpSession)}] ConnectAsync cancelled for {addr}:{effectivePort}.");
                    throw;
                }
                catch (System.Exception ex)
                {
                    lastEx = ex;
                    try { s.Dispose(); } catch { }
                    Logging?.Warn($"[SDK.{nameof(IoTTcpSession)}] Failed to connect to {addr}:{effectivePort}: {ex.Message}", ex);
                }
            }

            Logging?.Error($"[SDK.{nameof(IoTTcpSession)}] Could not connect to {effectiveHost}:{effectivePort}; last error: {lastEx?.Message}", lastEx);
            SetState(TcpSessionState.Disconnected);
            throw lastEx
                ?? new System.Net.Sockets.SocketException(
                    (System.Int32)System.Net.Sockets.SocketError.HostNotFound);
        }
        finally
        {
            _connectLock.Release();
        }
    }

    /// <inheritdoc/>
    protected override void ReportBytesSent(System.Int32 count)
    {
        System.Threading.Interlocked.Add(ref _bytesSent, count);
        base.ReportBytesSent(count);
    }

    /// <inheritdoc/>
    protected override void ReportBytesReceived(System.Int32 count)
    {
        System.Threading.Interlocked.Add(ref _bytesReceived, count);
        base.ReportBytesReceived(count);
    }

    /// <inheritdoc/>
    protected override void HandleSendError(System.Exception ex)
    {
        Logging?.Warn($"[SDK.{nameof(IoTTcpSession)}] Send error: {ex.Message}", ex);
        RaiseError(ex);
        TriggerReconnect(ex);
    }

    /// <inheritdoc/>
    protected override void HandleReceiveError(System.Exception ex)
    {
        Logging?.Warn($"[SDK.{nameof(IoTTcpSession)}] Receive error: {ex.Message}", ex);
        RaiseError(ex);
        TriggerReconnect(ex);
    }

    /// <inheritdoc/>
    protected override void TearDownConnection()
    {
        System.Boolean wasConnected = IsConnected;
        base.TearDownConnection();

        if (wasConnected)
        {
            Logging?.Info($"[SDK.{nameof(IoTTcpSession)}] Disconnected.");
            RaiseDisconnected(new System.Exception("Disconnected"));
        }
    }

    /// <inheritdoc/>
    public void Dispose(System.Boolean disposing)
    {
        if (disposing)
        {
            _connectLock.Dispose();
        }

        base.Dispose();
    }

    #endregion Overrides

    #region Private Methods

    /// <summary>
    /// Triggers a non-reentrant reconnect cycle after a send/receive error.
    /// Uses the same exponential backoff + jitter strategy as <see cref="TcpSession"/>.
    /// </summary>
    private void TriggerReconnect(System.Exception cause)
    {
        if (!Options.ReconnectEnabled)
        {
            return;
        }

        // CAS ensures only one reconnect loop runs at a time.
        if (System.Threading.Interlocked.CompareExchange(ref _reconnecting, 1, 0) != 0)
        {
            Logging?.Trace($"[SDK.{nameof(IoTTcpSession)}] Reconnect already in progress, skipping.");
            return;
        }

        Logging?.Info($"[SDK.{nameof(IoTTcpSession)}] Triggering auto-reconnect after: {cause.Message}");
        _ = ReconnectLoopAsync(cause);
    }

    private async System.Threading.Tasks.Task ReconnectLoopAsync(System.Exception cause)
    {
        TearDownConnection();

        if (System.Threading.Volatile.Read(ref _disposed) == 1 ||
            System.String.IsNullOrEmpty(_host) || _port == 0)
        {
            System.Threading.Interlocked.Exchange(ref _reconnecting, 0);
            return;
        }

        SetState(TcpSessionState.Reconnecting);

        System.Int32 attempt = 0;
        System.Int64 max = System.Math.Max(1, Options.ReconnectMaxDelayMillis);
        System.Int64 delay = System.Math.Max(1, Options.ReconnectBaseDelayMillis);

        // Dedicated CTS so Dispose() can abort the delay immediately.
        using System.Threading.CancellationTokenSource reconnectCts = new();

        while (System.Threading.Volatile.Read(ref _disposed) == 0 &&
               (Options.ReconnectMaxAttempts == 0 || attempt < Options.ReconnectMaxAttempts))
        {
            attempt++;
            System.Int64 jitter = (System.Int64)(Csprng.NextDouble() * delay * 0.3);

            try
            {
                await System.Threading.Tasks.Task.Delay(
                    (System.Int32)System.Math.Min(delay + jitter, System.Int32.MaxValue),
                    reconnectCts.Token).ConfigureAwait(false);
            }
            catch (System.OperationCanceledException)
            {
                break; // Disposed during delay.
            }

            try
            {
                await ConnectAsync(_host, _port, reconnectCts.Token).ConfigureAwait(false);
                Logging?.Info($"[SDK.{nameof(IoTTcpSession)}] Reconnected after {attempt} attempt(s).");
                RaiseReconnected(attempt);
                return;
            }
            catch (System.OperationCanceledException)
            {
                break;
            }
            catch (System.Exception ex)
            {
                Logging?.Warn($"[SDK.{nameof(IoTTcpSession)}] Reconnect attempt {attempt} failed: {ex.Message}", ex);
                delay = System.Math.Min(max, delay * 2);
            }
        }

        Logging?.Error($"[SDK.{nameof(IoTTcpSession)}] Reconnect exhausted after {attempt} attempt(s).");
        System.Threading.Interlocked.Exchange(ref _reconnecting, 0);
        SetState(TcpSessionState.Disconnected);
    }

    #endregion Private Methods
}