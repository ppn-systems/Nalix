// Copyright (c) 2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Threading.Tasks;
using Nalix.Abstractions.Networking.Packets;

namespace Nalix.Runtime.Dispatching;

/// <summary>
/// A builder interface exposed to source-generated code to allow registering packet handlers
/// without needing access to internal framework structures.
/// </summary>
/// <typeparam name="TPacket">The base packet type for the dispatch pipeline.</typeparam>
public interface IPacketHandlerBuilder<TPacket> where TPacket : IPacket
{
    /// <summary>
    /// Registers a new packet handler.
    /// </summary>
    /// <param name="opCode">The protocol opcode.</param>
    /// <param name="metadata">The metadata describing the handler's attributes.</param>
    /// <param name="methodName">The name of the handler method (for logging purposes).</param>
    /// <param name="instance">The controller instance, or null if all handlers are static.</param>
    /// <param name="returnType">The expected return type of the handler method.</param>
    /// <param name="expectedPacketType">The specific expected packet type, or null if any packet is accepted.</param>
    /// <param name="invoker">The compiled zero-allocation proxy delegate that executes the handler.</param>
    void RegisterHandler(
        ushort opCode,
        PacketMetadata metadata,
        string methodName,
        object? instance,
        Type returnType,
        Type? expectedPacketType,
        Func<object?, PacketContext<TPacket>, ValueTask<object?>> invoker);
}

/// <summary>
/// A non-generic compiler interface implemented by source-generated code.
/// Allows the <see cref="PacketHandlerRegistry"/> to invoke the generic builder without reflection.
/// </summary>
public interface IPacketHandlerCompiler
{
    /// <summary>
    /// Initializes dependencies for all generated controllers before building handlers.
    /// </summary>
    void InitializeDependencies();

    /// <summary>
    /// Builds and registers handlers into the generic builder.
    /// </summary>
    void Build<TPacket>(IPacketHandlerBuilder<TPacket> builder, Func<object> factory) where TPacket : IPacket;
}
