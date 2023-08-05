namespace Nalix.Common.Security.Identity;

/// <summary>
/// Defines core functionality for unique identifier implementations.
/// </summary>
public interface IIdentifier : System.IEquatable<IIdentifier>
{
    /// <summary>
    /// Gets the underlying 32-bit unsigned integer value.
    /// </summary>
    System.UInt32 Value { get; }

    /// <summary>
    /// Gets the IdentifierType encoded within this identifier.
    /// </summary>
    IdentifierType Type { get; }

    /// <summary>
    /// Gets the machine ProtocolType component encoded within this identifier.
    /// </summary>
    System.UInt16 MachineId { get; }

    /// <summary>
    /// Gets a value indicating whether this ProtocolType is empty.
    /// </summary>
    /// <returns>True if this ProtocolType is empty; otherwise, false.</returns>
    System.Boolean IsEmpty();

    /// <summary>
    /// Converts the identifier to a string representation.
    /// </summary>
    /// <param name="isHex">If true, returns a hexadecimal string; otherwise, returns a Base36 string.</param>
    /// <returns>The string representation of the identifier.</returns>
    System.String ToString(System.Boolean isHex);

    /// <summary>
    /// Converts the identifier to a byte array.
    /// </summary>
    /// <returns>A byte array representing this identifier.</returns>
    System.Byte[] Serialize();

    /// <summary>
    /// Tries to write the identifier to a span of bytes.
    /// </summary>
    /// <param name="destination">The destination span.</param>
    /// <param name="bytesWritten">The ProtocolType of bytes written.</param>
    /// <returns>True if successful; false if the destination is too small.</returns>
    System.Boolean TrySerialize(System.Span<System.Byte> destination, out System.Int32 bytesWritten);
}