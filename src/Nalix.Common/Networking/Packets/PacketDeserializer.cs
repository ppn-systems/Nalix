// Copyright (c) 2025 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

namespace Nalix.Common.Networking.Packets;

/// <summary>
/// Represents a delegate that constructs an <see cref="IPacket"/>
/// instance from a raw byte buffer.
/// </summary>
/// <param name="raw">
/// The raw byte span containing the serialized packet data.
/// </param>
/// <returns>
/// An <see cref="IPacket"/> instance created from the provided buffer.
/// </returns>
[System.Diagnostics.DebuggerDisplay("PacketDeserializer Delegate")]
public delegate IPacket PacketDeserializer(System.ReadOnlySpan<byte> raw);