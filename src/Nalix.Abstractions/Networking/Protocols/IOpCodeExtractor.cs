// Copyright (c) 2026 PPN Corporation. All rights reserved.
// Licensed under the Business Source License 1.1 (BSL-1.1).

namespace Nalix.Abstractions.Networking.Protocols;

/// <summary>
/// Defines a protocol-specific parser that extracts a packet header from raw payload bytes.
/// </summary>
public interface IOpCodeExtractor
{
    /// <summary>
    /// Reads the packet header from the specified payload.
    /// </summary>
    /// <param name="payload">The received packet payload.</param>
    /// <returns>The parsed packet header.</returns>
    ushort Extract(System.ReadOnlySpan<byte> payload);
}
