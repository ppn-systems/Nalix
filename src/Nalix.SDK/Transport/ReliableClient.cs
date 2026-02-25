// Copyright (c) 2026 PPN Corporation. All rights reserved.

using Nalix.Common.Concurrency;
using Nalix.Common.Diagnostics;
using Nalix.Common.Infrastructure.Caching;
using Nalix.Common.Messaging.Packets;
using Nalix.Common.Messaging.Packets.Abstractions;
using Nalix.Framework.Configuration;
using Nalix.Framework.Injection;
using Nalix.Framework.Options;
using Nalix.Framework.Tasks;
using Nalix.SDK.Configuration;
using Nalix.SDK.Transport.Internal;

namespace Nalix.SDK.Transport;

/// <summary>
/// TCP client entry point. Delegates send/receive responsibilities to internal helpers.
/// </summary>
public sealed class ReliableClient : System.IDisposable
{
    #region Constants

    /// <summary>
    /// Header size in bytes for the framing protocol (2-byte little-endian total length prefix).
    /// </summary>
    public const System.Byte HeaderSize = 2;

    #endregion Constants

    #region Fields

    private readonly System.Threading.Lock _sync = new();
    private readonly TransportOptions _options;

    // helpers
    private FRAME_SENDER _sender;
    private FRAME_READER _receiver;

    // Socket + control
    private System.Net.Sockets.Socket _socket;
    private System.Threading.CancellationTokenSource _loopCts;

    // TaskManager integration
    private IWorkerHandle _receiveHandle;
    private System.String _heartbeatName;
    private System.String _rateSamplerName;

    // Endpoint info stored for reconnect
    private System.String _host;
    private System.Int32 _port;

    // State
    private volatile System.Boolean _disposed;
    private System.Int64 _bytesSent;

    // Rate tracking
    private System.Int64 _bytesReceived;
    private System.Int64 _sendCounterForInterval;
    private System.Int64 _receiveCounterForInterval;
    private System.Int64 _lastSendBps;
    private System.Int64 _lastReceiveBps;

    #endregion Fields

    #region Event Args

    // Events
    /// <summary>
    /// Occurs when the client has successfully connected to the remote endpoint.
    /// </summary>
    public event System.EventHandler OnConnected;
    /// <summary>
    /// Occurs when the client is disconnected. The <see cref="System.EventHandler{T}"/> argument
    /// contains the exception that caused the disconnect, or <c>null</c> if it was requested.
    /// </summary>
    public event System.EventHandler<System.Exception> OnDisconnected;
    /// <summary>
    /// Synchronous message-received event. Subscribers receive an <see cref="IBufferLease"/>
    /// and are responsible for disposing the lease when done.
    /// </summary>
    public event System.EventHandler<IBufferLease> OnMessageReceived;
    /// <summary>
    /// Asynchronous message-received callback. If set, the provided delegate will be invoked
    /// to handle received messages. The delegate is responsible for disposing the <see cref="IBufferLease"/>.
    /// </summary>
    public System.Func<ReliableClient, IBufferLease, System.Threading.Tasks.Task> OnMessageReceivedAsync;
    /// <summary>
    /// Occurs when bytes are written to the socket. The event argument is the number of bytes sent.
    /// </summary>
    public event System.EventHandler<System.Int64> OnBytesSent;
    /// <summary>
    /// Occurs when bytes are received from the socket. The event argument is the number of bytes (header+payload) received for that frame.
    /// </summary>
    public event System.EventHandler<System.Int64> OnBytesReceived;
    /// <summary>
    /// Occurs when an internal error happens. Subscribers can use this for logging or diagnostics.
    /// </summary>
    public event System.EventHandler<System.Exception> OnError;

    #endregion Event Args

    #region Properties

    /// <summary>
    /// Gets whether the client is connected.
    /// </summary>
    public System.Boolean IsConnected => _socket?.Connected == true && !_disposed;

    /// <summary>
    /// Total bytes sent (thread-safe).
    /// </summary>
    public System.Int64 BytesSent => System.Threading.Interlocked.Read(ref _bytesSent);

    /// <summary>
    /// Total bytes received (thread-safe).
    /// </summary>
    public System.Int64 BytesReceived => System.Threading.Interlocked.Read(ref _bytesReceived);

    /// <summary>
    /// Last measured bytes-per-second sent (sampled every 1s).
    /// </summary>
    public System.Int64 SendBytesPerSecond => System.Threading.Interlocked.Read(ref _lastSendBps);

    /// <summary>
    /// Last measured bytes-per-second received (sampled every 1s).
    /// </summary>
    public System.Int64 ReceiveBytesPerSecond => System.Threading.Interlocked.Read(ref _lastReceiveBps);

    #endregion Properties

    #region Constructor

    /// <summary>Constructs a new client and loads ClientOptions via ConfigurationManager.</summary>
    /// <remarks>
    /// If configuration lookup fails, default <see cref="TransportOptions"/> values are used.
    /// The instance also attempts to obtain or create an <see cref="ITaskManager"/> and
    /// other shared instances via <see cref="InstanceManager"/>.
    /// </remarks>
    public ReliableClient()
    {
        try
        {
            _options = ConfigurationManager.Instance.Get<TransportOptions>();
        }
        catch
        {
            _options = new TransportOptions
            {
                ConnectTimeoutMillis = 5000,
                ReconnectEnabled = true,
                ReconnectMaxAttempts = 0,
                ReconnectBaseDelayMillis = 500,
                ReconnectMaxDelayMillis = 30000,
                KeepAliveIntervalMillis = 0,
                NoDelay = true,
                BufferSize = 8192,
                MaxPacketSize = PacketConstants.PacketSizeLimit
            };
        }
    }

    #endregion Constructor

    #region Public API

    /// <summary>
    /// Connects to the specified host and port asynchronously.
    /// This method stores host/port for automatic reconnects.
    /// </summary>
    /// <param name="host">The hostname or IP address to connect to.</param>
    /// <param name="port">The destination port.</param>
    /// <param name="cancellationToken">A <see cref="System.Threading.CancellationToken"/> to cancel the connect attempt.</param>
    /// <returns>A task that completes when connected.</returns>
    /// <exception cref="System.ArgumentNullException">Thrown when <paramref name="host"/> is null or whitespace.</exception>
    /// <exception cref="System.ObjectDisposedException">Thrown when the client has been disposed.</exception>
    /// <exception cref="System.InvalidOperationException">Thrown when the client is already connected.</exception>
    /// <remarks>
    /// This method will attempt to resolve DNS for the provided host and try each returned address.
    /// If <see cref="TransportOptions.ConnectTimeoutMillis"/> is set, the connect attempt will be cancelled
    /// after that timeout.
    /// </remarks>
    public async System.Threading.Tasks.Task ConnectAsync(System.String host, System.Int32 port, System.Threading.CancellationToken cancellationToken = default)
    {
        if (System.String.IsNullOrWhiteSpace(host))
        {
            throw new System.ArgumentNullException(nameof(host));
        }

        System.ObjectDisposedException.ThrowIf(this._disposed, nameof(ReliableClient));

        _host = host;
        _port = port;

        if (IsConnected)
        {
            return;
        }

        // cancel any existing loops
        lock (_sync)
        {
            if (_loopCts?.IsCancellationRequested == false)
            {
                try { _loopCts.Cancel(); } catch { }
            }
        }

        System.Threading.CancellationTokenSource connectCts = System.Threading.CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        if (_options.ConnectTimeoutMillis > 0)
        {
            connectCts.CancelAfter(_options.ConnectTimeoutMillis);
        }

        System.Exception lastEx = null;

        try
        {
            System.Net.IPAddress[] addrs = await System.Net.Dns.GetHostAddressesAsync(host, cancellationToken).ConfigureAwait(false);

            foreach (System.Net.IPAddress addr in addrs)
            {
                if (connectCts.IsCancellationRequested)
                {
                    break;
                }

                System.Net.Sockets.Socket s = new(addr.AddressFamily, System.Net.Sockets.SocketType.Stream, System.Net.Sockets.ProtocolType.Tcp);
                try
                {
                    s.NoDelay = _options.NoDelay;
                    s.SendBufferSize = _options.BufferSize;
                    s.ReceiveBufferSize = _options.BufferSize;

                    await s.ConnectAsync(new System.Net.IPEndPoint(addr, port), connectCts.Token).ConfigureAwait(false);

                    // assign socket and start loops
                    lock (_sync)
                    {
                        _socket = s;
                        _loopCts = new System.Threading.CancellationTokenSource();
                    }

                    // create helpers bound to the socket getter
                    System.Func<System.Net.Sockets.Socket> socketGetter = GET_CONNECTED_SOCKET_OR_THROW;

                    _sender = new FRAME_SENDER(socketGetter, _options, REPORT_BYTE_SSENT, HANDLE_SEND_ERROR);
                    _receiver = new FRAME_READER(socketGetter, _options, HANDLE_RECEIVE_MESSAGE, HANDLE_RECEIVE_ERROR, REPORT_BYTES_RECEIVED);

                    InstanceManager.Instance.GetExistingInstance<ILogger>()?
                                            .Info($"[SDK.{nameof(ReliableClient)}] connected remote={addr}:{port}");
                    OnConnected?.Invoke(this, System.EventArgs.Empty);

                    // start background loops using TaskManager when possible
                    System.Threading.CancellationToken loopToken = _loopCts.Token;

                    // Start rate sampler as a recurring TaskManager job (1s interval).
                    if (true)
                    {
                        try
                        {
                            _rateSamplerName = $"ClientRateSampler-{addr}:{port}";

                            InstanceManager.Instance.GetOrCreateInstance<TaskManager>().ScheduleRecurring(
                                name: _rateSamplerName,
                                interval: System.TimeSpan.FromMilliseconds(1000),
                                work: async (ct) =>
                                {
                                    System.Threading.CancellationToken effective = ct.CanBeCanceled ? ct : loopToken;
                                    await RATE_SAMPLER_TICK(effective).ConfigureAwait(false);
                                },
                                options: new RecurringOptions { NonReentrant = true, Tag = "ClientRateSampler" }
                            );
                        }
                        catch (System.Exception ex)
                        {
                            InstanceManager.Instance.GetExistingInstance<ILogger>()?
                                                    .Warn($"[SDK.{nameof(ReliableClient)}] schedule-rate-sampler-failed ex={ex.Message}");
                            // NOTE: per request, do NOT fallback to Task.Run; sampler will be skipped.
                        }
                    }

                    // Receive loop as a worker
                    try
                    {
                        // Schedule worker: it will use the same cancellation token so Cancel() on _loopCts stops it.
                        _receiveHandle = InstanceManager.Instance.GetOrCreateInstance<TaskManager>().ScheduleWorker(
                            name: $"ClientReceiver-{addr}:{port}",
                            group: "ClientConnection",
                            work: async (_, ct) =>
                            {
                                // If TaskManager cancels via its own CTS, prefer that token; otherwise use loopToken forwarded.
                                System.Threading.CancellationToken effective = ct.CanBeCanceled ? ct : loopToken;
                                await _receiver.ReceiveLoopAsync(effective).ConfigureAwait(false);
                            },
                            options: new WorkerOptions { CancellationToken = loopToken, Tag = "ClientReceiver" }
                        );
                    }
                    catch (System.Exception ex)
                    {
                        InstanceManager.Instance.GetExistingInstance<ILogger>()?
                                                .Warn($"[SDK.{nameof(ReliableClient)}] schedule-receive-failed ex={ex.Message}");
                        // fallback to Task.Run
                        _ = System.Threading.Tasks.Task.Run(() => _receiver.ReceiveLoopAsync(loopToken), System.Threading.CancellationToken.None);
                    }

                    // Heartbeat: schedule recurring if configured
                    if (_options.KeepAliveIntervalMillis > 0)
                    {
                        System.TimeSpan interval = System.TimeSpan.FromMilliseconds(System.Math.Max(1, _options.KeepAliveIntervalMillis));
                        try
                        {
                            _heartbeatName = $"ClientHeartbeat-{addr}:{port}";

                            InstanceManager.Instance.GetOrCreateInstance<TaskManager>().ScheduleRecurring(
                                name: _heartbeatName,
                                interval: interval,
                                work: async (ct) =>
                                {
                                    System.Threading.CancellationToken effective = ct.CanBeCanceled ? ct : loopToken;
                                    try
                                    {
                                        await SendAsync(System.ReadOnlyMemory<System.Byte>.Empty, effective).ConfigureAwait(false);
                                    }
                                    catch (System.OperationCanceledException) when (effective.IsCancellationRequested)
                                    {
                                        // swallow - cancellation expected
                                    }
                                },
                                options: new RecurringOptions { NonReentrant = true, Tag = "ClientHeartbeat" }
                            );
                        }
                        catch (System.Exception ex)
                        {
                            InstanceManager.Instance.GetExistingInstance<ILogger>()?
                                                    .Warn($"[SDK.{nameof(ReliableClient)}] schedule-heartbeat-failed ex={ex.Message}");

                            _ = System.Threading.Tasks.Task.Run(() => HEARTBEAT_LOOP_ASYNC(loopToken), System.Threading.CancellationToken.None);
                        }
                    }

                    return;
                }
                catch (System.Exception ex) when (!(ex is System.OperationCanceledException && connectCts.IsCancellationRequested))
                {
                    lastEx = ex;
                    InstanceManager.Instance.GetExistingInstance<ILogger>()?
                                            .Warn($"[SDK.{nameof(ReliableClient)}] connect-failed addr={addr} ex={ex.Message}");

                    try { s.Dispose(); } catch { }
                    continue;
                }
            }

            throw lastEx ?? new System.Net.Sockets.SocketException((System.Int32)System.Net.Sockets.SocketError.HostNotFound);
        }
        finally
        {
            connectCts.Dispose();
        }
    }

    /// <summary>Disconnects the client and cancels background loops.</summary>
    /// <returns>A completed task when disconnect work is initiated.</returns>
    /// <remarks>
    /// This is a best-effort, synchronous-style disconnect that cancels background loops and
    /// disposes the underlying socket. It is safe to call multiple times.
    /// </remarks>
    public System.Threading.Tasks.Task DisconnectAsync()
    {
        if (_disposed)
        {
            return System.Threading.Tasks.Task.CompletedTask;
        }

        lock (_sync)
        {
            try { _loopCts?.Cancel(); } catch { }
            try { _socket?.Shutdown(System.Net.Sockets.SocketShutdown.Both); } catch { }
            try { _socket?.Close(); _socket?.Dispose(); } catch { }
            _socket = null;
        }

        // Inform TaskManager to cancel recurring if we created it by name (best-effort).
        try
        {
            if (_heartbeatName is not null)
            {
                _ = InstanceManager.Instance.GetOrCreateInstance<TaskManager>()
                                            .CancelRecurring(_heartbeatName);
            }
            if (_rateSamplerName is not null)
            {
                _ = InstanceManager.Instance.GetOrCreateInstance<TaskManager>()
                                            .CancelRecurring(_rateSamplerName);
            }
            if (_receiveHandle is not null)
            {
                _ = InstanceManager.Instance.GetOrCreateInstance<TaskManager>()
                                            .CancelWorker(_receiveHandle.Id);
            }
        }
        catch { /* best-effort cleanup, ignore errors */ }

        InstanceManager.Instance.GetExistingInstance<ILogger>()?
                                .Info($"[SDK.{nameof(ReliableClient)}] disconnected (requested)");

        OnDisconnected?.Invoke(this, null);
        return System.Threading.Tasks.Task.CompletedTask;
    }

    /// <summary>
    /// Sends a framed payload (header + payload) asynchronously.
    /// </summary>
    /// <param name="payload">Payload bytes to send (payload only, header is added by the protocol).</param>
    /// <param name="cancellationToken">Cancellation token to cancel the send operation.</param>
    /// <returns>Task that resolves to <c>true</c> if the send succeeded, otherwise <c>false</c>.</returns>
    /// <exception cref="System.ObjectDisposedException">Thrown if the client has been disposed.</exception>
    /// <exception cref="System.InvalidOperationException">Thrown if the client is not connected.</exception>
    public System.Threading.Tasks.Task<System.Boolean> SendAsync(System.ReadOnlyMemory<System.Byte> payload, System.Threading.CancellationToken cancellationToken = default)
    {
        System.ObjectDisposedException.ThrowIf(this._disposed, nameof(ReliableClient));
        return _sender is null
            ? throw new System.InvalidOperationException("Client not connected.")
            : _sender.SendAsync(payload, cancellationToken);
    }

    /// <summary>
    /// Helper to send an <see cref="IPacket"/> asynchronously (serializes then sends).
    /// </summary>
    /// <param name="packet">The packet to serialize and send.</param>
    /// <param name="cancellationToken">Cancellation token to cancel the send operation.</param>
    /// <returns>Task that resolves to <c>true</c> if the send succeeded, otherwise <c>false</c>.</returns>
    /// <exception cref="System.ArgumentNullException">Thrown when <paramref name="packet"/> is null.</exception>
    /// <exception cref="System.ObjectDisposedException">Thrown if the client has been disposed.</exception>
    /// <exception cref="System.InvalidOperationException">Thrown if the client is not connected.</exception>
    public System.Threading.Tasks.Task<System.Boolean> SendAsync(IPacket packet, System.Threading.CancellationToken cancellationToken = default)
    {
        System.ObjectDisposedException.ThrowIf(this._disposed, nameof(ReliableClient));
        return packet is null
            ? throw new System.ArgumentNullException(nameof(packet))
            : _sender is null ? throw new System.InvalidOperationException("Client not connected.") : _sender.SendAsync(packet, cancellationToken);
    }

    /// <summary>
    /// Dispose the client and release all resources.
    /// </summary>
    /// <remarks>
    /// After calling <see cref="Dispose"/>, the instance is no longer usable.
    /// This method is idempotent and safe to call multiple times.
    /// </remarks>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;

        lock (_sync)
        {
            try { _loopCts?.Cancel(); } catch { }
            try { _socket?.Shutdown(System.Net.Sockets.SocketShutdown.Both); } catch { }
            try { _socket?.Close(); _socket?.Dispose(); } catch { }
            _socket = null;

            try { _loopCts?.Dispose(); } catch { }
            _loopCts = null;
        }

        // Try to clean up scheduled tasks (best-effort)
        try
        {
            if (_heartbeatName is not null)
            {
                _ = InstanceManager.Instance.GetOrCreateInstance<TaskManager>()
                                            .CancelRecurring(_heartbeatName);
            }
            if (_rateSamplerName is not null)
            {
                _ = InstanceManager.Instance.GetOrCreateInstance<TaskManager>()
                                            .CancelRecurring(_rateSamplerName);
            }
            if (_receiveHandle is not null)
            {
                _ = InstanceManager.Instance.GetOrCreateInstance<TaskManager>()
                                            .CancelWorker(_receiveHandle.Id);
            }
        }
        catch { }

        InstanceManager.Instance.GetExistingInstance<ILogger>()?
                                .Info($"[SDK.{nameof(ReliableClient)}] disposed");

        System.GC.SuppressFinalize(this);
    }

    #endregion Public API

    #region Internal helpers and callbacks

    private void REPORT_BYTE_SSENT(System.Int32 count)
    {
        System.Threading.Interlocked.Add(ref _bytesSent, count);
        System.Threading.Interlocked.Add(ref _sendCounterForInterval, count);
        try { OnBytesSent?.Invoke(this, count); } catch { }
    }

    private void REPORT_BYTES_RECEIVED(System.Int32 count)
    {
        System.Threading.Interlocked.Add(ref _bytesReceived, count);
        System.Threading.Interlocked.Add(ref _receiveCounterForInterval, count);
        try { OnBytesReceived?.Invoke(this, count); } catch { }
    }

    private void HANDLE_SEND_ERROR(System.Exception ex)
    {
        try { OnError?.Invoke(this, ex); } catch { }
        _ = HANDLE_DISCONNECT_AND_RECONNECT_ASYNC(ex);
    }

    private void HANDLE_RECEIVE_ERROR(System.Exception ex)
    {
        try { OnError?.Invoke(this, ex); } catch { }
        _ = HANDLE_DISCONNECT_AND_RECONNECT_ASYNC(ex);
    }

    private void HANDLE_RECEIVE_MESSAGE(IBufferLease lease) => OnMessageReceived?.Invoke(this, lease);

    private async System.Threading.Tasks.Task HEARTBEAT_LOOP_ASYNC(System.Threading.CancellationToken token)
    {
        System.Int32 interval = System.Math.Max(1, _options.KeepAliveIntervalMillis);
        while (!token.IsCancellationRequested)
        {
            try
            {
                await System.Threading.Tasks.Task.Delay(interval, token).ConfigureAwait(false);
                await SendAsync(System.ReadOnlyMemory<System.Byte>.Empty, token).ConfigureAwait(false);
            }
            catch (System.OperationCanceledException)
            {
                break;
            }
            catch (System.Exception ex)
            {
                InstanceManager.Instance.GetExistingInstance<ILogger>()?
                                        .Warn($"[SDK.{nameof(ReliableClient)}:{nameof(HEARTBEAT_LOOP_ASYNC)}] heartbeat-error {ex.Message}");

                try
                {
                    OnError?.Invoke(this, ex);
                }
                catch { }

                _ = HANDLE_DISCONNECT_AND_RECONNECT_ASYNC(ex);
                break;
            }
        }
    }

    private async System.Threading.Tasks.Task RATE_SAMPLER_TICK(System.Threading.CancellationToken token)
    {
        try
        {
            // one tick: exchange counters and store last-bps
            System.Int64 sent = System.Threading.Interlocked.Exchange(ref _sendCounterForInterval, 0);
            System.Int64 recv = System.Threading.Interlocked.Exchange(ref _receiveCounterForInterval, 0);
            System.Threading.Interlocked.Exchange(ref _lastSendBps, sent);
            System.Threading.Interlocked.Exchange(ref _lastReceiveBps, recv);
            await System.Threading.Tasks.Task.CompletedTask.ConfigureAwait(false);
        }
        catch (System.OperationCanceledException) when (token.IsCancellationRequested)
        {
            // expected cancellation
        }
        catch (System.Exception ex)
        {
            InstanceManager.Instance.GetExistingInstance<ILogger>()?
                                    .Warn($"[SDK.{nameof(ReliableClient)}:{nameof(RATE_SAMPLER_TICK)}] sampler-error {ex.Message}");

            try
            {
                OnError?.Invoke(this, ex);
            }
            catch { }
        }
    }

    private async System.Threading.Tasks.Task HANDLE_DISCONNECT_AND_RECONNECT_ASYNC(System.Exception cause)
    {
        try
        {
            OnDisconnected?.Invoke(this, cause);
        }
        catch { }

        lock (_sync)
        {
            try { _loopCts?.Cancel(); } catch { }
            try { _socket?.Shutdown(System.Net.Sockets.SocketShutdown.Both); } catch { }
            try { _socket?.Close(); _socket?.Dispose(); } catch { }
            _socket = null;
        }

        if (!_options.ReconnectEnabled || _disposed)
        {
            return;
        }

        if (System.String.IsNullOrEmpty(_host) || _port == 0)
        {
            InstanceManager.Instance.GetExistingInstance<ILogger>()?
                                    .Info($"[SDK.{nameof(ReliableClient)}] no saved endpoint; skipping auto-reconnect");
            return;
        }

        System.Int32 attempt = 0;
        System.Int64 delay = System.Math.Max(1, _options.ReconnectBaseDelayMillis);
        while (!_disposed && (_options.ReconnectMaxAttempts == 0 || attempt < _options.ReconnectMaxAttempts))
        {
            attempt++;
            try
            {
                InstanceManager.Instance.GetExistingInstance<ILogger>()?
                                        .Info($"[SDK.{nameof(ReliableClient)}] reconnect attempt={attempt} delay={delay}ms");

                await System.Threading.Tasks.Task.Delay((System.Int32)delay).ConfigureAwait(false);

                await ConnectAsync(_host, _port).ConfigureAwait(false);
                InstanceManager.Instance.GetExistingInstance<ILogger>()?
                                        .Info($"[SDK.{nameof(ReliableClient)}] reconnect-success attempt={attempt}");

                return;
            }
            catch (System.Exception ex)
            {
                InstanceManager.Instance.GetExistingInstance<ILogger>()?
                                        .Warn($"[SDK.{nameof(ReliableClient)}] reconnect-failed attempt={attempt} ex={ex.Message}");

                try
                {
                    OnError?.Invoke(this, ex);
                }
                catch { }

                delay = System.Math.Min(_options.ReconnectMaxDelayMillis, delay * 2);
                continue;
            }
        }

        InstanceManager.Instance.GetExistingInstance<ILogger>()?
                                .Info($"[SDK.{nameof(ReliableClient)}] reconnect attempts exhausted");
    }

    private System.Net.Sockets.Socket GET_CONNECTED_SOCKET_OR_THROW()
    {
        System.Net.Sockets.Socket s = _socket;
        return s?.Connected != true ? throw new System.InvalidOperationException("Client not connected.") : s;
    }

    #endregion Internal helpers and callbacks
}