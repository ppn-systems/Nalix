// Copyright (c) 2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System;
using Nalix.Common.Networking.Packets;

namespace Nalix.SDK.Tools.Models;

/// <summary>
/// Stores an immutable packet snapshot that can be reopened later.
/// </summary>
public sealed class PacketSnapshot
{
    /// <summary>
    /// Gets or sets the packet type name at snapshot time.
    /// </summary>
    public required string PacketTypeName { get; init; }

    /// <summary>
    /// Gets or sets the serialized bytes captured for the packet.
    /// </summary>
    public required byte[] RawBytes { get; init; }

    /// <summary>
    /// Gets or sets the packet opcode.
    /// </summary>
    public required ushort OpCode { get; init; }

    /// <summary>
    /// Gets or sets the packet magic number.
    /// </summary>
    public required uint MagicNumber { get; init; }

    /// <summary>
    /// Creates a snapshot from the provided packet.
    /// </summary>
    /// <param name="packet">The packet to snapshot.</param>
    /// <returns>The created snapshot.</returns>
    public static PacketSnapshot FromPacket(IPacket packet)
    {
        ArgumentNullException.ThrowIfNull(packet);

        return new PacketSnapshot
        {
            PacketTypeName = packet.GetType().FullName ?? packet.GetType().Name,
            RawBytes = packet.Serialize(),
            OpCode = packet.OpCode,
            MagicNumber = packet.MagicNumber
        };
    }
}
