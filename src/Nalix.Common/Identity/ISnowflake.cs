// Copyright (c) 2025-2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Diagnostics.CodeAnalysis;
using Nalix.Common.Primitives;

namespace Nalix.Common.Identity;

/// <summary>
/// Describes the common shape of a Nalix snowflake identifier.
/// A snowflake combines a value, machine id, and type tag into a compact 56-bit form.
/// </summary>
public interface ISnowflake
{
    /// <summary>
    /// Gets whether the identifier is empty.
    /// </summary>
    /// <remarks>
    /// Empty identifiers are used as a sentinel and should not be treated as valid IDs.
    /// </remarks>
    bool IsEmpty { get; }

    /// <summary>
    /// Gets the identifier type tag.
    /// </summary>
    /// <remarks>
    /// The type tag helps the system distinguish between identifier families such as
    /// accounts, sessions, and system objects.
    /// </remarks>
    SnowflakeType Type { get; }

    /// <summary>
    /// Gets the value component of the identifier.
    /// </summary>
    uint Value { get; }

    /// <summary>
    /// Gets the machine identifier component.
    /// </summary>
    /// <remarks>
    /// The machine id lets identifiers be generated independently on different machines
    /// while still remaining unique.
    /// </remarks>
    ushort MachineId { get; }

    /// <summary>
    /// Converts this <see cref="ISnowflake"/> to its compact 56-bit representation.
    /// </summary>
    /// <remarks>
    /// This is the smallest wire representation for the identifier and is useful when
    /// storing or transmitting IDs in binary form.
    /// </remarks>
    /// <returns>
    /// A <see cref="UInt56"/> value representing the identifier as a 56-bit unsigned integer.
    /// </returns>
    UInt56 ToUInt56();

    /// <summary>
    /// Converts this <see cref="ISnowflake"/> to a 7-byte array.
    /// </summary>
    /// <remarks>
    /// The layout is little-endian: [0-3]=Value, [4-5]=MachineId, [6]=Type.
    /// Use <see cref="TryWriteBytes(Span{byte})"/> when you already have a buffer.
    /// </remarks>
    /// <returns>A new 7-byte array containing the serialized identifier.</returns>
    byte[] ToByteArray();

    /// <summary>
    /// Attempts to write the serialized identifier to the specified byte span.
    /// </summary>
    /// <param name="destination">The span to write the serialized bytes to.</param>
    /// <returns>
    /// <c>true</c> if the identifier was written successfully; otherwise, <c>false</c>.
    /// </returns>
    /// <remarks>
    /// This is the simplest zero-allocation path when the caller only cares about success
    /// or failure.
    /// </remarks>
    bool TryWriteBytes(Span<byte> destination);

    /// <summary>
    /// Attempts to write the serialized identifier to the specified byte span.
    /// </summary>
    /// <param name="destination">The span to write the serialized bytes to.</param>
    /// <param name="bytesWritten">
    /// When this method returns, contains the number of bytes written to <paramref name="destination"/>.
    /// This is always 7 on success, or 0 on failure.
    /// </param>
    /// <returns>
    /// <c>true</c> if the identifier was written successfully; otherwise, <c>false</c>.
    /// </returns>
    /// <remarks>
    /// This is the recommended zero-allocation path when the caller also needs the byte
    /// count and already owns the destination buffer.
    /// </remarks>
    bool TryWriteBytes(Span<byte> destination, [NotNullWhen(true)] out int bytesWritten);
}
