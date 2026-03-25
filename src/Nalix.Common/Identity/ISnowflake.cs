// Copyright (c) 2025-2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Diagnostics.CodeAnalysis;
using Nalix.Common.Primitives;

namespace Nalix.Common.Identity;

/// <summary>
/// Defines the core contract for a unique identifier, including type,
/// machine association, serialization, and equality comparison.
/// </summary>
public interface ISnowflake
{
    /// <summary>
    /// Gets the 32-bit value component.
    /// </summary>
    /// <remarks>
    /// Extracts the lower 32 bits of the identifier, representing the main value.
    /// This operation is optimized for performance using direct bit manipulation.
    /// </remarks>
    bool IsEmpty { get; }

    /// <summary>
    /// Gets the 8-bit type component.
    /// </summary>
    /// <remarks>
    /// Extracts bits 48-55 of the identifier, representing the snowflake type.
    /// This operation is optimized for performance using direct bit manipulation.
    /// </remarks>
    SnowflakeType Type { get; }

    /// <summary>
    /// Gets the underlying 32-bit unsigned integer value of the identifier.
    /// </summary>
    uint Value { get; }

    /// <summary>
    /// Gets the 16-bit machine identifier component.
    /// </summary>
    /// <remarks>
    /// Extracts bits 32-47 of the identifier, representing the machine ID.
    /// This operation is optimized for performance using direct bit manipulation.
    /// </remarks>
    ushort MachineId { get; }

    /// <summary>
    /// Converts this <see cref="ISnowflake"/> to its underlying 56-bit representation.
    /// </summary>
    /// <returns>The 56-bit unsigned integer value of this identifier.</returns>
    /// <remarks>
    /// This is the most efficient serialization method as it returns the internal representation directly.
    /// Use this when you need to store or transmit the identifier in a compact binary format.
    /// </remarks>
    /// <returns>
    /// A <see cref="UInt56"/> value representing the identifier as a 56-bit unsigned integer.
    /// </returns>
    UInt56 ToUInt56();

    /// <summary>
    /// Converts this <see cref="ISnowflake"/> to a 7-byte array.
    /// </summary>
    /// <returns>A new byte array containing the serialized identifier.</returns>
    /// <remarks>
    /// This method allocates a new 7-byte array and writes the identifier in little-endian format.
    /// The layout is: [0-3]=Value, [4-5]=MachineId, [6]=Type.
    /// For better performance, use <see cref="TryWriteBytes(Span{byte})"/> with a pre-allocated buffer.
    /// </remarks>
    byte[] ToByteArray();

    /// <summary>
    /// Attempts to write the serialized <see cref="ISnowflake"/> to the specified byte span.
    /// </summary>
    /// <param name="destination">The span to write the serialized bytes to.</param>
    /// <returns>
    /// <c>true</c> if the identifier was successfully serialized; <c>false</c> if <paramref name="destination"/>
    /// is too small (less than 7 bytes).
    /// </returns>
    /// <remarks>
    /// This overload is identical to <see cref="TryWriteBytes(Span{byte}, out int)"/> but does not
    /// return the number of bytes written. Use this when you don't need the byte count.
    /// </remarks>
    bool TryWriteBytes(Span<byte> destination);

    /// <summary>
    /// Attempts to write the serialized <see cref="ISnowflake"/> to the specified byte span.
    /// </summary>
    /// <param name="destination">The span to write the serialized bytes to.</param>
    /// <param name="bytesWritten">
    /// When this method returns, contains the number of bytes written to <paramref name="destination"/>.
    /// This is always 7 on success, or 0 on failure.
    /// </param>
    /// <returns>
    /// <c>true</c> if the identifier was successfully serialized; <c>false</c> if <paramref name="destination"/>
    /// is too small (less than 7> bytes).
    /// </returns>
    /// <remarks>
    /// This method writes the identifier in little-endian format: [0-3]=Value, [4-5]=MachineId, [6]=Type.
    /// The method validates the destination buffer size before writing to prevent buffer overflows.
    /// This is the recommended serialization method for performance-critical scenarios.
    /// </remarks>
    bool TryWriteBytes(
        Span<byte> destination,
        [NotNullWhen(true)] out int bytesWritten);
}
