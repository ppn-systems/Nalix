﻿using Nalix.Network.Dispatch.Core;

namespace Nalix.Network.Dispatch.Middleware.Core;

/// <summary>
/// Interface that standardizes middleware implementations for packet handling.
/// Provides a method for asynchronous invocation that allows chaining via the next delegate.
/// </summary>
/// <typeparam name="TPacket">The type of packet to be handled by this middleware.</typeparam>
public interface IPacketMiddleware<TPacket>
{
    /// <summary>
    /// Handles the processing of a packet in the middleware pipeline.
    /// </summary>
    /// <param name="context">Encapsulates the packet and its connection metadata.</param>
    /// <param name="next">Delegate to call the next middleware in the sequence.</param>
    /// <returns>A Task representing the asynchronous operation.</returns>
    System.Threading.Tasks.Task InvokeAsync(
        PacketContext<TPacket> context,
        System.Func<System.Threading.Tasks.Task> next);
}