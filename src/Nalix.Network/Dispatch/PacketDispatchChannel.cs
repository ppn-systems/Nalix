using Nalix.Common.Connection;
using Nalix.Common.Package;

namespace Nalix.Network.Dispatch;

/// <summary>
/// Represents an ultra-high performance raw dispatcher designed for asynchronous, queue-based processing
/// with dependency injection (DI) support and flexible raw handling via reflection-based routing.
/// </summary>
/// <typeparam name="TPacket">
/// The raw type implementing <see cref="IPacket"/>,
/// <see cref="IPacketEncryptor{TPacket}"/>,
/// <see cref="IPacketCompressor{TPacket}"/>,
/// <see cref="IPacketDeserializer{TPacket}"/>.
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
/// dispatcher.RunAsync();
/// ...
/// dispatcher.HandlePacket(data, connection);
/// </code>
/// </example>
public sealed class PacketDispatchChannel<TPacket>
    : PacketDispatchCore<TPacket>, IPacketDispatch<TPacket> where TPacket : IPacket,
    IPacketFactory<TPacket>,
    IPacketEncryptor<TPacket>,
    IPacketCompressor<TPacket>,
    IPacketDeserializer<TPacket>
{
    #region Fields

    // Queue for storing raw handling tasks
    private readonly Channel.ChannelDispatch<TPacket> _dispatchQueue;

    // Reverse mapping: IConnection -> set of all associated raw keys
    private readonly System.Collections.Generic.Dictionary<
        IConnection, System.Collections.Generic.HashSet<System.Int32>> _reverseMap = [];

    // Forward mapping: raw key -> connection
    private readonly System.Collections.Generic.Dictionary<System.Int32, IConnection> _packetMap = [];

    // Locks for thread safety
    private readonly System.Threading.Lock _lock;

    private readonly System.Threading.SemaphoreSlim _semaphore;

    // Processing state
    private bool _isProcessing;

    private readonly System.Threading.CancellationTokenSource _ctokens = new();

    #endregion Fields

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
        _isProcessing = false;

        _lock = new System.Threading.Lock();
        _semaphore = new System.Threading.SemaphoreSlim(0);
        _ctokens = new System.Threading.CancellationTokenSource();
        _dispatchQueue = new Channel.ChannelDispatch<TPacket>(Options.QueueOptions);

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
        if (_isProcessing)
        {
            base.Logger?.Debug("[Dispatch] RunAsync() called but dispatcher is already running.");
            return;
        }

        _isProcessing = true;

        base.Logger?.Info("[Dispatch] Dispatch loop starting...");
        System.Threading.Tasks.Task.Run(RunQueueLoopAsync);
    }

    /// <summary>
    /// Stops the raw processing loop
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(
       System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
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
    [System.Runtime.CompilerServices.MethodImpl(
       System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public void HandlePacket(byte[]? raw, IConnection connection)
    {
        if (raw == null)
        {
            base.Logger?.Error($"[Dispatch] Null byte[] received from {connection.RemoteEndPoint}. Packet dropped.");
            return;
        }

        HandlePacket(System.MemoryExtensions.AsSpan(raw), connection);
    }

    /// <inheritdoc />
    [System.Runtime.CompilerServices.MethodImpl(
       System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public void HandlePacket(System.ReadOnlyMemory<byte>? raw, IConnection connection)
    {
        if (raw == null)
        {
            base.Logger?.Error(
                $"[Dispatch] Null ReadOnlyMemory<byte> received from {connection.RemoteEndPoint}. Packet dropped.");
            return;
        }

        HandlePacket(raw.Value.Span, connection);
    }

    /// <inheritdoc />
    [System.Runtime.CompilerServices.MethodImpl(
       System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public void HandlePacket(in System.ReadOnlySpan<byte> raw, IConnection connection)
    {
        if (raw.IsEmpty)
        {
            base.Logger?.Error(
                $"[Dispatch] Empty ReadOnlySpan<byte> received from {connection.RemoteEndPoint}. Packet dropped.");
            return;
        }

        // Deserialize and enqueue the raw for processing
        HandlePacketAsync(TPacket.Deserialize(raw), connection);
    }

    /// <inheritdoc />
    [System.Runtime.CompilerServices.MethodImpl(
       System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public void HandlePacketAsync(TPacket packet, IConnection connection)
    {
        lock (_lock)
        {
            _dispatchQueue.Enqueue(packet);

            _packetMap[packet.Hash] = connection;

            if (!_reverseMap.TryGetValue(connection,
                out System.Collections.Generic.HashSet<System.Int32>? set))
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
            while (_isProcessing && !_ctokens.Token.IsCancellationRequested)
            {
                // Wait for packets to be available
                await _semaphore.WaitAsync(_ctokens.Token);

                // Dequeue and process raw
                TPacket packet;
                IConnection? connection;

                lock (_lock)
                {
                    if (_dispatchQueue.Count == 0)
                        continue;

                    packet = _dispatchQueue.Dequeue();

                    if (!_packetMap.TryGetValue(packet.Hash, out connection))
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
            _isProcessing = false;
        }
    }

    [System.Runtime.CompilerServices.MethodImpl(
       System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    private void OnConnectionClosed(object? sender, IConnectEventArgs e)
    {
        if (sender is not IConnection connection) return;

        lock (_lock)
        {
            if (_reverseMap.TryGetValue(connection,
                out System.Collections.Generic.HashSet<System.Int32>? keys))
            {
                foreach (System.Int32 key in keys)
                {
                    _packetMap.Remove(key);
                }

                _reverseMap.Remove(connection);
            }

            connection.OnCloseEvent -= OnConnectionClosed;
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
        Stop();
        _ctokens.Dispose();
        _semaphore.Dispose();
    }

    #endregion IDisposable
}
