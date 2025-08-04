using Nalix.Common.Connection;
using Nalix.Common.Packets.Interfaces;
using Nalix.Network.Dispatch.Channel;
using Nalix.Network.Dispatch.Core;

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
/// ...
/// dispatcher.HandlePacket(data, connection);
/// </code>
/// </example>
public sealed class PacketDispatchChannel<TPacket>
    : PacketDispatchCore<TPacket>, IPacketDispatch<TPacket> where TPacket
    : IPacket, IPacketTransformer<TPacket>
{
    #region Fields

    private System.Boolean _running;

    [System.Diagnostics.CodeAnalysis.SuppressMessage(
        "CodeQuality", "IDE0079:Remove unnecessary suppression", Justification = "<Pending>")]
    [System.Diagnostics.CodeAnalysis.SuppressMessage(
        "Performance", "CA1859:Use concrete types when possible for improved performance", Justification = "<Pending>")]
    private readonly IDispatchChannel<TPacket> _dispatch;
    private readonly System.Threading.SemaphoreSlim _semaphore = new(0);
    private readonly System.Threading.CancellationTokenSource _cts = new();

    #endregion Fields

    #region Constructors

    /// <summary>
    /// Initializes a new instance of the <see cref="PacketDispatchChannel{TPacket}"/> class
    /// with custom configuration options.
    /// </summary>
    /// <param name="options">A delegate used to configure dispatcher options</param>
    public PacketDispatchChannel(System.Action<Options.PacketDispatchOptions<TPacket>> options)
        : base(options)
    {
        _running = false;
        _dispatch = new DispatchChannel<TPacket>(logger: null);

        // Push any additional initialization here if needed
#if DEBUG
        base.Logger?.Debug("[Dispatch] Initialized with custom options");
#endif
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
        if (_running)
        {
#if DEBUG
            base.Logger?.Debug("[Dispatch] StartTickLoopAsync() called but dispatcher is already running.");
#endif
            return;
        }

        _running = true;

        base.Logger?.Info("[Dispatch] Dispatch loop starting...");
        _ = System.Threading.Tasks.Task.Run(this.RunDispatchLoopAsync);
    }

    /// <summary>
    /// Stops the raw processing loop
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(
       System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public void Stop()
    {
        if (!_running)
        {
            return;
        }

        _running = false;

        try
        {
            if (!this._cts.IsCancellationRequested)
            {
                this._cts.Cancel();
#if DEBUG
                base.Logger?.Trace("[Dispatch] Dispatch loop stopped gracefully.");
#endif
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
            base.Logger?.Warn($"[Dispatch] Null byte[] received from {connection.RemoteEndPoint}. Packet dropped.");
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
            base.Logger?.Warn(
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
            base.Logger?.Warn(
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
        this._dispatch.Push(packet, connection);

        if (_dispatch.TotalPackets > 0)
        {
            _ = _semaphore.Release();
        }
    }

    #endregion Public Methods

    #region Private Methods

    /// <summary>
    /// Continuously processes packets from the queue
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(
       System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    private async System.Threading.Tasks.Task RunDispatchLoopAsync()
    {
        try
        {
            while (_running && !_cts.Token.IsCancellationRequested)
            {
                // Wait for packets to be available
                await this._semaphore.WaitAsync(this._cts.Token).ConfigureAwait(false);

                // Dequeue and process raw
                if (!_dispatch.Pull(out TPacket packet, out IConnection connection))
                {
                    base.Logger?.Warn("[Dispatch] Failed to dequeue packet from dispatch channel.");
                    continue;
                }

                await base.ExecutePacketHandlerAsync(packet, connection).ConfigureAwait(false);
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
            _running = false;
        }
    }

    #endregion Private Methods

    #region IDisposable

    /// <summary>
    /// Releases resources used by the dispatcher
    /// </summary>
    public void Dispose()
    {
        this.Stop();
        this._cts.Dispose();
        this._semaphore.Dispose();
    }

    #endregion IDisposable
}