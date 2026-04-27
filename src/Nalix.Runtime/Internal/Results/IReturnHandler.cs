// Copyright (c) 2025-2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System.ComponentModel;
using System.Threading.Tasks;
using Nalix.Abstractions.Networking.Packets;
using Nalix.Runtime.Dispatching;

#if DEBUG
[assembly: System.Runtime.CompilerServices.InternalsVisibleTo("Nalix.Network.Tests")]
[assembly: System.Runtime.CompilerServices.InternalsVisibleTo("Nalix.Network.Benchmarks")]
#endif

namespace Nalix.Runtime.Internal.Results;

/// <summary>
/// Defines a handler interface for processing method return values of packet-handling methods
/// in a zero-allocation manner.
/// </summary>
/// <typeparam name="TPacket">The packet type being handled.</typeparam>
[EditorBrowsable(EditorBrowsableState.Never)]
internal interface IReturnHandler<TPacket> where TPacket : IPacket
{
    /// <summary>
    /// Handles the result of a method call asynchronously.
    /// </summary>
    /// <param name="result">The method return value, may be null.</param>
    /// <param name="context">The context associated with the packet and connection.</param>
    ValueTask HandleAsync(object? result, PacketContext<TPacket> context);
}
