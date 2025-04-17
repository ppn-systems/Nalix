namespace Notio.Network.Dispatcher;

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
/// var dispatcher = new PacketQueueDispatcher`Packet`(opts => {
///     opts.WithHandler(...);
/// });
/// dispatcher.Start();
/// ...
/// dispatcher.HandlePacket(data, connection);
/// </code>
/// </example>
public sealed class PacketQueueDispatcher<TPacket>
    : PacketDispatcherBase<TPacket>, IPacketDispatcher<TPacket> where TPacket : Common.Package.IPacket,
    Common.Package.IPacketEncryptor<TPacket>,
    Common.Package.IPacketCompressor<TPacket>,
    Common.Package.IPacketDeserializer<TPacket>
{
    #region Fields

    // Queue for storing packet handling tasks
    private readonly Queue.PacketQueue<TPacket> _packetQueue;
    private readonly System.Collections.Generic.Dictionary<ulong, Common.Connection.IConnection> _mapping = [];

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
                return _packetQueue.Count;
            }
        }
    }

    #endregion

    #region Constructors

    /// <summary>
    /// Initializes a new instance of the <see cref="PacketQueueDispatcher{TPacket}"/> class
    /// with custom configuration options.
    /// </summary>
    /// <param name="options">A delegate used to configure dispatcher options</param>
    public PacketQueueDispatcher(System.Action<Options.PacketDispatcherOptions<TPacket>> options)
        : base(options)
    {
        _isProcessing = false;

        _packetQueue = new Queue.PacketQueue<TPacket>();

        _lock = new System.Threading.Lock();
        _semaphore = new System.Threading.SemaphoreSlim(0);
        _ctokens = new System.Threading.CancellationTokenSource();

        // Add any additional initialization here if needed
        base.Logger?.Debug("[Dispatcher] Initialized with custom options");
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
            base.Logger?.Debug("[Dispatcher] Start() called but dispatcher is already running.");
            return;
        }

        _isProcessing = true;

        base.Logger?.Info("[Dispatcher] Dispatch loop starting...");
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
                base.Logger?.Info("[Dispatcher] Dispatch loop stopped gracefully.");
            }
        }
        catch (System.ObjectDisposedException)
        {
            base.Logger?.Warn("[Dispatcher] Attempted to cancel a disposed CancellationTokenSource.");
        }
        catch (System.Exception ex)
        {
            base.Logger?.Error($"[Dispatcher] Error while stopping dispatcher: {ex.Message}", ex);
        }
    }

    /// <inheritdoc />
    public void HandlePacket(byte[]? packet, Common.Connection.IConnection connection)
    {
        if (packet == null)
        {
            base.Logger?.Error($"[Dispatcher] Null byte[] received from {connection.RemoteEndPoint}. Packet dropped.");
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
                $"[Dispatcher] Null ReadOnlyMemory<byte> received from {connection.RemoteEndPoint}. Packet dropped.");
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
                $"[Dispatcher] Empty ReadOnlySpan<byte> received from {connection.RemoteEndPoint}. Packet dropped.");
            return;
        }

        // Deserialize and enqueue the packet for processing
        this.HandlePacketAsync(TPacket.Deserialize(packet), connection);
    }

    /// <inheritdoc />
    public void HandlePacketAsync(TPacket packet, Common.Connection.IConnection connection)
    {
        _packetQueue.Enqueue(packet);

        lock (_lock)
        {
            _mapping[CombineValues(packet.Number, packet.Id, packet.Timestamp)] = connection;
        }

        _semaphore.Release();
    }

    #endregion

    #region Private Methods

    /// <summary>
    /// Combines a byte, short, and ulong into a single ulong value
    /// </summary>
    private static ulong CombineValues(byte b, ushort s, ulong ul)
        => unchecked(((ulong)b << 48) | ((ulong)s << 32) | ul);

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
                Common.Connection.IConnection connection;

                lock (_lock)
                {
                    if (_packetQueue.Count == 0)
                        continue;

                    packet = _packetQueue.Dequeue();
                    lock (_lock)
                    {
                        connection = _mapping[CombineValues(packet.Number, packet.Id, packet.Timestamp)];
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
            base.Logger?.Error($"[Dispatcher] Error in packet processing loop: {ex.Message}", ex);
        }
        finally
        {
            _isProcessing = false;
        }
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
