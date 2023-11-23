// Copyright (c) 2025 PPN Corporation. All rights reserved.

using Nalix.Common.Abstractions;
using Nalix.Common.Connection;
using Nalix.Common.Packets.Abstractions;
using Nalix.Network.Dispatch.Catalog;
using Nalix.Network.Dispatch.Channel;
using Nalix.Network.Dispatch.Core.Engine;
using Nalix.Network.Dispatch.Core.Interfaces;
using Nalix.Shared.Extensions;
using Nalix.Shared.Injection;

namespace Nalix.Network.Dispatch;

/// <summary>
/// Represents an ultra-high performance raw dispatcher designed for asynchronous, queue-based processing
/// with dependency injection (DI) support and flexible raw handling via reflection-based routing.
/// </summary>
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
[System.Diagnostics.DebuggerDisplay("Running={_running}, Pending={_dispatch.TotalPackets}")]
public sealed class PacketDispatchChannel
    : PacketDispatchCore<IPacket>, IPacketDispatch<IPacket>, System.IDisposable, IActivatable
{
    #region Fields

    private readonly PacketCatalog _catalog;
    private readonly DispatchChannel<IPacket> _dispatch;
    private readonly System.Threading.SemaphoreSlim _semaphore = new(0);
    private readonly System.Threading.CancellationTokenSource _cts = new();

    private System.Int32 _running;
    private System.Threading.Tasks.Task? _loopTask;

    #endregion Fields

    #region Constructors

    /// <summary>
    /// Initializes a new instance of the <see cref="PacketDispatchChannel"/> class
    /// with custom configuration options.
    /// </summary>
    /// <param name="options">A delegate used to configure dispatcher options</param>
    public PacketDispatchChannel(System.Action<Options.PacketDispatchOptions<IPacket>> options)
        : base(options)
    {
        _dispatch = new DispatchChannel<IPacket>(logger: null);
        _catalog = InstanceManager.Instance.GetExistingInstance<PacketCatalog>()
                   ?? throw new System.InvalidOperationException(
                       $"[{nameof(PacketDispatchChannel)}] PacketCatalog not registered in InstanceManager. " +
                       $"Make sure to build and register PacketCatalog before starting dispatcher.");

        // Push any additional initialization here if needed
#if DEBUG
        Logger?.Debug("[Dispatch] Initialized with custom options");
#endif
    }

    #endregion Constructors

    #region Public Methods

    /// <summary>
    /// Starts the raw processing loop
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(
       System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public void Activate()
    {
        if (System.Threading.Interlocked.CompareExchange(ref _running, 1, 0) != 0)
        {
#if DEBUG
            Logger?.Debug("[Dispatch] StartTickLoopAsync() called but dispatcher is already running.");
#endif
            return;
        }

        Logger?.Info("[Dispatch] Dispatch loop starting...");
        _loopTask = System.Threading.Tasks.Task.Run(this.RunDispatchLoopAsync);
    }

    /// <summary>
    /// Stops the raw processing loop
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(
       System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public void Deactivate()
    {
        if (System.Threading.Interlocked.Exchange(ref _running, 0) == 0)
        {
            return;
        }

        try
        {
            if (!this._cts.IsCancellationRequested)
            {
                this._cts.Cancel();
#if DEBUG
                Logger?.Trace("[Dispatch] Dispatch loop stopped gracefully.");
#endif
            }

            try { _semaphore.Release(); } catch { /* ignore over-release */ }

            System.Threading.Tasks.Task? t = _loopTask;
            if (t is not null)
            {
                try { t.Wait(System.TimeSpan.FromSeconds(2)); }
                catch { /* ignore */ }
            }
        }
        catch (System.ObjectDisposedException)
        {
            Logger?.Warn("[Dispatch] Attempted to cancel a disposed CancellationTokenSource.");
        }
        catch (System.Exception ex)
        {
            Logger?.Error($"[Dispatch] Error while stopping dispatcher: {ex.Message}", ex);
        }
    }

    /// <inheritdoc />
    [System.Runtime.CompilerServices.MethodImpl(
       System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining |
       System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
    public void HandlePacket(System.Byte[]? raw, IConnection connection)
    {
        if (raw == null)
        {
            Logger?.Warn($"[Dispatch] Null byte[] received from {connection.RemoteEndPoint}. Packet dropped.");
            return;
        }

        this.HandlePacket(System.MemoryExtensions.AsSpan(raw), connection);
    }

    /// <inheritdoc />
    [System.Runtime.CompilerServices.MethodImpl(
       System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining |
       System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
    public void HandlePacket(System.ReadOnlyMemory<System.Byte>? raw, IConnection connection)
    {
        if (raw == null)
        {
            Logger?.Warn(
                $"[Dispatch] Null ReadOnlyMemory<byte> received from {connection.RemoteEndPoint}. Packet dropped.");
            return;
        }

        this.HandlePacket(raw.Value.Span, connection);
    }

    /// <inheritdoc />
    [System.Runtime.CompilerServices.MethodImpl(
       System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining |
       System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
    public void HandlePacket(in System.ReadOnlySpan<System.Byte> raw, IConnection connection)
    {
        // 1) Fast-fail: empty payload
        if (raw.IsEmpty)
        {
            Logger?.Warn(
                "[PacketDispatch] Empty payload from {0}. Dropped.",
                connection.RemoteEndPoint);
            return;
        }

        // 2) Capture basic context once
        System.Int32 len = raw.Length;
        System.UInt32 magic = len >= 4 ? raw.ReadMagicNumberLE() : 0u;

        // 3) Try deserialize
        if (!_catalog.TryDeserialize(raw, out IPacket? packet) || packet is null)
        {
            // Log only a small head preview to avoid leaking large/secret data
            System.String head = System.Convert.ToHexString(raw[..System.Math.Min(16, len)]);
            Logger?.Warn(
                "[PacketDispatch] Unknown packet. Remote={0}, Len={1}, Magic=0x{2:X8}, Head={3}. Dropped.",
                connection.RemoteEndPoint, len, magic, head);
            return;
        }

        // 4) Success trace (can be disabled in production)
        Logger?.Trace(
            "[PacketDispatch] Deserialized {0} from {1}. Len={2}, Magic=0x{3:X8}.",
            packet.GetType().Name, connection.RemoteEndPoint, len, magic);

        // 5) Dispatch to typed handler
        this.HandlePacket(packet, connection);
    }

    /// <inheritdoc />
    [System.Runtime.CompilerServices.MethodImpl(
       System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining |
       System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
    public void HandlePacket(IPacket packet, IConnection connection)
    {
        this._dispatch.Push(packet, connection);
        _ = _semaphore.Release();
    }

    #endregion Public Methods

    #region Private Methods

    /// <summary>
    /// Continuously processes packets from the queue
    /// </summary>
    private async System.Threading.Tasks.Task RunDispatchLoopAsync()
    {
        try
        {
            while (System.Threading.Volatile.Read(ref _running) == 1 &&
                  !_cts.Token.IsCancellationRequested)
            {
                // Wait for packets to be available
                await this._semaphore.WaitAsync(this._cts.Token).ConfigureAwait(false);

                // Dequeue and process raw
                if (!_dispatch.Pull(out IPacket packet, out IConnection connection))
                {
                    Logger?.Warn("[Dispatch] Failed to dequeue packet from dispatch channel.");
                    continue;
                }

                await ExecutePacketHandlerAsync(packet, connection).ConfigureAwait(false);
            }
        }
        catch (System.OperationCanceledException)
        {
            // Normal cancellation, no need to log
        }
        catch (System.Exception ex)
        {
            Logger?.Error($"[Dispatch] Error in raw processing loop: {ex.Message}", ex);
        }
        finally
        {
            System.Threading.Volatile.Write(ref _running, 0);
        }
    }

    #endregion Private Methods

    #region IDisposable

    /// <summary>
    /// Releases resources used by the dispatcher
    /// </summary>
    [System.Diagnostics.DebuggerNonUserCode]
    public void Dispose()
    {
        this.Deactivate();
        this._cts.Dispose();
        this._semaphore.Dispose();
    }

    #endregion IDisposable
}