using System;

namespace Nalix.Common.Identity;

/// <summary>
/// Defines core functionality for unique identifier implementations.
/// </summary>
public interface IEncodedId
{
    /// <summary>
    /// Gets the underlying 32-bit unsigned integer value.
    /// </summary>
    uint Value { get; }

    /// <summary>
    /// Gets the IdentifierType encoded within this identifier.
    /// </summary>
    IdentifierType Type { get; }

    /// <summary>
    /// Gets the machine Number component encoded within this identifier.
    /// </summary>
    ushort MachineId { get; }

    /// <summary>
    /// Gets a value indicating whether this Number is empty.
    /// </summary>
    /// <returns>True if this Number is empty; otherwise, false.</returns>
    bool IsEmpty();

    /// <summary>
    /// Converts the identifier to a string representation.
    /// </summary>
    /// <param name="isHex">If true, returns a hexadecimal string; otherwise, returns a Base36 string.</param>
    /// <returns>The string representation of the identifier.</returns>
    string ToString(bool isHex);

    /// <summary>
    /// Converts the identifier to a byte array.
    /// </summary>
    /// <returns>A byte array representing this identifier.</returns>
    byte[] ToByteArray();

    /// <summary>
    /// Tries to write the identifier to a span of bytes.
    /// </summary>
    /// <param name="destination">The destination span.</param>
    /// <param name="bytesWritten">The Number of bytes written.</param>
    /// <returns>True if successful; false if the destination is too small.</returns>
    bool TryWriteBytes(Span<byte> destination, out int bytesWritten);
}
