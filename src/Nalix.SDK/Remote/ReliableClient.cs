// Copyright (c) 2025 PPN Corporation. All rights reserved.

using Nalix.Common.Abstractions;
using Nalix.Common.Concurrency;
using Nalix.Common.Diagnostics;
using Nalix.Common.Enums;
using Nalix.Common.Infrastructure.Client;
using Nalix.Common.Messaging.Packets;
using Nalix.Common.Messaging.Packets.Abstractions;
using Nalix.Framework.Configuration;
using Nalix.Framework.Injection;
using Nalix.Framework.Options;
using Nalix.Framework.Tasks;
using Nalix.SDK.Remote.Configuration;
using Nalix.SDK.Remote.Internal;
using System.Linq;

namespace Nalix.SDK.Remote;

/// <inheritdoc/>
[System.Diagnostics.DebuggerDisplay("Remote={Options.Address}:{Options.Port}, Connected={IsConnected}")]
public sealed class ReliableClient : IReliableClient
{
    #region Constants

    // Suggested constants to add to the ReliableClient class.
    // Place these near the top of the class (e.g. after the Fields region).
    private const System.Int32 DefaultConnectTimeoutMs = 30_000;           // default ConnectAsync timeout (ms)
    private const System.Int32 StreamReadTimeoutMs = 10_000;               // NetworkStream.ReadTimeout (ms)
    private const System.Int32 StreamWriteTimeoutMs = 10_000;              // NetworkStream.WriteTimeout (ms)

    private const System.Int32 ChannelCapacity = 1024;                     // bounded channel capacity
    private const System.Int32 SenderBatchDelayMs = 1;                     // opportunistic batch delay (ms)
    private const System.Int32 SenderMaxBatchCount = 64;                   // max packets per batch
    private const System.Int32 SenderMaxBatchBytes = 64 * 1024;            // max bytes per batch (64 KB)
    private const System.Int32 FrameHeaderSize = 2;                        // frame header size (U16 length)

    private const System.Int32 SocketBufferSize = 8192;                    // socket send/recv buffer sizes
    private const System.Int32 KeepAliveTimeMs = 20_000;                   // keepalive idle (ms)
    private const System.Int32 KeepAliveIntervalMs = 5_000;                // keepalive interval (ms)

    private const System.Int32 WorkerIdArrayLength = 3;                    // size of _workerId array
    private const System.Int32 LingerTimeoutSeconds = 0;                   // linger option timeout (seconds)

    #endregion Constants

    #region Fields

    private readonly System.Threading.SemaphoreSlim _connGate;

    private readonly ISnowflake[] _workerId;
    private System.Net.Sockets.TcpClient _client;
    private System.Net.Sockets.NetworkStream _stream;

    private FRAME_READER<IPacket> _inbound;
    private FRAME_SENDER<IPacket> _outbound;

    private volatile System.Boolean _closed;
    private volatile System.Boolean _ioHealthy;

    private System.Int32 _discNotified; // 0/1 gate for Disconnected

    // New: bounded channels for inbound/outbound
    private System.Threading.Channels.Channel<IPacket> _recvChannel;
    private System.Threading.Channels.Channel<System.ReadOnlyMemory<System.Byte>> _sendChannel;

    #endregion Fields

    #region Properties

    // Optional dispatcher instance (user may create their own and subscribe)
    /// <summary>
    /// Gets or sets the optional dispatcher instance for handling received packets.
    /// Users may assign their own dispatcher and subscribe to packet events.
    /// </summary>
    public IReliableDispatcher Dispatcher;

    /// <summary>
    /// Gets the context associated with the network connection.
    /// </summary>
    public ITransportOptions Options { get; }

    // In ReliableClient class
    /// <summary>
    /// Indicates whether a 32-byte session key has been installed (handshake completed).
    /// </summary>
    public System.Boolean IsHandshaked => Options?.EncryptionKey is { Length: 32 };

    /// <summary>
    /// Gets a value indicating whether the client is connected to the server.
    /// </summary>
    [System.Diagnostics.CodeAnalysis.MemberNotNullWhen(true, nameof(_stream), nameof(_outbound), nameof(_inbound))]
    public System.Boolean IsConnected
    {
        get
        {
            if (_closed || !_ioHealthy || _stream is null)
            {
                return false;
            }

            try
            {
                return _stream.CanRead && _stream.CanWrite;
            }
            catch (System.ObjectDisposedException)
            {
                return false;
            }
        }
    }

    #endregion Properties

    #region Events

    /// <summary>
    /// Raised after a successful connection is established.
    /// Executed on the calling thread of ConnectAsync.
    /// </summary>
    public event System.Action Connected;

    /// <summary>
    /// Raised whenever a packet is received on the background network worker.
    /// Executed on a background thread; do not touch Unity API here.
    /// </summary>
    public event System.Action<IPacket> PacketReceived;

    /// <summary>
    /// Raised when the connection is closed or the receive loop exits due to an error.
    /// Executed on a background thread; ex is null for normal Dispose().
    /// </summary>
    public event System.Action<System.Exception> Disconnected;

    #endregion Events

    #region Constructor

    /// <summary>
    /// Initializes a new instance of the <see cref="ReliableClient"/> class.
    /// </summary>
    public ReliableClient()
    {
        _connGate = new(1, 1);
        _workerId = new ISnowflake[WorkerIdArrayLength]; // extra slot for sender/recv consumer
        _client = new System.Net.Sockets.TcpClient { NoDelay = true };

        this.Options = ConfigurationManager.Instance.Get<TransportOptions>();
    }

    #endregion Constructor

    #region APIs

    /// <inheritdoc/>
    [System.Diagnostics.DebuggerStepThrough]
    [System.Runtime.CompilerServices.SkipLocalsInit]
    [System.Diagnostics.CodeAnalysis.MemberNotNull(nameof(_stream))]
    [System.Diagnostics.CodeAnalysis.MemberNotNull(nameof(_inbound))]
    [System.Diagnostics.CodeAnalysis.MemberNotNull(nameof(_outbound))]
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Roslynator", "RCS1163:Unused parameter", Justification = "<Pending>")]
    public async System.Threading.Tasks.Task ConnectAsync(
        [System.Diagnostics.CodeAnalysis.NotNull] System.Int32 timeout = DefaultConnectTimeoutMs,
        [System.Diagnostics.CodeAnalysis.NotNull] System.Threading.CancellationToken cancellationToken = default)
    {
        _closed = false;
        _ioHealthy = true;
        _discNotified = 0;

        _client?.Close();
        CONFIGURE_SOCKET(_client);

        using System.Threading.CancellationTokenSource cts =
            System.Threading.CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

        cts.CancelAfter(timeout);

        await _connGate.WaitAsync(cts.Token).ConfigureAwait(false);

        try
        {
            if (IsConnected)
            {
                return;
            }

            await _client.ConnectAsync(Options.Address, Options.Port, cts.Token);

            _stream = _client.GetStream();

            _stream.ReadTimeout = StreamReadTimeoutMs;
            _stream.WriteTimeout = StreamWriteTimeoutMs;

            _inbound = new FRAME_READER<IPacket>(_stream);
            _outbound = new FRAME_SENDER<IPacket>(_stream);

            // Initialize bounded channels
            // Receive channel: bounded, wait when full -> backpressure
            _recvChannel = System.Threading.Channels.Channel.CreateBounded<IPacket>(new System.Threading.Channels.BoundedChannelOptions(ChannelCapacity)
            {
                SingleReader = true,
                SingleWriter = false,
                FullMode = System.Threading.Channels.BoundedChannelFullMode.Wait
            });

            // Send channel: bounded, wait when full -> backpressure to sender
            _sendChannel = System.Threading.Channels.Channel.CreateBounded<System.ReadOnlyMemory<System.Byte>>(new System.Threading.Channels.BoundedChannelOptions(ChannelCapacity)
            {
                SingleReader = true,
                SingleWriter = false,
                FullMode = System.Threading.Channels.BoundedChannelFullMode.Wait
            });

            // Provide a default dispatcher (optional)
            Dispatcher = new ReliableDispatcher();

            // Notify connected
            SAFE_INVOKE(Connected, InstanceManager.Instance.GetExistingInstance<ILogger>());

            // Start background receive network worker through TaskManager (same as before but enqueue to _recvChannel)
            IWorkerHandle recvNetWorker = InstanceManager.Instance.GetOrCreateInstance<TaskManager>().ScheduleWorker(
                name: $"tcp-recv-net-{Options.Address}:{Options.Port}",
                group: "network",
                async (ctx, ct) =>
                {
                    while (!ct.IsCancellationRequested && IsConnected)
                    {
                        IPacket packet = null;

                        try
                        {
                            packet = await _inbound!.RECEIVE_ASYNC(ct).ConfigureAwait(false);
                        }
                        catch (System.OperationCanceledException)
                        {
                            break;
                        }
                        catch (System.Exception ex)
                        {
                            InstanceManager.Instance.GetExistingInstance<ILogger>()?
                                                    .Error($"[SDK.ReliableClient.GM] Network receive loop error: {ex.Message}");

                            if (packet != null)
                            {
                                // best-effort dispatch before error handling
                                _ = _recvChannel.Writer.WaitToWriteAsync(ct).AsTask().ContinueWith(t => _recvChannel.Writer.TryWrite(packet));
                            }

                            if (System.Threading.Interlocked.Exchange(ref _discNotified, 1) == 0)
                            {
                                SAFE_INVOKE(Disconnected, ex, InstanceManager.Instance.GetExistingInstance<ILogger>());
                            }

                            this.MARK_IO_DEAD(ex);
                            this.Disconnect();
                            break;
                        }

                        // Enqueue to bounded channel (wait if full) to provide backpressure
                        try
                        {
                            await _recvChannel.Writer.WriteAsync(packet, ct).ConfigureAwait(false);
                        }
                        catch (System.OperationCanceledException)
                        {
                            // writer canceled -> exit
                            break;
                        }
                    }
                },
                new WorkerOptions
                {
                    Tag = "tcp",
                    OnFailed = (st, ex) => InstanceManager.Instance.GetExistingInstance<ILogger>()?
                                                                   .Warn($"[SDK.ReliableClient.GM] Worker failed: {ex.Message}")
                });

            _workerId[0] = recvNetWorker.Id;

            // Start receive consumer worker: read from channel and invoke PacketReceived + dispatcher
            IWorkerHandle recvConsumer = InstanceManager.Instance.GetOrCreateInstance<TaskManager>().ScheduleWorker(
                name: $"tcp-recv-consumer-{Options.Address}:{Options.Port}",
                group: "network",
                async (ctx, ct) =>
                {
                    System.Threading.Channels.ChannelReader<IPacket> reader = _recvChannel.Reader;
                    try
                    {
                        if (!Dispatcher.IsEmpty)
                        {
                            await foreach (IPacket p in System.Threading.Tasks.TaskAsyncEnumerableExtensions.ConfigureAwait(reader.ReadAllAsync(ct), false))
                            {
                                // Dispatch to dispatcher (non-blocking) and then raise event for backwards compatibility.
                                try
                                {
                                    Dispatcher?.Dispatch(p);
                                }
                                catch (System.Exception ex)
                                {
                                    InstanceManager.Instance.GetExistingInstance<ILogger>()?
                                                            .Warn($"[SDK.ReliableClient] Dispatcher threw: {ex.Message}");
                                }

                                SAFE_INVOKE(PacketReceived, p, InstanceManager.Instance.GetExistingInstance<ILogger>());
                            }
                        }
                        else
                        {
                            await foreach (IPacket p in System.Threading.Tasks.TaskAsyncEnumerableExtensions.ConfigureAwait(reader.ReadAllAsync(ct), false))
                            {
                                // No dispatcher: just raise event
                                SAFE_INVOKE(PacketReceived, p, InstanceManager.Instance.GetExistingInstance<ILogger>());
                            }
                        }
                    }
                    catch (System.OperationCanceledException)
                    {
                        // canceled
                    }
                    catch (System.Exception ex)
                    {
                        InstanceManager.Instance.GetExistingInstance<ILogger>()?
                                                .Warn($"[SDK.ReliableClient] Receive consumer failed: {ex.Message}");
                    }
                },
                new WorkerOptions
                {
                    Tag = "tcp",
                    IdType = SnowflakeType.System,
                });

            _workerId[1] = recvConsumer.Id;

            // Start sender worker that batches outgoing packets
            IWorkerHandle senderWorker = InstanceManager.Instance.GetOrCreateInstance<TaskManager>().ScheduleWorker(
                name: $"tcp-send-batch-{Options.Address}:{Options.Port}",
                group: "network",
                async (ctx, ct) =>
                {
                    var reader = _sendChannel.Reader;

                    while (!ct.IsCancellationRequested && IsConnected)
                    {
                        System.ReadOnlyMemory<System.Byte> first;
                        try
                        {
                            first = await reader.ReadAsync(ct).ConfigureAwait(false);
                        }
                        catch (System.OperationCanceledException)
                        {
                            break;
                        }

                        // accumulate
                        System.Collections.Generic.List<System.ReadOnlyMemory<System.Byte>> segments = new(4) { first };
                        System.Int32 totalBytes = first.Length + FrameHeaderSize; // header bytes included per frame
                        while (segments.Count < SenderMaxBatchCount && totalBytes < SenderMaxBatchBytes && reader.TryRead(out var next))
                        {
                            segments.Add(next);
                            totalBytes += next.Length + 2;
                        }

                        // If small batch and channel has items, optionally wait a short time to let more packets arrive
                        if (segments.Count == 1 && totalBytes < SenderMaxBatchBytes)
                        {
                            // Small opportunistic delay
                            try { await System.Threading.Tasks.Task.Delay(SenderBatchDelayMs, ct).ConfigureAwait(false); } catch { }
                            // Drain any newly arrived items up to limits
                            while (segments.Count < SenderMaxBatchCount && totalBytes < SenderMaxBatchBytes && reader.TryRead(out var next2))
                            {
                                segments.Add(next2);
                                totalBytes += next2.Length + FrameHeaderSize;
                            }
                        }

                        // Build a single buffer containing framed packets: [total:U16 LE][payload]...
                        // Use ArrayPool to avoid repeated allocations
                        var rented = System.Buffers.ArrayPool<System.Byte>.Shared.Rent(totalBytes);
                        try
                        {
                            System.Int32 pos = 0;
                            foreach (var seg in segments)
                            {
                                System.Int32 payloadLen = seg.Length;
                                System.UInt16 total = (System.UInt16)(payloadLen + sizeof(System.UInt16));
                                System.Buffers.Binary.BinaryPrimitives.WriteUInt16LittleEndian(System.MemoryExtensions.AsSpan(rented, pos, 2), total);
                                pos += FrameHeaderSize;
                                if (payloadLen > 0)
                                {
                                    seg.CopyTo(System.MemoryExtensions.AsMemory(rented, pos, payloadLen));
                                    pos += payloadLen;
                                }
                            }

                            // Single write + flush
                            try
                            {
                                await _stream.WriteAsync(System.MemoryExtensions.AsMemory(rented, 0, pos), ct).ConfigureAwait(false);
                                await _stream.FlushAsync(ct).ConfigureAwait(false);
                            }
                            catch (System.Exception ex)
                            {
                                InstanceManager.Instance.GetExistingInstance<ILogger>()?
                                                .Error($"[SDK.ReliableClient] Sender worker write failed: {ex.Message}");
                                // Mark IO dead and disconnect
                                this.MARK_IO_DEAD(ex);
                                this.Disconnect();
                                break;
                            }
                        }
                        finally
                        {
                            System.Array.Clear(rented, 0, totalBytes);
                            System.Buffers.ArrayPool<System.Byte>.Shared.Return(rented);
                        }
                    }
                },
                new WorkerOptions
                {
                    Tag = "tcp",
                    IdType = SnowflakeType.System,
                });

            _workerId[2] = senderWorker.Id;
        }
        catch (System.OperationCanceledException) when (cts.IsCancellationRequested && !cancellationToken.IsCancellationRequested)
        {
            // internal timeout
            throw new System.TimeoutException($"ConnectAsync timeout after {timeout} ms.");
        }
        catch (System.OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // propagate user cancel
            throw;
        }
        finally
        {
            _ = _connGate.Release();
        }
    }

    /// <inheritdoc/>
    [System.Diagnostics.CodeAnalysis.MemberNotNull(nameof(_outbound))]
    public System.Threading.Tasks.Task SendAsync(
        [System.Diagnostics.CodeAnalysis.NotNull] IPacket packet,
        [System.Diagnostics.CodeAnalysis.NotNull] System.Threading.CancellationToken ct = default)
    {
        System.ArgumentNullException.ThrowIfNull(packet);

        if (_sendChannel is null)
        {
            // Not connected or send channel not initialized: fallback to direct outbound sender
            return (_outbound ?? throw new System.InvalidOperationException("Not connected.")).SEND_ASYNC(packet, ct);
        }

        // Serialize and enqueue to send channel (bounded). This will apply backpressure when full.
        System.Byte[] mem = packet.Serialize();

        // Validate size
        if (mem.Length > PacketConstants.PacketSizeLimit)
        {
            throw new System.ArgumentOutOfRangeException(nameof(packet), "Packet too large.");
        }

        // Try to write quickly; otherwise await available space
        var writer = _sendChannel.Writer;
        return !writer.TryWrite(mem) ? writer.WriteAsync(mem, ct).AsTask() : System.Threading.Tasks.Task.CompletedTask;
    }

    /// <inheritdoc/>
    public System.Threading.Tasks.Task SendAsync(System.ReadOnlyMemory<System.Byte> bytes, System.Threading.CancellationToken ct = default)
    {
        if (_sendChannel is null)
        {
            // fallback to direct write
            return (_outbound ?? throw new System.InvalidOperationException("Not connected.")).SEND_ASYNC(bytes, ct);
        }

        if (bytes.Length > PacketConstants.PacketSizeLimit)
        {
            throw new System.ArgumentOutOfRangeException(nameof(bytes), "Packet too large.");
        }

        var writer = _sendChannel.Writer;
        return !writer.TryWrite(bytes) ? writer.WriteAsync(bytes, ct).AsTask() : System.Threading.Tasks.Task.CompletedTask;
    }

    /// <inheritdoc/>
    [System.Diagnostics.DebuggerStepThrough]
    public void Disconnect()
    {
        _closed = true;
        _ioHealthy = false;

        if (_workerId is not null)
        {
            for (System.Int32 i = 0; i < _workerId.Length; i++)
            {
                if (_workerId[i] is null)
                {
                    continue;
                }

                _ = InstanceManager.Instance.GetOrCreateInstance<TaskManager>()
                                            .CancelWorker(_workerId[i]);
            }

        }

        this.DEEP_CLOSE();

        try
        {
            _stream?.Dispose();
        }
        catch { /* swallow */ }
        try
        {
            _client?.Close();
        }
        catch { /* swallow */ }

        _outbound = null;
        _inbound = null;

        // Complete channels to unblock workers
        try
        {
            _ = (_recvChannel?.Writer.TryComplete());
        }
        catch { }
        try
        {
            _ = (_sendChannel?.Writer.TryComplete());
        }
        catch { }

        // Dispose dispatcher if owned
        try
        {
            Dispatcher?.Dispose();
        }
        catch { }

        // Notify once on explicit disconnect as well
        if (System.Threading.Interlocked.Exchange(ref _discNotified, 1) == 0)
        {
            SAFE_INVOKE(Disconnected, null, InstanceManager.Instance.GetExistingInstance<ILogger>());
        }
    }

    /// <inheritdoc/>
    [System.Diagnostics.DebuggerStepThrough]
    public void Dispose()
    {
        this.Disconnect();
        System.GC.SuppressFinalize(this);
    }

    #endregion APIs

    #region Private Methods

    private static void CONFIGURE_SOCKET([System.Diagnostics.CodeAnalysis.NotNull] System.Net.Sockets.TcpClient client)
    {
        if (client is null)
        {
            return;
        }

        try
        {
            client.NoDelay = true;
            client.SendBufferSize = SocketBufferSize;
            client.ReceiveBufferSize = SocketBufferSize;
        }
        catch { /* ignore */ }

        try
        {
            client.LingerState = new System.Net.Sockets.LingerOption(false, LingerTimeoutSeconds);
        }
        catch { /* ignore */ }

        try
        {
            client.Client?.SetSocketOption(
                System.Net.Sockets.SocketOptionLevel.Socket,
                System.Net.Sockets.SocketOptionName.KeepAlive, true);
        }
        catch { /* ignore */ }

        if (System.OperatingSystem.IsWindows())
        {
            _ = client.Client.IOControl(System.Net.Sockets.IOControlCode.KeepAliveValues,
                              KEEP_ALIVE_CONFIG(keepAliveTimeMs: KeepAliveTimeMs, keepAliveIntervalMs: KeepAliveIntervalMs), null);
        }
    }

    private static System.Byte[] KEEP_ALIVE_CONFIG(System.UInt32 keepAliveTimeMs, System.UInt32 keepAliveIntervalMs)
    {
        System.Byte[] buffer = new System.Byte[12];
        System.BitConverter.GetBytes(1u).CopyTo(buffer, 0); // Enable
        System.BitConverter.GetBytes(keepAliveTimeMs).CopyTo(buffer, 4); // Idle time
        System.BitConverter.GetBytes(keepAliveIntervalMs).CopyTo(buffer, 8); // Interval
        return buffer;
    }

    private void MARK_IO_DEAD(System.Exception ex = null)
    {
        _ioHealthy = false;
        if (System.Threading.Interlocked.Exchange(ref _discNotified, 1) == 0)
        {
            SAFE_INVOKE(Disconnected, ex, InstanceManager.Instance.GetExistingInstance<ILogger>());
        }
    }

    private static void ABORTIVE_CLOSE(System.Net.Sockets.Socket s)
    {
        if (s == null)
        {
            return;
        }

        try
        {
            s.LingerState = new System.Net.Sockets.LingerOption(true, 0);
        }
        catch { /* ignore */ }

        try
        {
            s.Shutdown(System.Net.Sockets.SocketShutdown.Both);
        }
        catch { /* ignore */ }

        try
        {
            s.Close(0);
        }
        catch { /* ignore */ }

        try { s.Dispose(); } catch { /* ignore */ }
    }

    private void DEEP_CLOSE()
    {
        try
        {
            _stream?.Dispose();
        }
        catch { /* ignore */ }
        _stream = null;

        try
        {
            ABORTIVE_CLOSE(_client?.Client);
        }
        catch { /* ignore */ }

        try
        {
            _client?.Dispose();
        }
        catch { /* ignore */ }
        _client = null;

        _outbound = null;
        _inbound = null;
    }

    private static void SAFE_INVOKE(System.Action evt, ILogger log)
    {
        System.Action d = evt;
        if (d is null)
        {
            return;
        }

        foreach (System.Action h in d.GetInvocationList().Cast<System.Action>())
        {
            try
            {
                h();
            }
            catch (System.Exception ex)
            {
                log?.Warn($"[SDK.ReliableClient] Subscriber threw: {ex}");
            }
        }
    }

    private static void SAFE_INVOKE<T>(System.Action<T> evt, T arg, ILogger log)
    {
        System.Action<T> d = evt;
        if (d is null)
        {
            return;
        }

        foreach (System.Action<T> h in d.GetInvocationList().Cast<System.Action<T>>())
        {
            try
            {
                h(arg);
            }
            catch (System.Exception ex)
            {
                log?.Warn($"[SDK.ReliableClient] Subscriber threw: {ex}");
            }
        }
    }

    #endregion Private Methods
}