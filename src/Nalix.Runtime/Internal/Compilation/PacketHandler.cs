// Copyright (c) 2025-2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System;
using System.ComponentModel;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Nalix.Abstractions.Networking;
using Nalix.Abstractions.Networking.Packets;
using Nalix.Runtime.Dispatching;
using Nalix.Runtime.Internal.Results;

#if DEBUG
[assembly: InternalsVisibleTo("Nalix.Runtime.Tests")]
[assembly: InternalsVisibleTo("Nalix.Runtime.Pipeline.Tests")]
[assembly: InternalsVisibleTo("Nalix.Network.Tests")]
[assembly: InternalsVisibleTo("Nalix.Network.Benchmarks")]
[assembly: InternalsVisibleTo("Nalix.Runtime.Benchmarks")]
#endif

namespace Nalix.Runtime.Internal.Compilation;

/// <summary>
/// Immutable dispatch record that pairs packet metadata with the compiled invoker
/// used to execute a packet handler without reflection on the hot path.
/// </summary>
/// <typeparam name="TPacket">The packet type handled by this delegate.</typeparam>
/// <param name="opCode">The opcode mapped to this handler.</param>
/// <param name="metadata">Dispatch metadata used for runtime policies.</param>
/// <param name="controllerInstance">The controller instance that owns the handler method.</param>
/// <param name="method">The reflected handler method information.</param>
/// <param name="returnType">The handler return type.</param>
/// <param name="compiledInvoker">Compiled delegate used to invoke the handler.</param>
/// <param name="expectedPacketType">
/// Cached concrete packet runtime type expected by the handler, or <see langword="null"/>
/// when runtime packet type checks are not required.
/// </param>
/// <param name="returnHandler">
/// Cached outbound return handler resolved for <paramref name="returnType"/>.
/// </param>
/// <param name="rawInvoker">
/// Compiled delegate for raw handlers that receive <see cref="BufferContext"/> instead
/// of a deserialized packet. <see langword="null"/> for standard handlers.
/// </param>
[StructLayout(LayoutKind.Sequential)]
[EditorBrowsable(EditorBrowsableState.Never)]
[method: MethodImpl(MethodImplOptions.AggressiveInlining)]
internal readonly struct PacketHandler<TPacket>(
    ushort opCode, PacketMetadata metadata,
    object controllerInstance, MethodInfo method, Type returnType,
    Func<object, PacketContext<TPacket>, ValueTask<object>> compiledInvoker,
    Type? expectedPacketType,
    IReturnHandler<TPacket> returnHandler,
    Func<object, BufferContext, ValueTask<object>>? rawInvoker = null) where TPacket : IPacket
{
    #region Fields

    /// <summary>
    /// The OpCode associated with this packet handler.
    /// </summary>
    public readonly ushort OpCode = opCode;

    /// <summary>
    /// The return type of the handler method.
    /// </summary>
    public readonly Type ReturnType = returnType;

    /// <summary>
    /// Metadata for this handler, including timeout, rate limiting, and permissions.
    /// </summary>
    public readonly PacketMetadata Metadata = metadata;

    /// <summary>
    /// The controller instance to invoke the handler on (cached for reuse).
    /// </summary>
    public readonly object Instance = controllerInstance;

    /// <summary>
    /// The original method info, useful for debugging or reflection fallback.
    /// </summary>
    public readonly MethodInfo MethodInfo = method;

    /// <summary>
    /// A compiled delegate for invoking the handler directly.
    /// This is the performance-critical entry point used every time a packet is dispatched.
    /// It avoids reflection, parameter boxing, and per-call delegate allocation.
    /// </summary>
    public readonly Func<object, PacketContext<TPacket>, ValueTask<object>> Invoker = compiledInvoker;

    /// <summary>
    /// Concrete packet type expected by this handler, or <see langword="null"/>
    /// when no strict runtime type check is required.
    /// </summary>
    public readonly Type? ExpectedPacketType = expectedPacketType;

    /// <summary>
    /// Cached return handler for the method return type.
    /// </summary>
    public readonly IReturnHandler<TPacket> ReturnHandler = returnHandler;

    /// <summary>
    /// Compiled delegate for raw handlers that receive <see cref="BufferContext"/> and
    /// the original wire bytes directly, bypassing packet deserialization.
    /// <see langword="null"/> for standard handlers.
    /// </summary>
    public readonly Func<object, BufferContext, ValueTask<object>>? RawInvoker = rawInvoker;

    /// <summary>
    /// Returns <see langword="true"/> when this handler operates on raw bytes
    /// (<see cref="ReadOnlyMemory{T}"/>) instead of a deserialized <typeparamref name="TPacket"/>.
    /// </summary>
    public readonly bool IsRawHandler => RawInvoker is not null;

    #endregion Fields

    #region Methods

    /// <summary>
    /// Executes the handler using the compiled delegate for maximum performance.
    /// This is the zero-allocation path that the dispatcher calls for every packet.
    /// </summary>
    /// <param name="context">The packet context containing the request and metadata.</param>
    /// <returns>
    /// A <see cref="ValueTask{TResult}"/> that completes with the handler's result.
    /// </returns>
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    public ValueTask<object> ExecuteAsync(PacketContext<TPacket> context) => Invoker(Instance, context);

    /// <summary>
    /// Executes a raw handler using the compiled delegate. The handler receives
    /// a <see cref="BufferContext"/> containing the original wire bytes.
    /// </summary>
    /// <param name="context">The buffer context with raw data and connection.</param>
    /// <returns>A <see cref="ValueTask{TResult}"/> that completes with the handler's result.</returns>
    /// <exception cref="InvalidOperationException">
    /// Thrown when this handler is not a raw handler (<see cref="IsRawHandler"/> is <see langword="false"/>).
    /// </exception>
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    public ValueTask<object> ExecuteRawAsync(BufferContext context)
        => RawInvoker is not null
            ? RawInvoker(Instance, context)
            : throw new InvalidOperationException(
                $"Handler 0x{OpCode:X4} is not a raw handler. Use ExecuteAsync instead.");

    /// <summary>
    /// Determines whether this handler can be executed for the specified packet context.
    /// </summary>
    /// <param name="context">The packet context to validate for execution.</param>
    /// <returns><see langword="true"/> if the handler can be executed; otherwise, <see langword="false"/>.</returns>
    /// <remarks>
    /// This method can be extended to implement validation logic such as:
    /// <list type="bullet">
    /// <item><description>Permission checks</description></item>
    /// <item><description>Rate limiting</description></item>
    /// <item><description>Custom filters</description></item>
    /// </list>
    /// </remarks>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public bool CanExecute(PacketContext<TPacket> context)
    {
        // SEC-77: Enforce permission policy by default on the hot path.
        // Middleware is still recommended for logging and more complex policies,
        // but this provides a fail-closed baseline in the dispatcher itself.
        if (Metadata.Permission is { } permission &&
        permission.Level > context.Connection.Level)
        {
            return false;
        }

        return true;
    }

    /// <summary>
    /// Determines whether this raw handler can be executed for the specified connection.
    /// </summary>
    /// <param name="connection">The connection to validate.</param>
    /// <returns><see langword="true"/> if the handler can be executed; otherwise, <see langword="false"/>.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public bool CanExecuteRaw(IConnection connection)
    {
        if (Metadata.Permission is { } permission &&
            permission.Level > connection.Level)
        {
            return false;
        }

        return true;
    }

    #endregion Methods
}
