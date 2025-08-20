// Copyright (c) 2025 PPN Corporation. All rights reserved.

using Nalix.Common.Security.Types;

namespace Nalix.Common.Security.Abstractions;

/// <summary>
/// Defines the core contract for a unique identifier, including type,
/// machine association, serialization, and equality comparison.
/// </summary>
public interface IIdentifier : System.IEquatable<IIdentifier>
{
    /// <summary>
    /// Gets the underlying 32-bit unsigned integer value of the identifier.
    /// </summary>
    System.UInt32 Value { get; }

    /// <summary>
    /// Gets the <see cref="IdentifierType"/> encoded within this identifier.
    /// </summary>
    IdentifierType Type { get; }

    /// <summary>
    /// Gets the machine ID component encoded within this identifier.
    /// </summary>
    System.UInt16 MachineId { get; }

    /// <summary>
    /// Determines whether this identifier is empty (uninitialized or default value).
    /// </summary>
    /// <returns>
    /// <see langword="true"/> if the identifier is empty; otherwise, <see langword="false"/>.
    /// </returns>
    System.Boolean IsEmpty();

    /// <summary>
    /// Converts the identifier to a string representation.
    /// </summary>
    /// <param name="isHex">
    /// If <see langword="true"/>, returns a hexadecimal string;  
    /// otherwise, returns a Base36-encoded string.
    /// </param>
    /// <returns>The string representation of the identifier.</returns>
    System.String ToString(System.Boolean isHex);

    /// <summary>
    /// Serializes the identifier into a byte array.
    /// </summary>
    /// <returns>
    /// A byte array containing the serialized representation of the identifier.
    /// </returns>
    System.Byte[] Serialize();

    /// <summary>
    /// Attempts to serialize the identifier into the provided byte span.
    /// </summary>
    /// <param name="destination">The destination byte span.</param>
    /// <param name="bytesWritten">When this method returns, contains the number of bytes written.</param>
    /// <returns>
    /// <see langword="true"/> if the identifier was successfully serialized;  
    /// otherwise, <see langword="false"/> if the destination is too small.
    /// </returns>
    System.Boolean TrySerialize(System.Span<System.Byte> destination, out System.Int32 bytesWritten);
}
