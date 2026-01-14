// Copyright (c) 2025 PPN Corporation. All rights reserved.

using Nalix.Common.Core.Enums;
using Nalix.Common.Core.Primitives;

namespace Nalix.Common.Core.Abstractions;

/// <summary>
/// Defines the core contract for a unique identifier, including type,
/// machine association, serialization, and equality comparison.
/// </summary>
public interface ISnowflake
{
    /// <summary>
    /// Gets the <see cref="SnowflakeType"/> encoded within this identifier.
    /// </summary>
    SnowflakeType Type { get; }

    /// <summary>
    /// Gets the underlying 32-bit unsigned integer value of the identifier.
    /// </summary>
    System.UInt32 Value { get; }

    /// <summary>
    /// Determines whether this identifier is empty (uninitialized or default value).
    /// </summary>
    /// <returns>
    /// <see langword="true"/> if the identifier is empty; otherwise, <see langword="false"/>.
    /// </returns>
    System.Boolean IsEmpty { get; }

    /// <summary>
    /// Gets the machine ID component encoded within this identifier.
    /// </summary>
    System.UInt16 MachineId { get; }

    /// <summary>
    /// Converts the identifier to its 56-bit unsigned integer representation.
    /// </summary>
    /// <returns>
    /// A <see cref="UInt56"/> value representing the identifier as a 56-bit unsigned integer.
    /// </returns>
    UInt56 ToUInt56();

    /// <summary>
    /// Serializes the identifier into a byte array.
    /// </summary>
    /// <returns>
    /// A byte array containing the serialized representation of the identifier.
    /// </returns>
    System.Byte[] ToByteArray();

    /// <summary>
    /// Attempts to serialize the identifier into the provided byte span.
    /// </summary>
    /// <param name="destination">The destination byte span.</param>
    /// <returns>
    /// <see langword="true"/> if the identifier was successfully serialized;
    /// otherwise, <see langword="false"/> if the destination is too small.
    /// </returns>
    System.Boolean TryWriteBytes(System.Span<System.Byte> destination);
    /// <summary>
    /// Attempts to serialize the identifier into the provided byte span.
    /// </summary>
    /// <param name="destination">The destination byte span.</param>
    /// <param name="bytesWritten">When this method returns, contains the number of bytes written.</param>
    /// <returns>
    /// <see langword="true"/> if the identifier was successfully serialized;
    /// otherwise, <see langword="false"/> if the destination is too small.
    /// </returns>
    System.Boolean TryWriteBytes(
        [System.Diagnostics.CodeAnalysis.NotNull] System.Span<System.Byte> destination,
        [System.Diagnostics.CodeAnalysis.NotNullWhen(true)] out System.Int32 bytesWritten);
}
