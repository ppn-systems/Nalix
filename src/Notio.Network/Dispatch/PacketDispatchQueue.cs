using Notio.Network.Dispatch.Core;

namespace Notio.Network.Dispatch;

/// <summary>
/// Represents an ultra-high performance packet dispatcher designed for asynchronous, queue-based processing
/// with dependency injection (DI) support and flexible packet handling via reflection-based routing.
/// </summary>
/// <typeparam name="TPacket">
/// The packet type implementing <see cref="Common.Package.IPacket"/>,
/// <see cref="Common.Package.IPacketEncryptor{TPacket}"/>, 
/// <see cref="Common.Package.IPacketCompressor{TPacket}"/>,
/// <see cref="Common.Package.IPacketDeserializer{TPacket}"/>.
/// </typeparam>
/// <remarks>
/// <para>
/// This dispatcher works by queuing incoming packets and processing them in a background loop. Packet handling
/// is done asynchronously using handlers resolved via packet command IDs.
/// </para>
/// <para>
/// It is suitable for high-throughput systems such as custom TCP servers, IoT message brokers, or game servers
/// where latency, memory pressure, and throughput are critical.
/// </para>
/// </remarks>
/// <example>
/// Example usage:
/// <code>
/// var dispatcher = new PacketDispatchQueue`Packet`(opts => {
///     opts.WithHandler(...);
/// });
/// dispatcher.Start();
/// ...
/// dispatcher.HandlePacket(data, connection);
/// </code>
/// </example>
public sealed class PacketDispatchQueue<TPacket>
    : PacketDispatchBase<TPacket>, IPacketDispatch<TPacket> where TPacket : Common.Package.IPacket,
    Common.Package.IPacketEncryptor<TPacket>,
    Common.Package.IPacketCompressor<TPacket>,
    Common.Package.IPacketDeserializer<TPacket>
{
    #region Fields

    // Queue for storing packet handling tasks
    private readonly Queue.PacketPriorityQueue<TPacket> _dispatchQueue;
    // Reverse mapping: IConnection -> set of all associated packet keys
    private readonly System.Collections.Generic.Dictionary<
        Common.Connection.IConnection, System.Collections.Generic.HashSet<ulong>> _reverseMap = [];

    // Forward mapping: packet key -> connection
    private readonly System.Collections.Generic.Dictionary<
        ulong, Common.Connection.IConnection> _packetMap = [];

    // Locks for thread safety
    private readonly System.Threading.Lock _lock;
    private readonly System.Threading.SemaphoreSlim _semaphore;

    // Processing state
    private bool _isProcessing;
    private readonly System.Threading.CancellationTokenSource _ctokens = new();

    #endregion

    #region Properties

    /// <summary>
    /// Maximum number of packets that can be queued
    /// </summary>
    public int MaxQueueSize { get; set; } = 1000;

    /// <summary>
    /// Current number of packets in the queue
    /// </summary>
    public int QueueCount
    {
        get
        {
            lock (_lock)
            {
                return _dispatchQueue.Count;
            }
        }
    }

    #endregion

    #region Constructors

    /// <summary>
    /// Initializes a new instance of the <see cref="PacketDispatchQueue{TPacket}"/> class
    /// with custom configuration options.
    /// </summary>
    /// <param name="options">A delegate used to configure dispatcher options</param>
    public PacketDispatchQueue(System.Action<Options.PacketDispatchOptions<TPacket>> options)
        : base(options)
    {
        _isProcessing = false;

        _lock = new System.Threading.Lock();
        _semaphore = new System.Threading.SemaphoreSlim(0);
        _ctokens = new System.Threading.CancellationTokenSource();
        _dispatchQueue = new Queue.PacketPriorityQueue<TPacket>(Options.QueueOptions);

        // Add any additional initialization here if needed
        base.Logger?.Debug("[Dispatch] Initialized with custom options");
    }

    #endregion

    #region Public Methods

    /// <summary>
    /// Starts the packet processing loop
    /// </summary>
    public void Start()
    {
        if (_isProcessing)
        {
            base.Logger?.Debug("[Dispatch] Start() called but dispatcher is already running.");
            return;
        }

        _isProcessing = true;

        base.Logger?.Info("[Dispatch] Dispatch loop starting...");
        System.Threading.Tasks.Task.Run(RunQueueLoopAsync);
    }

    /// <summary>
    /// Stops the packet processing loop
    /// </summary>
    public void Stop()
    {
        if (!_isProcessing)
            return;

        _isProcessing = false;

        try
        {
            if (!_ctokens.IsCancellationRequested)
            {
                _ctokens.Cancel();
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
    public void HandlePacket(byte[]? packet, Common.Connection.IConnection connection)
    {
        if (packet == null)
        {
            base.Logger?.Error($"[Dispatch] Null byte[] received from {connection.RemoteEndPoint}. Packet dropped.");
            return;
        }

        this.HandlePacket(System.MemoryExtensions.AsSpan(packet), connection);
    }

    /// <inheritdoc />
    public void HandlePacket(System.ReadOnlyMemory<byte>? packet, Common.Connection.IConnection connection)
    {
        if (packet == null)
        {
            base.Logger?.Error(
                $"[Dispatch] Null ReadOnlyMemory<byte> received from {connection.RemoteEndPoint}. Packet dropped.");
            return;
        }

        this.HandlePacket(packet.Value.Span, connection);
    }

    /// <inheritdoc />
    public void HandlePacket(in System.ReadOnlySpan<byte> packet, Common.Connection.IConnection connection)
    {
        if (packet.IsEmpty)
        {
            base.Logger?.Error(
                $"[Dispatch] Empty ReadOnlySpan<byte> received from {connection.RemoteEndPoint}. Packet dropped.");
            return;
        }

        // Deserialize and enqueue the packet for processing
        this.HandlePacketAsync(TPacket.Deserialize(packet), connection);
    }

    /// <inheritdoc />
    public void HandlePacketAsync(TPacket packet, Common.Connection.IConnection connection)
    {
        lock (_lock)
        {
            _dispatchQueue.Enqueue(packet);

            _packetMap[packet.Hash] = connection;

            if (!_reverseMap.TryGetValue(connection,
                out System.Collections.Generic.HashSet<ulong>? set))
            {
                set = [];

                // Create reverse mapping entry
                _reverseMap[connection] = set;

                // Register event only once
                connection.OnCloseEvent += OnConnectionClosed;
            }

            set.Add(packet.Hash);
        }
        _semaphore.Release();
    }

    #endregion

    #region Private Methods

    /// <summary>
    /// Continuously processes packets from the queue
    /// </summary>
    private async System.Threading.Tasks.Task RunQueueLoopAsync()
    {
        try
        {
            while (_isProcessing && !_ctokens.Token.IsCancellationRequested)
            {
                // Wait for packets to be available
                await _semaphore.WaitAsync(_ctokens.Token);

                // Dequeue and process packet
                TPacket packet;
                Common.Connection.IConnection? connection;

                lock (_lock)
                {
                    if (_dispatchQueue.Count == 0)
                        continue;

                    packet = _dispatchQueue.Dequeue();

                    if (!_packetMap.TryGetValue(packet.Hash, out connection))
                    {
                        base.Logger?.Warn("[Dispatch] No connection found for packet.");
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
            base.Logger?.Error($"[Dispatch] Error in packet processing loop: {ex.Message}", ex);
        }
        finally
        {
            _isProcessing = false;
        }
    }

    private void OnConnectionClosed(object? sender, Common.Connection.IConnectEventArgs e)
    {
        if (sender is not Common.Connection.IConnection connection) return;

        lock (_lock)
        {
            if (_reverseMap.TryGetValue(connection,
                out System.Collections.Generic.HashSet<ulong>? keys))
            {
                foreach (ulong key in keys)
                {
                    _packetMap.Remove(key);
                }

                _reverseMap.Remove(connection);
            }

            connection.OnCloseEvent -= OnConnectionClosed;
        }

        base.Logger?.Info($"[Dispatch] Auto-removed keys for closed connection {connection.RemoteEndPoint}");
    }

    #endregion

    #region IDisposable

    /// <summary>
    /// Releases resources used by the dispatcher
    /// </summary>
    public void Dispose()
    {
        this.Stop();
        _ctokens.Dispose();
        _semaphore.Dispose();
    }

    #endregion
}
