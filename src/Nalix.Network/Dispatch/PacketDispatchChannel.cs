using Nalix.Common.Connection;
using Nalix.Common.Packets;
using Nalix.Network.Dispatch.Core;
using Nalix.Network.Dispatch.Internal.Channel;

namespace Nalix.Network.Dispatch;

/// <summary>
/// Represents an ultra-high performance raw dispatcher designed for asynchronous, queue-based processing
/// with dependency injection (DI) support and flexible raw handling via reflection-based routing.
/// </summary>
/// <typeparam name="TPacket">
/// The raw type implementing <see cref="IPacket"/>, <see cref="IPacketTransformer{TPacket}"/>
/// </typeparam>
/// <remarks>
/// <para>
/// This dispatcher works by queuing incoming packets and processing them in a background loop. Packet handling
/// is done asynchronously using handlers resolved via raw command IDs.
/// </para>
/// <para>
/// It is suitable for high-throughput systems such as custom Reliable servers, IoT message brokers, or game servers
/// where latency, memory pressure, and throughput are critical.
/// </para>
/// </remarks>
/// <example>
/// Example usage:
/// <code>
/// var dispatcher = new PacketDispatchChannel`Packet`(opts => {
///     opts.WithHandler(...);
/// });
/// dispatcher.StartTickLoopAsync();
/// ...
/// dispatcher.HandlePacket(data, connection);
/// </code>
/// </example>
public sealed class PacketDispatchChannel<TPacket>
    : PacketDispatchCore<TPacket>, IPacketDispatch<TPacket> where TPacket
    : IPacket, IPacketTransformer<TPacket>
{
    #region Fields

    // Queue for storing raw handling tasks
    private readonly MultiLevelQueue<TPacket> _dispatchQueue;

    // Reverse mapping: IConnection -> set of all associated raw keys
    private readonly System.Collections.Generic.Dictionary<
        IConnection, System.Collections.Generic.HashSet<System.Int32>> _reverseMap = [];

    // Forward mapping: raw key -> connection
    private readonly System.Collections.Generic.Dictionary<System.Int32, IConnection> _packetMap = [];

    // Locks for thread safety
    private readonly System.Threading.Lock _lock;

    private readonly System.Threading.SemaphoreSlim _semaphore;

    // Processing state
    private System.Boolean _isProcessing;

    private readonly System.Threading.CancellationTokenSource _ctokens = new();

    #endregion Fields

    #region Properties

    /// <summary>
    /// Current number of packets in the queue
    /// </summary>
    public System.Int32 QueueCount
    {
        get
        {
            lock (this._lock)
            {
                return this._dispatchQueue.Count;
            }
        }
    }

    #endregion Properties

    #region Constructors

    /// <summary>
    /// Initializes a new instance of the <see cref="PacketDispatchChannel{TPacket}"/> class
    /// with custom configuration options.
    /// </summary>
    /// <param name="options">A delegate used to configure dispatcher options</param>
    public PacketDispatchChannel(System.Action<Options.PacketDispatchOptions<TPacket>> options)
        : base(options)
    {
        this._isProcessing = false;

        this._lock = new System.Threading.Lock();
        this._semaphore = new System.Threading.SemaphoreSlim(0);
        this._ctokens = new System.Threading.CancellationTokenSource();
        this._dispatchQueue = new MultiLevelQueue<TPacket>(this.Options.QueueOptions);

        // Add any additional initialization here if needed
        base.Logger?.Debug("[Dispatch] Initialized with custom options");
    }

    #endregion Constructors

    #region Public Methods

    /// <summary>
    /// Starts the raw processing loop
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(
       System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public void Start()
    {
        if (this._isProcessing)
        {
            base.Logger?.Debug("[Dispatch] StartTickLoopAsync() called but dispatcher is already running.");
            return;
        }

        this._isProcessing = true;

        base.Logger?.Info("[Dispatch] Dispatch loop starting...");
        _ = System.Threading.Tasks.Task.Run(this.RunQueueLoopAsync);
    }

    /// <summary>
    /// Stops the raw processing loop
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(
       System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public void Stop()
    {
        if (!this._isProcessing)
        {
            return;
        }

        this._isProcessing = false;

        try
        {
            if (!this._ctokens.IsCancellationRequested)
            {
                this._ctokens.Cancel();
                base.Logger?.Info("[Dispatch] Dispatch loop stopped gracefully.");
            }
        }
        catch (System.ObjectDisposedException)
        {
            base.Logger?.Warn("[Dispatch] Attempted to cancel a disposed CancellationTokenSource.");
        }
        catch (System.Exception ex)
        {
            base.Logger?.Error($"[Dispatch] Error while stopping dispatcher: {ex.Message}", ex);
        }
    }

    /// <inheritdoc />
    [System.Runtime.CompilerServices.MethodImpl(
       System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public void HandlePacket(System.Byte[]? raw, IConnection connection)
    {
        if (raw == null)
        {
            base.Logger?.Error($"[Dispatch] Null byte[] received from {connection.RemoteEndPoint}. Packet dropped.");
            return;
        }

        this.HandlePacket(System.MemoryExtensions.AsSpan(raw), connection);
    }

    /// <inheritdoc />
    [System.Runtime.CompilerServices.MethodImpl(
       System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public void HandlePacket(System.ReadOnlyMemory<System.Byte>? raw, IConnection connection)
    {
        if (raw == null)
        {
            base.Logger?.Error(
                $"[Dispatch] Null ReadOnlyMemory<byte> received from {connection.RemoteEndPoint}. Packet dropped.");
            return;
        }

        this.HandlePacket(raw.Value.Span, connection);
    }

    /// <inheritdoc />
    [System.Runtime.CompilerServices.MethodImpl(
       System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public void HandlePacket(in System.ReadOnlySpan<System.Byte> raw, IConnection connection)
    {
        if (raw.IsEmpty)
        {
            base.Logger?.Error(
                $"[Dispatch] Empty ReadOnlySpan<byte> received from {connection.RemoteEndPoint}. Packet dropped.");
            return;
        }

        // Deserialize and enqueue the raw for processing
        this.HandlePacketAsync(TPacket.Deserialize(raw), connection);
    }

    /// <inheritdoc />
    [System.Runtime.CompilerServices.MethodImpl(
       System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public void HandlePacketAsync(TPacket packet, IConnection connection)
    {
        lock (this._lock)
        {
            _ = this._dispatchQueue.Enqueue(packet);

            this._packetMap[packet.Hash] = connection;

            if (!this._reverseMap.TryGetValue(connection,
                out System.Collections.Generic.HashSet<System.Int32>? set))
            {
                set = [];

                // Create reverse mapping entry
                this._reverseMap[connection] = set;

                // Register event only once
                connection.OnCloseEvent += this.OnConnectionClosed;
            }

            _ = set.Add(packet.Hash);
        }
        _ = this._semaphore.Release();
    }

    #endregion Public Methods

    #region Private Methods

    /// <summary>
    /// Continuously processes packets from the queue
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(
       System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    private async System.Threading.Tasks.Task RunQueueLoopAsync()
    {
        try
        {
            while (this._isProcessing && !this._ctokens.Token.IsCancellationRequested)
            {
                // Wait for packets to be available
                await this._semaphore.WaitAsync(this._ctokens.Token);

                // Dequeue and process raw
                TPacket packet;
                IConnection? connection;

                lock (this._lock)
                {
                    if (this._dispatchQueue.Count == 0)
                    {
                        continue;
                    }

                    packet = this._dispatchQueue.Dequeue();

                    if (!this._packetMap.TryGetValue(packet.Hash, out connection))
                    {
                        base.Logger?.Warn("[Dispatch] No connection found for raw.");
                        continue;
                    }
                }

                await base.ExecutePacketHandlerAsync(packet, connection);
            }
        }
        catch (System.OperationCanceledException)
        {
            // Normal cancellation, no need to log
        }
        catch (System.Exception ex)
        {
            base.Logger?.Error($"[Dispatch] Error in raw processing loop: {ex.Message}", ex);
        }
        finally
        {
            this._isProcessing = false;
        }
    }

    [System.Runtime.CompilerServices.MethodImpl(
       System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    private void OnConnectionClosed(System.Object? sender, IConnectEventArgs e)
    {
        if (sender is not IConnection connection)
        {
            return;
        }

        lock (this._lock)
        {
            if (this._reverseMap.TryGetValue(connection,
                out System.Collections.Generic.HashSet<System.Int32>? keys))
            {
                foreach (System.Int32 key in keys)
                {
                    _ = this._packetMap.Remove(key);
                }

                _ = this._reverseMap.Remove(connection);
            }

            connection.OnCloseEvent -= this.OnConnectionClosed;
        }

        base.Logger?.Info($"[Dispatch] Auto-removed keys for closed connection {connection.RemoteEndPoint}");
    }

    #endregion Private Methods

    #region IDisposable

    /// <summary>
    /// Releases resources used by the dispatcher
    /// </summary>
    public void Dispose()
    {
        this.Stop();
        this._ctokens.Dispose();
        this._semaphore.Dispose();
    }

    #endregion IDisposable
}