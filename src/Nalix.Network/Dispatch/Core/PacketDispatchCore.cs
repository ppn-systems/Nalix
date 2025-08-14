// Copyright (c) 2025 PPN Corporation. All rights reserved.

using Nalix.Common.Connection;
using Nalix.Common.Logging;
using Nalix.Common.Packets.Interfaces;
using Nalix.Network.Dispatch.Options;

namespace Nalix.Network.Dispatch.Core;

/// <summary>
/// Serves as the base class for packet dispatchers, offering common configuration and logging support.
/// </summary>
/// <typeparam name="TPacket">
/// The packet type that implements both <see cref="IPacket"/> and <see cref="IPacketTransformer{TPacket}"/>.
/// </typeparam>
public abstract class PacketDispatchCore<TPacket> where TPacket : IPacket
{
    #region Properties

    /// <summary>
    /// Gets the logger instance associated with this dispatcher, if configured.
    /// </summary>
    protected ILogger? Logger => this.Options.Logger;

    /// <summary>
    /// Gets the configuration options for this dispatcher instance.
    /// </summary>
    protected readonly PacketDispatchOptions<TPacket> Options;

    #endregion Properties

    #region Constructors

    /// <summary>
    /// Initializes a new instance of the <see cref="PacketDispatchCore{TPacket}"/> class
    /// using the provided <paramref name="options"/>.
    /// </summary>
    /// <param name="options">
    /// The dispatcher configuration options. Must not be <see langword="null"/>.
    /// </param>
    /// <exception cref="System.ArgumentNullException">
    /// Thrown if <paramref name="options"/> is <see langword="null"/>.
    /// </exception>
    [System.Diagnostics.CodeAnalysis.SuppressMessage(
        "Style", "IDE0290:UsePrimaryConstructor", Justification = "<Pending>")]
    protected PacketDispatchCore(PacketDispatchOptions<TPacket> options)
        => this.Options = options ?? throw new System.ArgumentNullException(nameof(options));

    /// <summary>
    /// Initializes a new instance of the <see cref="PacketDispatchCore{TPacket}"/> class
    /// with optional configuration logic.
    /// </summary>
    /// <param name="configure">
    /// An optional delegate to configure the <see cref="PacketDispatchOptions{TPacket}"/> instance.
    /// </param>
    protected PacketDispatchCore(System.Action<PacketDispatchOptions<TPacket>>? configure = null)
        : this(new PacketDispatchOptions<TPacket>()) => configure?.Invoke(this.Options);

    #endregion Constructors

    #region Protected Methods

    /// <summary>
    /// Executes the registered packet handler asynchronously using the provided packet and connection context.
    /// </summary>
    /// <param name="handler">
    /// A delegate that processes the packet. This delegate should implement the packet-specific logic,
    /// such as validation, response preparation, or triggering related workflows.
    /// </param>
    /// <param name="packet">
    /// The deserialized packet instance containing the data to be handled. Assumed to be already validated and routed.
    /// </param>
    /// <param name="connection">
    /// The connection instance representing the client that sent the packet.
    /// </param>
    /// <returns>
    /// A <see cref="System.Threading.Tasks.ValueTask"/> that represents the asynchronous execution of the handler logic.
    /// </returns>
    [System.Runtime.CompilerServices.MethodImpl(
       System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    protected async System.Threading.Tasks.ValueTask ExecuteHandlerAsync(
        TPacket packet,
        IConnection connection,
        System.Func<TPacket, IConnection, System.Threading.Tasks.Task> handler)
        => await handler(packet, connection).ConfigureAwait(false);

    /// <summary>
    /// Asynchronously processes a single incoming packet by resolving and executing the appropriate handler.
    /// </summary>
    /// <param name="packet">
    /// The packet to be processed. Must contain a valid OpCode to resolve a handler.
    /// </param>
    /// <param name="connection">
    /// The connection from which the packet was received.
    /// </param>
    /// <returns>
    /// A task that represents the asynchronous operation of handling the packet.
    /// </returns>
    /// <remarks>
    /// This method attempts to resolve a packet handler using the packet's OpCode via <see cref="PacketDispatchOptions{TPacket}.TryResolveHandler"/>.
    /// If a handler is found, it is invoked asynchronously. Exceptions are caught and logged.
    /// </remarks>
    [System.Runtime.CompilerServices.MethodImpl(
       System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    protected async System.Threading.Tasks.Task ExecutePacketHandlerAsync(TPacket packet, IConnection connection)
    {
        if (this.Options.TryResolveHandler(packet.OpCode,
            out System.Func<TPacket, IConnection, System.Threading.Tasks.Task>? handler))
        {
            this.Logger?.Debug($"[DispatchCore] Processing packet OpCode: {packet.OpCode} from {connection.RemoteEndPoint}...");

            try
            {
                await this.ExecuteHandlerAsync(packet, connection, handler)
                          .ConfigureAwait(false);
            }
            catch (System.Exception ex)
            {
                this.Logger?.Error(
                    $"[DispatchCore] Exception occurred while handling packet OpCode: " +
                    $"{packet.OpCode} from {connection.RemoteEndPoint}. " +
                    $"Error: {ex.GetType().Name} - {ex.Message}", ex);
            }

            return;
        }

        this.Logger?.Warn($"[DispatchCore] No handler found for packet OpCode: {packet.OpCode} from {connection.RemoteEndPoint}.");
    }

    #endregion Protected Methods
}
