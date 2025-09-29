// Copyright (c) 2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using Nalix.Common.Networking.Packets;
using Nalix.Framework.Configuration;
using Nalix.SDK.Configuration;
using Nalix.SDK.Transport.Internal;

namespace Nalix.SDK.Transport;

/// <summary>
/// Provides a TCP session implementation optimized for IoT scenarios,
/// including thread-safe connect logic, event-driven frame handling,
/// and simple bandwidth tracking.
/// </summary>
[System.Diagnostics.CodeAnalysis.DynamicallyAccessedMembers(
    System.Diagnostics.CodeAnalysis.DynamicallyAccessedMemberTypes.NonPublicMethods |
    System.Diagnostics.CodeAnalysis.DynamicallyAccessedMemberTypes.PublicParameterlessConstructor)]
public sealed class TcpIoTSession : BaseTcpSession
{
    #region Fields

    private System.String? _host;
    private System.UInt16? _port;
    private System.Int32 _hasEverConnected = 0;
    internal System.Int64 _bytesSent = 0;
    internal System.Int64 _bytesReceived = 0;

    /// <summary>
    /// Semaphore to serialize Connect/Disconnect operations and avoid races.
    /// </summary>
    private readonly System.Threading.SemaphoreSlim _connectLock = new(1, 1);

    /// <summary>
    /// Keep a reference to receive worker task so we can observe it if needed.
    /// </summary>
    private new System.Threading.Tasks.Task? _receiveTask;

    #endregion Fields

    #region Properties

    /// <inheritdoc/>
    public System.Int64 BytesSent => System.Threading.Interlocked.Read(ref _bytesSent);

    /// <inheritdoc/>
    public System.Int64 BytesReceived => System.Threading.Interlocked.Read(ref _bytesReceived);

    #endregion Properties

    #region Constructor

    /// <summary>
    /// Initializes a new instance of <see cref="TcpIoTSession"/> and validates options.
    /// </summary>
    public TcpIoTSession() : base()
    {
        Options = ConfigurationManager.Instance.Get<TransportOptions>();
        Options.Validate();
        Logging?.Info($"[SDK.{GetType().Name}] TcpIoTSession created, options validated");
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="TcpIoTSession"/> class.
    /// </summary>
    /// <param name="options">
    /// The transport configuration options used to initialize the TCP session.
    /// </param>
    /// <param name="registry">
    /// The packet registry responsible for managing and resolving packet handlers.
    /// </param>
    /// <exception cref="System.ArgumentNullException">
    /// Thrown when <paramref name="options"/> or <paramref name="registry"/> is null.
    /// </exception>
    public TcpIoTSession(TransportOptions options, IPacketRegistry registry) : base()
    {
        System.ArgumentNullException.ThrowIfNull(options);
        System.ArgumentNullException.ThrowIfNull(registry);

        Options = options;
        Catalog = registry;
    }

    #endregion Constructor

    #region Overrides

    /// <summary>
    /// Sets up send and receive frame helpers for this session.
    /// </summary>
    protected override void CreateFrameHelpers()
    {
        _sender = new FRAME_SENDER(GET_CONNECTED_SOCKET_OR_THROW, Options, REPORT_BYTES_SENT, HANDLE_SEND_ERROR);
        _receiver = new FRAME_READER(GET_CONNECTED_SOCKET_OR_THROW, Options, HANDLE_RECEIVE_MESSAGE, HANDLE_RECEIVE_ERROR, REPORT_BYTES_RECEIVED);
        Logging?.Debug($"[SDK.{GetType().Name}] Frame helpers created");
    }

    /// <summary>
    /// Launches a background task to handle incoming frames.
    /// </summary>
    /// <param name="loopToken">Cancellation token for lifecycle.</param>
    protected override void StartReceiveWorker(System.Threading.CancellationToken loopToken)
    {
        if (_receiver is null)
        {
            return;
        }

        _receiveTask = System.Threading.Tasks.Task.Run(async () =>
        {
            try
            {
                Logging?.Info($"[SDK.{GetType().Name}] IoT receive worker started");
                await _receiver.ReceiveLoopAsync(loopToken).ConfigureAwait(false);
            }
            catch (System.Exception ex)
            {
                Logging?.Error($"[SDK.{GetType().Name}] Receive worker crashed: {ex.Message}", ex);
                HANDLE_RECEIVE_ERROR(ex);
            }
        }, System.Threading.CancellationToken.None);
    }

    /// <inheritdoc/>
    public override async System.Threading.Tasks.Task ConnectAsync(
        System.String? host = null,
        System.UInt16? port = null,
        System.Threading.CancellationToken ct = default)
    {
        System.ObjectDisposedException.ThrowIf(System.Threading.Volatile.Read(ref _disposed) == 1, nameof(TcpIoTSession));
        await _connectLock.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            System.String? effectiveHost = System.String.IsNullOrWhiteSpace(host) ? Options.Address : host;
            System.UInt16 effectivePort = port ?? Options.Port;
            if (System.String.IsNullOrWhiteSpace(effectiveHost))
            {
                throw new System.ArgumentException("Host required");
            }

            if (IsConnected &&
                System.String.Equals(_host, effectiveHost, System.StringComparison.OrdinalIgnoreCase) &&
                _port == effectivePort)
            {
                Logging?.Debug($"[SDK.{GetType().Name}] Already connected to {effectiveHost}:{effectivePort}");
                return;
            }

            if (IsConnected)
            {
                Logging?.Debug($"[SDK.{GetType().Name}] Cleaning up existing connection before new connect");
                CLEANUP_CONNECTION();
            }

            lock (_sync)
            {
                if (_loopCts is not null)
                {
                    CANCEL_AND_DISPOSE_LOCKED(ref _loopCts);
                }
            }

            System.Exception? lastEx = null;
            System.Net.IPAddress[] addrs = System.Net.IPAddress.TryParse(effectiveHost, out var ip)
                ? [ip]
                : await System.Net.Dns.GetHostAddressesAsync(effectiveHost, ct).ConfigureAwait(false);

            foreach (var addr in addrs)
            {
                var s = new System.Net.Sockets.Socket(
                    addr.AddressFamily,
                    System.Net.Sockets.SocketType.Stream,
                    System.Net.Sockets.ProtocolType.Tcp);

                try
                {
                    s.NoDelay = Options.NoDelay;
                    s.SendBufferSize = Options.BufferSize;
                    s.ReceiveBufferSize = Options.BufferSize;
                    // Optionally set socket options here if required

                    await s.ConnectAsync(new System.Net.IPEndPoint(addr, effectivePort), ct).ConfigureAwait(false);

                    System.Threading.CancellationToken loopToken;
                    lock (_sync)
                    {
                        _socket = s;
                        _loopCts = new System.Threading.CancellationTokenSource();
                        loopToken = _loopCts.Token;
                        _host = effectiveHost;
                        _port = effectivePort;
                    }
                    Logging?.Info($"[SDK.{GetType().Name}] Connected to {effectiveHost}:{effectivePort}");

                    CreateFrameHelpers();

                    System.Boolean isReconnect = System.Threading.Interlocked.Exchange(ref _hasEverConnected, 1) == 1;
                    RaiseConnected(); // IoT: treat as connected again both first and reconnect

                    StartReceiveWorker(loopToken);

                    return;
                }
                catch (System.OperationCanceledException)
                {
                    try { s.Dispose(); } catch { }
                    Logging?.Info($"[SDK.{GetType().Name}] ConnectAsync cancelled for {addr}:{effectivePort}");
                    throw;
                }
                catch (System.Exception ex)
                {
                    lastEx = ex;
                    try { s.Dispose(); } catch { }
                    Logging?.Warn($"[SDK.{GetType().Name}] Failed to connect to {addr}:{effectivePort}: {ex.Message}", ex);
                }
            }

            Logging?.Error($"[SDK.{GetType().Name}] Could not connect to {effectiveHost}:{effectivePort}; last error: {lastEx?.Message}", lastEx);
            throw lastEx ?? new System.Net.Sockets.SocketException((System.Int32)System.Net.Sockets.SocketError.HostNotFound);
        }
        finally
        {
            _connectLock.Release();
        }
    }

    /// <inheritdoc/>
    protected override void REPORT_BYTES_SENT(System.Int32 count)
    {
        System.Threading.Interlocked.Add(ref _bytesSent, count);
        base.REPORT_BYTES_SENT(count);
    }

    /// <inheritdoc/>
    protected override void REPORT_BYTES_RECEIVED(System.Int32 count)
    {
        System.Threading.Interlocked.Add(ref _bytesReceived, count);
        base.REPORT_BYTES_RECEIVED(count);
    }

    /// <inheritdoc/>
    protected override void HANDLE_SEND_ERROR(System.Exception ex)
    {
        Logging?.Warn($"[SDK.{GetType().Name}] Send error: {ex.Message}", ex);
        RaiseError(ex);
        CLEANUP_CONNECTION();
    }

    /// <inheritdoc/>
    protected override void HANDLE_RECEIVE_ERROR(System.Exception ex)
    {
        Logging?.Warn($"[SDK.{GetType().Name}] Receive error: {ex.Message}", ex);
        RaiseError(ex);
        CLEANUP_CONNECTION();
    }

    /// <inheritdoc/>
    protected override void CLEANUP_CONNECTION()
    {
        System.Boolean wasConnected = IsConnected;
        base.CLEANUP_CONNECTION();
        if (wasConnected)
        {
            Logging?.Info($"[SDK.{GetType().Name}] Disconnected");
            RaiseDisconnected(new System.Exception("Disconnected"));
        }
    }

    #endregion
}