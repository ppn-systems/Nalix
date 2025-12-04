// Copyright (c) 2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using Nalix.Framework.Configuration;
using Nalix.SDK.Configuration;
using Nalix.SDK.Transport.Internal;

namespace Nalix.SDK.Transport;

/// <inheritdoc/>
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

    // Semaphore to serialize Connect/Disconnect operations and avoid races
    private readonly System.Threading.SemaphoreSlim _connectLock = new(initialCount: 1, maxCount: 1);

    // Keep a reference to receive worker task so we can observe it if needed
    private new System.Threading.Tasks.Task? _receiveTask;

    #endregion Fields

    #region Properties

    /// <inheritdoc/>
    public System.Int64 BytesSent => System.Threading.Interlocked.Read(ref _bytesSent);

    /// <inheritdoc/>
    public System.Int64 BytesReceived => System.Threading.Interlocked.Read(ref _bytesReceived);

    #endregion Properties

    #region Constructor

    /// <inheritdoc/>
    public TcpIoTSession() : base()
    {
        Options = ConfigurationManager.Instance.Get<TransportOptions>();
        Options.Validate();
    }

    #endregion Constructor

    #region Overrides

    /// <inheritdoc/>
    protected override void CreateFrameHelpers()
    {
        _sender = new FRAME_SENDER(GET_CONNECTED_SOCKET_OR_THROW, Options, REPORT_BYTES_SENT, HANDLE_SEND_ERROR);

        _receiver = new FRAME_READER(
            GET_CONNECTED_SOCKET_OR_THROW,
            Options,
            HANDLE_RECEIVE_MESSAGE,
            HANDLE_RECEIVE_ERROR,
            REPORT_BYTES_RECEIVED);
    }

    /// <inheritdoc/>
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
                await _receiver.ReceiveLoopAsync(loopToken).ConfigureAwait(false);
            }
            catch (System.Exception ex)
            {
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
                return;
            }

            if (IsConnected)
            {
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

            // Try to short-circuit if host is already an IP literal to avoid DNS lookup overhead.
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

                    // Optionally: set keepalive, timeouts, linger etc. based on Options
                    // s.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.KeepAlive, true);

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

                    CreateFrameHelpers();

                    System.Boolean isReconnect =
                        System.Threading.Interlocked.Exchange(ref _hasEverConnected, 1) == 1;

                    if (!isReconnect)
                    {
                        RaiseConnected();
                    }
                    else
                    {
                        // IoT: no separate reconnect event → treat as connected again
                        RaiseConnected();
                    }

                    StartReceiveWorker(loopToken);

                    return;
                }
                catch (System.OperationCanceledException)
                {
                    // honor cancellation immediately
                    try { s.Dispose(); } catch { }
                    throw;
                }
                catch (System.Exception ex)
                {
                    lastEx = ex;
                    try { s.Dispose(); } catch { }
                }
            }

            // Throw detailed SocketException if we have no lastEx
            throw lastEx ?? new System.Net.Sockets.SocketException(
                (System.Int32)System.Net.Sockets.SocketError.HostNotFound);
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
        RaiseError(ex);

        CLEANUP_CONNECTION();
    }

    /// <inheritdoc/>
    protected override void HANDLE_RECEIVE_ERROR(System.Exception ex)
    {
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
            RaiseDisconnected(new System.Exception("Disconnected"));
        }
    }

    #endregion
}