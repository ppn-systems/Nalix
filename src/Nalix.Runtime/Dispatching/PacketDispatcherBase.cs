// Copyright (c) 2025-2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Diagnostics.CodeAnalysis;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Nalix.Abstractions.Exceptions;
using Nalix.Abstractions.Networking;
using Nalix.Abstractions.Networking.Packets;
using Nalix.Network.Routing;
using Nalix.Runtime.Internal.Compilation;

namespace Nalix.Runtime.Dispatching;

/// <summary>
/// Serves as the base class for packet dispatchers, offering Abstractions configuration and logging support.
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
    /// Asynchronously processes a single incoming packet by resolving and executing the appropriate handler.
    /// </summary>
    /// <param name="packet">
    /// The packet to be processed. Must contain a valid OpCode to resolve a handler.
    /// </param>
    /// <param name="connection">
    /// The connection from which the packet was received.
    /// </param>
    /// <param name="token">
    /// A cancellation token used to abort packet dispatch.
    /// </param>
    /// <returns>
    /// A task that represents the asynchronous operation of handling the packet.
    /// </returns>
    /// <remarks>
    /// This method attempts to resolve a packet handler using the packet's OpCode via <see cref="PacketDispatchOptions{TPacket}.TryResolveHandler"/>.
    /// If a handler is found, it is invoked asynchronously. Exceptions are caught and logged.
    /// </remarks>
    protected ValueTask ExecutePacketHandlerAsync(TPacket packet, IConnection connection, CancellationToken token = default)
    {
        if (this.Options.TryResolveHandler(packet.Header.OpCode, out PacketHandler<TPacket> handler))
        {
            if (this.Logging != null && this.Logging.IsEnabled(LogLevel.Trace))
            {
                this.Logging.LogTrace($"[RT.{nameof(PacketDispatcherBase<>)}:{nameof(ExecutePacketHandlerAsync)}] handle opcode={packet.Header.OpCode}");
            }

            ValueTask pending = this.Options.ExecuteResolvedHandlerAsync(in handler, packet, connection, token);

            if (pending.IsCompletedSuccessfully)
            {
                try
                {
#pragma warning disable CA1849 // Completed-success fast path; GetResult observes synchronous exceptions without blocking or allocating an async state machine.
                    pending.GetAwaiter().GetResult();
#pragma warning restore CA1849
                }
                catch (Exception ex) when (ExceptionClassifier.IsNonFatal(ex))
                {
                    if (ex is not SerializationFailureException)
                    {
                        if (this.Logging != null && this.Logging.IsEnabled(LogLevel.Error))
                        {
                            this.Logging.LogError(ex, $"[RT.{nameof(PacketDispatcherBase<>)}:{nameof(ExecutePacketHandlerAsync)}] handler-error opcode={packet.Header.OpCode}");
                        }
                    }
                }

                return ValueTask.CompletedTask;
            }

            return AwaitHandlerAsync(this, pending, packet.Header.OpCode);

            static async ValueTask AwaitHandlerAsync(PacketDispatcherBase<TPacket> owner, ValueTask operation, ushort opCode)
            {
                try
                {
                    await operation.ConfigureAwait(false);
                }
                catch (Exception ex) when (ExceptionClassifier.IsNonFatal(ex))
                {
                    if (owner.Logging != null && owner.Logging.IsEnabled(LogLevel.Error))
                    {
                        owner.Logging.LogError(ex, $"[RT.{nameof(PacketDispatcherBase<>)}:{nameof(ExecutePacketHandlerAsync)}] handler-error opcode={opCode}");
                    }
                }
            }
        }

        if (this.Logging != null && this.Logging.IsEnabled(LogLevel.Warning))
        {
            this.Logging.LogWarning($"[RT.{nameof(PacketDispatcherBase<>)}:{nameof(ExecutePacketHandlerAsync)}] no-handler opcode={packet.Header.OpCode}");
        }
        return ValueTask.CompletedTask;
    }

    #endregion Protected Methods
}
