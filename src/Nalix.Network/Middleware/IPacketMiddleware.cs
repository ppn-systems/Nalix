// Copyright (c) 2025 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using Nalix.Common.Networking.Packets;
using Nalix.Network.Routing;

namespace Nalix.Network.Middleware;

/// <summary>
/// Interface that standardizes middleware implementations for packet handling.
/// Provides a method for asynchronous invocation that allows chaining via the next delegate.
/// </summary>
/// <typeparam name="TPacket">The type of packet to be handled by this middleware.</typeparam>
public interface IPacketMiddleware<TPacket> where TPacket : IPacket
{
    /// <summary>
    /// Handles the processing of a packet in the middleware pipeline.
    /// </summary>
    /// <param name="context">Encapsulates the packet and its connection metadata.</param>
    /// <param name="next">Delegate to call the next middleware in the sequence.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    System.Threading.Tasks.Task InvokeAsync(
        [System.Diagnostics.CodeAnalysis.NotNull] PacketContext<TPacket> context,
        [System.Diagnostics.CodeAnalysis.NotNull]
        System.Func<System.Threading.CancellationToken, System.Threading.Tasks.Task> next);
}