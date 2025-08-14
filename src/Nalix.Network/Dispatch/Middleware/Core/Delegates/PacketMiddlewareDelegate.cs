// Copyright (c) 2025 PPN Corporation. All rights reserved.


// Copyright (c) 2025 PPN Corporation. All rights reserved.

using Nalix.Network.Dispatch.Core.Context;

namespace Nalix.Network.Dispatch.Middleware.Core.Delegates;

/// <summary>
/// Delegate type representing a middleware handler for processing packets.
/// It receives the packet context, which contains the packet and its connection metadata,
/// and a delegate to invoke the next middleware component in the pipeline.
/// </summary>
/// <typeparam name="TPacket">The type of packet being processed.</typeparam>
/// <param name="context">
/// Encapsulates the packet and its associated connection metadata.
/// </param>
/// <param name="next">
/// Delegate to invoke the next middleware component.
/// </param>
public delegate System.Threading.Tasks.Task PacketMiddlewareDelegate<TPacket>(
    PacketContext<TPacket> context,
    System.Func<System.Threading.Tasks.Task> next);
