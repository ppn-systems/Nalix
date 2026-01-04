// Copyright (c) 2025-2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System.Threading;
using System.Threading.Tasks;

namespace Nalix.Common.Networking.Packets;

/// <summary>
/// Abstracts packet sending with automatic transform (encrypt/compress)
/// </summary>
/// <typeparam name="TPacket"></typeparam>
public interface IPacketSender<TPacket>
{
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
    ValueTask<bool> SendAsync(TPacket packet, CancellationToken ct = default);

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
    ValueTask<bool> SendAsync(TPacket packet, bool forceEncrypt, CancellationToken ct = default);
}
