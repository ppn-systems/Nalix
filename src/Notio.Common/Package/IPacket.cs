namespace Notio.Common.Package;

/// <summary>
/// Defines the contract for a network packet.
/// </summary>
public interface IPacket : System.IEquatable<IPacket>, System.IDisposable
{
    #region Metadata

    /// <summary>
    /// Gets the total length of the packet.
    /// </summary>
    ushort Length { get; }

    /// <summary>
    /// Gets the command associated with the packet.
    /// </summary>
    ushort Id { get; }

    /// <summary>
    /// Gets the packet identifier.
    /// </summary>
    byte Number { get; }

    /// <summary>
    /// Gets the checksum of the packet.
    /// </summary>
    uint Checksum { get; }

    /// <summary>
    /// Gets the timestamp when the packet was created.
    /// </summary>
    ulong Timestamp { get; }

    /// <summary>
    /// Gets the packet type.
    /// </summary>
    PacketType Type { get; }

    /// <summary>
    /// Gets or sets the packet flags.
    /// </summary>
    PacketFlags Flags { get; }

    /// <summary>
    /// Gets the packet priority.
    /// </summary>
    PacketPriority Priority { get; }

    /// <summary>
    /// Gets the payload of the packet.
    /// </summary>
    System.Memory<byte> Payload { get; }

    #endregion

    /// <summary>
    /// Gets a value indicating whether the packet is encrypted.
    /// </summary>
    bool IsEncrypted => (Flags & PacketFlags.Encrypted) != 0;

    /// <summary>
    /// Verifies if the packet's checksum is valid.
    /// </summary>
    bool IsValid();

    /// <summary>
    /// Checks if the packet has expired.
    /// </summary>
    /// <param name="timeout">The expiration timeout.</param>
    bool IsExpired(System.TimeSpan timeout);

    /// <summary>
    /// Serializes the packet into a byte array for transmission or storage.
    /// </summary>
    /// <returns>
    /// A byte array representing the serialized packet.
    /// </returns>
    System.Memory<byte> Serialize();

    /// <summary>
    /// Serializes the packet into the provided buffer.
    /// </summary>
    /// <param name="buffer">
    /// The span of bytes where the serialized packet data will be written.
    /// The buffer must be large enough to hold the entire packet.
    /// </param>
    /// <exception cref="Exceptions.PackageException">
    /// Thrown if the buffer is too small to contain the serialized packet.
    /// </exception>
    void Serialize(System.Span<byte> buffer);

    /// <summary>
    /// Returns a string representation of the packet, useful for debugging or logging.
    /// </summary>
    /// <returns>
    /// A string that describes the packet's key attributes.
    /// </returns>
    string ToString();
}
