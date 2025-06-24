// Copyright (c) 2025 PPN Corporation. All rights reserved.

using Nalix.Common.Caching;
using Nalix.Common.Connection;
using Nalix.Common.Packets.Abstractions;

namespace Nalix.Network.Abstractions;

/// <summary>
/// Defines a dispatcher interface for handling incoming network packets.
/// </summary>
/// <typeparam name="TPacket">
/// The packet type that implements <see cref="IPacket"/>.
/// </typeparam>
/// <remarks>
/// Implementations of this interface are responsible for processing incoming
/// packets from various representations, including raw byte buffers and fully
/// deserialized packet objects. The dispatcher determines how packets are
/// handled based on their content and the connection from which they originate.
/// </remarks>
public interface IPacketDispatch<TPacket> where TPacket : IPacket
{
    /// <summary>
    /// Handles an incoming packet represented as a <see cref="IBufferLease"/> array.
    /// </summary>
    /// <param name="packet">
    /// The byte array containing the raw packet data, or <see langword="null"/> to indicate no data.
    /// </param>
    /// <param name="connection">
    /// The connection from which the packet was received.
    /// </param>
    void HandlePacket(
        [System.Diagnostics.CodeAnalysis.AllowNull] IBufferLease packet,
        [System.Diagnostics.CodeAnalysis.NotNull] IConnection connection);

    /// <summary>
    /// Handles a fully deserialized packet instance.
    /// </summary>
    /// <param name="packet">
    /// The deserialized packet to be processed.
    /// </param>
    /// <param name="connection">
    /// The connection from which the packet was received.
    /// </param>
    void HandlePacket(
        [System.Diagnostics.CodeAnalysis.NotNull] TPacket packet,
        [System.Diagnostics.CodeAnalysis.NotNull] IConnection connection);
}
