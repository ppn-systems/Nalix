// Copyright (c) 2025 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.


// Copyright (c) 2025 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

namespace Nalix.Common.Messaging.Abstractions;

/// <summary>
/// Exposes static compression/decompression capability for a packet type.
/// </summary>
/// <typeparam name="TPacket">Packet type implementing <see cref="IPacket"/>.</typeparam>
public interface IPacketCompressor<TPacket> where TPacket : IPacket
{
    /// <summary>
    /// Compresses the specified packet instance.
    /// </summary>
    /// <param name="packet">The packet to compress.</param>
    /// <returns>COMPRESSED <typeparamref name="TPacket"/>.</returns>
    static abstract TPacket Compress(TPacket packet);

    /// <summary>
    /// Decompresses the specified packet instance.
    /// </summary>
    /// <param name="packet">The packet to decompress.</param>
    /// <returns>Decompressed <typeparamref name="TPacket"/>.</returns>
    static abstract TPacket Decompress(TPacket packet);
}
