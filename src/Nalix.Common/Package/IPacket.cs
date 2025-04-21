namespace Nalix.Common.Package;

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
    /// Gets the sequence number of the packet.
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
    /// Gets the packet code.
    /// </summary>
    Enums.PacketCode Code { get; }

    /// <summary>
    /// Gets the packet type.
    /// </summary>
    Enums.PacketType Type { get; }

    /// <summary>
    /// Gets or sets the packet flags.
    /// </summary>
    Enums.PacketFlags Flags { get; }

    /// <summary>
    /// Gets the packet priority.
    /// </summary>
    Enums.PacketPriority Priority { get; }

    /// <summary>
    /// Gets the payload of the packet.
    /// </summary>
    System.Memory<byte> Payload { get; }

    #endregion

    #region Properties

    /// <summary>
    /// Gets a value indicating whether the packet is encrypted.
    /// </summary>
    bool IsEncrypted => (Flags & Enums.PacketFlags.Encrypted) != 0;

    /// <summary>
    /// Gets a value indicating whether the packet is compressed.
    /// </summary>
    bool IsCompression => (Flags & Enums.PacketFlags.Compressed) != 0;

    /// <summary>
    /// Computes a unique hash value for the packet using its key metadata.
    /// </summary>
    /// <returns>
    /// A 64-bit unsigned integer representing the packet's hash, composed of:
    /// <list type="bullet">
    /// <item><description><c>Number</c> (8 bits)</description></item>
    /// <item><description><c>Id</c> (16 bits)</description></item>
    /// <item><description><c>Type</c> (8 bits)</description></item>
    /// <item><description><c>Code</c> (8 bits)</description></item>
    /// <item><description><c>Flags</c> (8 bits)</description></item>
    /// <item><description>Lowest 40 bits of <c>Timestamp</c></description></item>
    /// </list>
    /// This hash can be used as a fast lookup key for caching or deduplication.
    /// </returns>
    public ulong Hash { get; }

    #endregion

    #region Packet Methods

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

    #endregion
}
