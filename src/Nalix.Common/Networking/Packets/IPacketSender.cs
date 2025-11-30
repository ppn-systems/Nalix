// Copyright (c) 2025 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

namespace Nalix.Common.Networking.Packets;

/// <summary>
/// Abstracts packet sending with automatic transform (encrypt/compress)
/// </summary>
public interface IPacketSender<TPacket>
{
    /// <summary>
    /// Sends a packet, applying encryption/compression automatically
    /// based on the metadata of the current handler.
    /// </summary>
    System.Threading.Tasks.ValueTask<System.Boolean> SendAsync(
        TPacket packet,
        System.Threading.CancellationToken ct = default);

    /// <summary>
    /// Sends a packet, explicitly overriding the encryption flag.
    /// </summary>
    System.Threading.Tasks.ValueTask<System.Boolean> SendAsync(
        TPacket packet,
        System.Boolean forceEncrypt,
        System.Threading.CancellationToken ct = default);
}