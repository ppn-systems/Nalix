// Copyright (c) 2025 PPN Corporation. All rights reserved.

#if DEBUG
[assembly: System.Runtime.CompilerServices.InternalsVisibleTo("Nalix.Network.Tests")]
[assembly: System.Runtime.CompilerServices.InternalsVisibleTo("Nalix.Network.Benchmarks")]
#endif

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
    System.Threading.Tasks.ValueTask HandleAsync(
        [System.Diagnostics.CodeAnalysis.AllowNull] System.Object result,
        [System.Diagnostics.CodeAnalysis.DisallowNull] PacketContext<TPacket> context);
}