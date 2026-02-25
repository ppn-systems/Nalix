// Copyright (c) 2026 PPN Corporation. All rights reserved.

using Nalix.Common.Concurrency;
using Nalix.Common.Diagnostics;
using Nalix.Common.Infrastructure.Caching;
using Nalix.Common.Infrastructure.Client;
using Nalix.Common.Messaging.Packets;
using Nalix.Common.Messaging.Packets.Abstractions;
using Nalix.Framework.Configuration;
using Nalix.Framework.Injection;
using Nalix.Framework.Options;
using Nalix.Framework.Tasks;
using Nalix.SDK.Configuration;
using Nalix.Shared.Memory.Buffers;
using Nalix.Shared.Memory.Pooling;

namespace Nalix.SDK.Transport;

/// <summary>
/// UDP client transport. Designed to match the style and lifecycle of ReliableClient:
/// - ConnectAsync/DisconnectAsync
/// - Receive loop scheduled as TaskManager worker
/// - Rate sampler scheduled as TaskManager recurring job
/// - Use BufferPoolManager and BufferLease for received payloads
/// </summary>
public sealed class UnreliableClient : IClientConnection
{
    #region Fields

    private readonly System.Threading.Lock _sync = new();
    private readonly BufferPoolManager _bufferPool;

    // Socket + control
    private System.Net.Sockets.UdpClient _udpClient;
    private System.Net.IPEndPoint _remoteEndPoint;
    private System.Threading.CancellationTokenSource _loopCts;

    // TaskManager integration
    private IWorkerHandle _receiveHandle;
    private IRecurringHandle _rateSamplerHandle;
    private System.String _rateSamplerName;

    // State & counters
    private volatile System.Boolean _disposed;
    private System.Int64 _bytesSent;
    private System.Int64 _bytesReceived;

    // Rate sampling counters
    private System.Int64 _sendCounterForInterval;
    private System.Int64 _receiveCounterForInterval;
    private System.Int64 _lastSendBps;
    private System.Int64 _lastReceiveBps;

    #endregion Fields

    #region Events

    /// <inheritdoc/>
    public event System.EventHandler OnConnected;

    /// <inheritdoc/>
    public event System.EventHandler<System.Exception> OnDisconnected;

    /// <inheritdoc/>
    public event System.EventHandler<IBufferLease> OnMessageReceived;

    /// <inheritdoc/>
    public event System.EventHandler<System.Int64> OnBytesSent;

    /// <inheritdoc/>
    public event System.EventHandler<System.Int64> OnBytesReceived;

    /// <inheritdoc/>
    public event System.EventHandler<System.Exception> OnError;

    #endregion Events

    #region Properties

    /// <inheritdoc/>
    public readonly TransportOptions Options;

    /// <inheritdoc/>
    ITransportOptions IClientConnection.Options => this.Options;

    /// <inheritdoc/>
    public System.Boolean IsConnected => _udpClient != null && !_disposed;

    /// <inheritdoc/>
    public System.Int64 BytesSent => System.Threading.Interlocked.Read(ref _bytesSent);

    /// <inheritdoc/>
    public System.Int64 BytesReceived => System.Threading.Interlocked.Read(ref _bytesReceived);

    /// <inheritdoc/>
    public System.Int64 SendBytesPerSecond => System.Threading.Interlocked.Read(ref _lastSendBps);

    /// <inheritdoc/>
    public System.Int64 ReceiveBytesPerSecond => System.Threading.Interlocked.Read(ref _lastReceiveBps);

    #endregion Properties

    #region Constructors

    /// <summary>
    /// Construct UnreliableClient using TransportOptions from ConfigurationManager if available.
    /// </summary>
    public UnreliableClient()
    {
        try
        {
            Options = ConfigurationManager.Instance.Get<TransportOptions>();
        }
        catch
        {
            Options = new TransportOptions
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

        _bufferPool = InstanceManager.Instance.GetOrCreateInstance<BufferPoolManager>();

        if (InstanceManager.Instance.GetExistingInstance<IPacketCatalog>() == null)
        {
            InstanceManager.Instance.GetExistingInstance<ILogger>()?
                                    .Error($"[SDK.{nameof(ReliableClient)}] no IPacketCatalog instance found; this is a fatal configuration error. The process will terminate.");

            // Fail fast with a clear message so operator/collector can see cause.
            System.Environment.FailFast($"[SDK.{nameof(ReliableClient)}] missing required service: IPacketCatalog. Terminating process.");
        }
    }

    #endregion Constructors

    #region Public API

    /// <inheritdoc/>
    public async System.Threading.Tasks.Task ConnectAsync(System.String host = null, System.UInt16? port = null, System.Threading.CancellationToken cancellationToken = default)
    {
        port ??= this.Options.Port;
        host ??= this.Options.Address;

        if (System.String.IsNullOrWhiteSpace(host))
        {
            throw new System.ArgumentNullException(nameof(host));
        }

        if (port is <= 0 or > 65535)
        {
            throw new System.ArgumentOutOfRangeException(nameof(port));
        }

        System.ObjectDisposedException.ThrowIf(_disposed, nameof(UnreliableClient));

        // store remote
        _remoteEndPoint = new System.Net.IPEndPoint(await RESOLVE_HOST_ASYNC(host, cancellationToken).ConfigureAwait(false), (System.Int32)port);

        lock (_sync)
        {
            // cancel any existing loops
            if (_loopCts?.IsCancellationRequested == false)
            {
                try { _loopCts.Cancel(); } catch { }
            }

            // create UdpClient bound to ephemeral port
            try
            {
                _udpClient?.Close();
                _udpClient?.Dispose();
            }
            catch { }
            _udpClient = new System.Net.Sockets.UdpClient(0);
            try
            {
                if (Options != null)
                {
                    _udpClient.Client.SendBufferSize = Options.BufferSize;
                    _udpClient.Client.ReceiveBufferSize = Options.BufferSize;
                }
            }
            catch { }

            _loopCts = new System.Threading.CancellationTokenSource();
        }

        InstanceManager.Instance.GetExistingInstance<ILogger>()?
                                .Info($"[SDK.{nameof(UnreliableClient)}] connected remote={_remoteEndPoint}");

        OnConnected?.Invoke(this, System.EventArgs.Empty);

        System.Threading.CancellationToken loopToken = _loopCts.Token;

        // Schedule receive loop via TaskManager
        try
        {
            _receiveHandle = InstanceManager.Instance.GetOrCreateInstance<TaskManager>().ScheduleWorker(
                name: $"UdpReceiver-{_remoteEndPoint}",
                group: "ClientConnection",
                work: async (_, ct) =>
                {
                    System.Threading.CancellationToken effective = ct.CanBeCanceled ? ct : loopToken;
                    await RECEIVE_LOOP_ASYNC(effective).ConfigureAwait(false);
                },
                options: new WorkerOptions { CancellationToken = loopToken, Tag = "UdpReceiver" }
            );
        }
        catch (System.Exception ex)
        {
            InstanceManager.Instance.GetExistingInstance<ILogger>()?
                                    .Warn($"[SDK.{nameof(UnreliableClient)}] schedule-receive-failed ex={ex.Message}");

            // fallback: start as Task.Run only if TaskManager unavailable (retain behavior similar to ReliableClient)
            _ = System.Threading.Tasks.Task.Run(() => RECEIVE_LOOP_ASYNC(loopToken), System.Threading.CancellationToken.None);
        }

        // Start sampler as recurring job (1s)
        try
        {
            _rateSamplerName = $"UdpRateSampler-{_remoteEndPoint}";
            _rateSamplerHandle = InstanceManager.Instance.GetOrCreateInstance<TaskManager>().ScheduleRecurring(
                name: _rateSamplerName,
                interval: System.TimeSpan.FromMilliseconds(1000),
                work: async (ct) =>
                {
                    System.Threading.CancellationToken effective = ct.CanBeCanceled ? ct : loopToken;
                    await RATE_SAMPLER_TICK_ASYNC(effective).ConfigureAwait(false);
                },
                options: new RecurringOptions { NonReentrant = true, Tag = "UdpRateSampler" }
            );
        }
        catch (System.Exception ex)
        {
            InstanceManager.Instance.GetExistingInstance<ILogger>()?
                                    .Warn($"[SDK.{nameof(UnreliableClient)}] schedule-rate-sampler-failed ex={ex.Message}");
            // per earlier requirement: prefer TaskManager; do not fallback
        }
    }

    /// <inheritdoc/>
    public System.Threading.Tasks.Task DisconnectAsync()
    {
        if (_disposed)
        {
            return System.Threading.Tasks.Task.CompletedTask;
        }

        lock (_sync)
        {
            try { _loopCts?.Cancel(); } catch { }
            try { _udpClient?.Close(); _udpClient?.Dispose(); } catch { }
            _udpClient = null;
        }

        // cancel scheduled jobs by name (best-effort)
        try
        {
            if (_rateSamplerName is not null)
            {
                _ = InstanceManager.Instance.GetOrCreateInstance<TaskManager>().CancelRecurring(_rateSamplerName);
            }
            if (_receiveHandle is not null)
            {
                _ = InstanceManager.Instance.GetOrCreateInstance<TaskManager>().CancelWorker(_receiveHandle.Id);
            }
        }
        catch { }

        InstanceManager.Instance.GetExistingInstance<ILogger>()?
                                .Info($"[SDK.{nameof(UnreliableClient)}] disconnected (requested)");

        OnDisconnected?.Invoke(this, null);
        return System.Threading.Tasks.Task.CompletedTask;
    }

    /// <inheritdoc/>
    public async System.Threading.Tasks.Task<System.Boolean> SendAsync(IPacket packet, System.Threading.CancellationToken cancellationToken = default)
    {
        System.ArgumentNullException.ThrowIfNull(packet);

        System.ObjectDisposedException.ThrowIf(_disposed, nameof(UnreliableClient));
        if (_udpClient is null)
        {
            throw new System.InvalidOperationException("Client not connected.");
        }

        try
        {
            if (packet.Length == 0)
            {
                return await SendAsync(System.ReadOnlyMemory<System.Byte>.Empty, cancellationToken).ConfigureAwait(false);
            }

            // Try to avoid extra allocs when possible
            if (packet.Length < 512)
            {
                var tmp = new System.Byte[packet.Length];
                System.Int32 w = packet.Serialize(tmp);
                await _udpClient.SendAsync(tmp, w, _remoteEndPoint).ConfigureAwait(false);
                REPORT_BYTES_SENT(w);
                return true;
            }
            else
            {
                var rent = _bufferPool.Rent(packet.Length);
                try
                {
                    System.Int32 w = packet.Serialize(rent);
                    await _udpClient.SendAsync(rent, w, _remoteEndPoint).ConfigureAwait(false);
                    REPORT_BYTES_SENT(w);
                    return true;
                }
                finally
                {
                    _bufferPool.Return(rent);
                }
            }
        }
        catch (System.Exception ex)
        {
            try { OnError?.Invoke(this, ex); } catch { }
            return false;
        }
    }

    /// <inheritdoc/>
    public async System.Threading.Tasks.Task<System.Boolean> SendAsync(System.ReadOnlyMemory<System.Byte> payload, System.Threading.CancellationToken cancellationToken = default)
    {
        System.ObjectDisposedException.ThrowIf(_disposed, nameof(UnreliableClient));
        if (_udpClient is null)
        {
            throw new System.InvalidOperationException("Client not connected.");
        }

        try
        {
            if (payload.Length == 0)
            {
                await _udpClient.SendAsync([], 0, _remoteEndPoint).ConfigureAwait(false);
                REPORT_BYTES_SENT(0);
                return true;
            }

            // minimize allocations for small payloads
            if (MEMORY_MARSHAL_TRY_GET_ARRAY(payload, out System.Byte[] arr))
            {
                await _udpClient.SendAsync(arr, arr.Length, _remoteEndPoint).ConfigureAwait(false);
                REPORT_BYTES_SENT(arr.Length);
                return true;
            }
            else
            {
                System.Byte[] tmp = payload.ToArray();
                await _udpClient.SendAsync(tmp, tmp.Length, _remoteEndPoint).ConfigureAwait(false);
                REPORT_BYTES_SENT(tmp.Length);
                return true;
            }
        }
        catch (System.Exception ex)
        {
            try { OnError?.Invoke(this, ex); } catch { }
            return false;
        }
    }

    /// <inheritdoc/>
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
            try { _udpClient?.Close(); } catch { }
            try { _loopCts?.Dispose(); } catch { }
            try { _udpClient?.Dispose(); } catch { }

            _loopCts = null;
            _udpClient = null;
        }

        // Try to cancel scheduled TaskManager jobs
        try
        {
            if (_rateSamplerName is not null)
            {
                _ = InstanceManager.Instance.GetOrCreateInstance<TaskManager>().CancelRecurring(_rateSamplerName);
            }
            if (_receiveHandle is not null)
            {
                _ = InstanceManager.Instance.GetOrCreateInstance<TaskManager>().CancelWorker(_receiveHandle.Id);
            }
        }
        catch { }

        InstanceManager.Instance.GetExistingInstance<ILogger>()?
                                .Info($"[SDK.{nameof(UnreliableClient)}] disposed");

        System.GC.SuppressFinalize(this);
    }

    #endregion Public API

    #region Internal helpers

    private void REPORT_BYTES_SENT(System.Int32 count)
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

    private async System.Threading.Tasks.Task RATE_SAMPLER_TICK_ASYNC(System.Threading.CancellationToken token)
    {
        try
        {
            System.Int64 sent = System.Threading.Interlocked.Exchange(ref _sendCounterForInterval, 0);
            System.Int64 recv = System.Threading.Interlocked.Exchange(ref _receiveCounterForInterval, 0);
            System.Threading.Interlocked.Exchange(ref _lastSendBps, sent);
            System.Threading.Interlocked.Exchange(ref _lastReceiveBps, recv);
            await System.Threading.Tasks.Task.CompletedTask.ConfigureAwait(false);
        }
        catch (System.OperationCanceledException) when (token.IsCancellationRequested) { }
        catch (System.Exception ex)
        {
            InstanceManager.Instance.GetExistingInstance<ILogger>()?
                                    .Warn($"[SDK.{nameof(UnreliableClient)}:{nameof(RATE_SAMPLER_TICK_ASYNC)}] sampler-error {ex.Message}");

            try { OnError?.Invoke(this, ex); } catch { }
        }
    }

    private static System.Boolean MEMORY_MARSHAL_TRY_GET_ARRAY(System.ReadOnlyMemory<System.Byte> memory, out System.Byte[] array)
    {
        // Try to avoid copying when the underlying memory is an array segment
        if (System.Runtime.InteropServices.MemoryMarshal.TryGetArray(memory, out System.ArraySegment<System.Byte> seg))
        {
            array = seg.Array!;
            return true;
        }
        array = null;
        return false;
    }

    private async System.Threading.Tasks.Task RECEIVE_LOOP_ASYNC(System.Threading.CancellationToken token)
    {
        System.Net.Sockets.UdpClient client;
        try
        {
            client = _udpClient ?? throw new System.InvalidOperationException("Client not connected.");
        }
        catch (System.Exception ex)
        {
            InstanceManager.Instance.GetExistingInstance<ILogger>()?
                                    .Error($"[SDK.{nameof(UnreliableClient)}] receive-start-error {ex.Message}", ex);

            try { OnError?.Invoke(this, ex); } catch { }
            return;
        }

        try
        {
            while (!token.IsCancellationRequested)
            {
                // UdpClient.ReceiveAsync does not accept CancellationToken, so use Task.WhenAny
                System.Threading.Tasks.Task<System.Net.Sockets.UdpReceiveResult> receiveTask = client.ReceiveAsync();
                System.Threading.Tasks.Task completed = await System.Threading.Tasks.Task.WhenAny(receiveTask, System.Threading.Tasks.Task.Delay(System.Threading.Timeout.Infinite, token)).ConfigureAwait(false);

                if (completed != receiveTask)
                {
                    // cancellation requested
                    break;
                }

                System.Net.Sockets.UdpReceiveResult result;
                try
                {
                    result = receiveTask.Result; // completed
                }
                catch (System.Exception ex)
                {
                    InstanceManager.Instance.GetExistingInstance<ILogger>()?
                                            .Warn($"[SDK.{nameof(UnreliableClient)}] receive failed: {ex.Message}");

                    try { OnError?.Invoke(this, ex); } catch { }
                    if (token.IsCancellationRequested)
                    {
                        break;
                    }

                    continue;
                }

                // Copy received bytes into pooled buffer then wrap into BufferLease
                System.Byte[] recvBuf = result.Buffer;
                System.Int32 len = recvBuf.Length;
                if (len == 0)
                {
                    continue;
                }

                System.Byte[] rented = _bufferPool.Rent(len);
                System.Boolean ownershipTransferred = false;
                try
                {
                    System.Buffer.BlockCopy(recvBuf, 0, rented, 0, len);

                    // Report bytes received (headerless for UDP)
                    REPORT_BYTES_RECEIVED(len);

                    // Wrap into BufferLease and hand off starting at 0
                    BufferLease lease = BufferLease.TakeOwnership(rented, 0, len);
                    ownershipTransferred = true;

                    try
                    {
                        OnMessageReceived?.Invoke(this, lease);
                    }
                    catch (System.Exception)
                    {
                        // ensure lease is freed on handler exception
                        try { lease.Dispose(); } catch { }
                        throw;
                    }

                    // responsibility to Dispose lease remains with handlers
                }
                catch
                {
                    if (!ownershipTransferred)
                    {
                        try { _bufferPool.Return(rented); } catch { }
                    }
                    throw;
                }
            }
        }
        catch (System.OperationCanceledException) when (token.IsCancellationRequested)
        {
            // normal cancellation
        }
        catch (System.Exception ex)
        {
            InstanceManager.Instance.GetExistingInstance<ILogger>()?
                                    .Error($"[SDK.{nameof(UnreliableClient)}:{nameof(RECEIVE_LOOP_ASYNC)}] faulted msg={ex.Message}", ex);

            try { OnError?.Invoke(this, ex); } catch { }
        }
    }

    private static async System.Threading.Tasks.Task<System.Net.IPAddress> RESOLVE_HOST_ASYNC(System.String host, System.Threading.CancellationToken ct)
    {
        System.Net.IPAddress[] addrs = await System.Net.Dns.GetHostAddressesAsync(host, ct).ConfigureAwait(false);
        return addrs == null || addrs.Length == 0 ? throw new System.Net.Sockets.SocketException((System.Int32)System.Net.Sockets.SocketError.HostNotFound) : addrs[0];
    }

    #endregion Internal helpers
}