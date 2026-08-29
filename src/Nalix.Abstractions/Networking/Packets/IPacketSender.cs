// Copyright (c) 2025-2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System.Threading;
using System.Threading.Tasks;

namespace Nalix.Abstractions.Networking.Packets;

/// <summary>
/// Abstracts packet sending with automatic transform (encrypt/compress)
/// </summary>
public interface IPacketSender : IPoolable
{
    /// <inheritdoc/>
    void Initialize<TPacket>(IPacketContext<TPacket> context) where TPacket : IPacket;

    /// <summary>
    /// Sends a packet, applying encryption/compression automatically
    /// based on the metadata of the current handler.
    /// </summary>
    /// <param name="packet">
    /// The packet instance to send.
    /// </param>
    /// <param name="ct">
    /// A cancellation token that can cancel the send operation.
    /// </param>
    ValueTask SendAsync(IPacket packet, CancellationToken ct = default);

    /// <summary>
    /// Sends a packet, explicitly overriding the encryption flag.
    /// </summary>
    /// <param name="packet">
    /// The packet instance to send.
    /// </param>
    /// <param name="forceEncrypt">
    /// <c>true</c> to force encryption even if metadata would not require it.
    /// </param>
    /// <param name="ct">
    /// A cancellation token that can cancel the send operation.
    /// </param>
    ValueTask SendAsync(IPacket packet, bool forceEncrypt, CancellationToken ct = default);

    /// <summary>
    /// Sends <paramref name="response"/> as a reply to the packet currently being processed,
    /// automatically echoing its <c>SequenceId</c> so the client's request/response and
    /// stream correlation logic can match the reply. Prefer this over <see cref="SendAsync(IPacket, CancellationToken)"/>
    /// whenever the outgoing packet is a direct response to the handler's inbound packet.
    /// </summary>
    /// <param name="response">
    /// The response packet instance to send.
    /// </param>
    /// <param name="ct">
    /// A cancellation token that can cancel the send operation.
    /// </param>
    ValueTask ReplyAsync(IPacket response, CancellationToken ct = default);
}
