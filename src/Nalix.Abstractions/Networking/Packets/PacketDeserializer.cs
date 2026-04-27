// Copyright (c) 2025-2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Diagnostics;

namespace Nalix.Abstractions.Networking.Packets;

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
[DebuggerDisplay("PacketDeserializer Delegate")]
public delegate IPacket PacketDeserializer(ReadOnlySpan<byte> raw);

/// <summary>
/// Represents a delegate that deserializes packet data into an existing
/// packet reference when possible.
/// </summary>
/// <param name="raw">The raw byte span containing serialized packet data.</param>
/// <param name="value">
/// Existing packet instance reference that may be reused or replaced by the concrete deserializer.
/// </param>
/// <returns>The deserialized packet instance.</returns>
[DebuggerDisplay("PacketDeserializerInto Delegate")]
public delegate TPacket PacketDeserializerInto<TPacket>(ReadOnlySpan<byte> raw, ref TPacket value) where TPacket : IPacket;
