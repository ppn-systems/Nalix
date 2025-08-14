// Copyright (c) 2025 PPN Corporation. All rights reserved.


// Copyright (c) 2025 PPN Corporation. All rights reserved.

using Nalix.Network.Dispatch.Core;

namespace Nalix.Network.Dispatch.Results;

/// <summary>
/// Defines a handler interface for processing method return values of packet-handling methods
/// in a zero-allocation manner.
/// </summary>
/// <typeparam name="TPacket">The packet type being handled.</typeparam>
internal interface IReturnHandler<TPacket>
{
    /// <summary>
    /// Handles the result of a method call asynchronously.
    /// </summary>
    /// <param name="result">The method return value, may be null.</param>
    /// <param name="context">The context associated with the packet and connection.</param>
    System.Threading.Tasks.ValueTask HandleAsync(System.Object? result, PacketContext<TPacket> context);
}