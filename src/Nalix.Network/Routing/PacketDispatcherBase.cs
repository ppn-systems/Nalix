// Copyright (c) 2025-2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Nalix.Common.Networking;
using Nalix.Common.Networking.Packets;

namespace Nalix.Network.Routing;

/// <summary>
/// Serves as the base class for packet dispatchers, offering common configuration and logging support.
/// </summary>
/// <typeparam name="TPacket">
/// The packet type that implements both <see cref="IPacket"/>.
/// </typeparam>
public abstract class PacketDispatcherBase<TPacket> where TPacket : IPacket
{
    #region Properties

    /// <summary>
    /// Gets the logger instance associated with this dispatcher, if configured.
    /// </summary>
    protected ILogger? Logging => this.Options.Logging;

    /// <summary>
    /// Gets the configuration options for this dispatcher instance.
    /// </summary>
    protected PacketDispatchOptions<TPacket> Options { get; }

    #endregion Properties

    #region Constructors

    /// <summary>
    /// Initializes a new instance of the <see cref="PacketDispatcherBase{TPacket}"/> class
    /// using the provided <paramref name="options"/>.
    /// </summary>
    /// <param name="options">
    /// The dispatcher configuration options. Must not be <see langword="null"/>.
    /// </param>
    /// <exception cref="ArgumentNullException">
    /// Thrown if <paramref name="options"/> is <see langword="null"/>.
    /// </exception>
    [SuppressMessage("Style", "IDE0016:Use 'throw' expression", Justification = "<Pending>")]
    [SuppressMessage("Maintainability", "CA1510:Use ArgumentNullException throw helper", Justification = "<Pending>")]
    protected PacketDispatcherBase(PacketDispatchOptions<TPacket> options)
    {
        if (options == null)
        {
            throw new ArgumentNullException(nameof(options));
        }
        this.Options = options;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="PacketDispatcherBase{TPacket}"/> class
    /// with optional configuration logic.
    /// </summary>
    /// <param name="configure">
    /// An optional delegate to configure the <see cref="PacketDispatchOptions{TPacket}"/> instance.
    /// </param>
    [SuppressMessage("Style", "IDE1005:Delegate invocation can be simplified.", Justification = "<Pending>")]
    protected PacketDispatcherBase(
        Action<PacketDispatchOptions<TPacket>>? configure = null)
            : this(new PacketDispatchOptions<TPacket>())
    {
        if (configure != null)
        {
            configure(this.Options);
        }
    }

    #endregion Constructors

    #region Protected Methods

    /// <summary>
    /// Executes the registered packet handler asynchronously using the provided packet and connection context.
    /// </summary>
    /// <param name="packet">
    /// The deserialized packet instance containing the data to be handled. Assumed to be already validated and routed.
    /// </param>
    /// <param name="connection">
    /// The connection instance representing the client that sent the packet.
    /// </param>
    /// <param name="handler">
    /// A delegate that processes the packet. This delegate should implement the packet-specific logic,
    /// such as validation, response preparation, or triggering related workflows.
    /// </param>
    /// <returns>
    /// A <see cref="ValueTask"/> that represents the asynchronous execution of the handler logic.
    /// </returns>
    [MethodImpl(MethodImplOptions.NoInlining)]
    protected async ValueTask ExecuteHandlerAsync(TPacket packet, IConnection connection, Func<TPacket, IConnection, Task> handler)
    {
        ArgumentNullException.ThrowIfNull(handler, nameof(handler));

        await handler(packet, connection).ConfigureAwait(false);
    }

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
    [MethodImpl(MethodImplOptions.NoInlining)]
    protected async Task ExecutePacketHandlerAsync(
        TPacket packet,
        IConnection connection)
    {
        if (this.Options.TryResolveHandler(
            packet.OpCode,
            out Func<TPacket, IConnection, Task> handler))
        {
            this.Logging?.Trace($"[NW.{nameof(PacketDispatcherBase<>)}:{nameof(ExecuteHandlerAsync)}] handle opcode={packet.OpCode}");
            try
            {
                await this.ExecuteHandlerAsync(packet, connection, handler)
                          .ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                this.Logging?.Error($"[NW.{nameof(PacketDispatcherBase<>)}:{nameof(ExecuteHandlerAsync)}] handler-error opcode={packet.OpCode}", ex);
            }

            return;
        }

        this.Logging?.Warn($"[NW.{nameof(PacketDispatcherBase<>)}:{nameof(ExecuteHandlerAsync)}] no-handler opcode={packet.OpCode}");
    }

    #endregion Protected Methods
}
