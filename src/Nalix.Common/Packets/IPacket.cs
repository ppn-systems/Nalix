using Nalix.Common.Packets.Enums;

namespace Nalix.Common.Packets;

/// <summary>
/// Defines the contract for a network packet.
/// </summary>
/// [Length (2 bytes)][OpCode  (2 bytes)][ProtocolType (1 byte)][Checksum (4 bytes)][Timestamp (8 bytes)]
/// [OpCode   (2 bytes)][Type (1 byte)][Flags  (1 byte)][Priority  (1 byte)][Payload            ]
public interface IPacket : System.IEquatable<IPacket>, System.IDisposable
{
    #region Metadata

    /// <summary>
    /// Gets the total length of the packet.
    /// </summary>
    System.UInt16 Length { get; }

    /// <summary>
    /// Gets the command associated with the packet.
    /// </summary>
    System.UInt16 OpCode { get; }

    /// <summary>
    /// Gets the sequence number of the packet.
    /// </summary>
    System.Byte ProtocolType { get; }

    /// <summary>
    /// Gets the checksum of the packet.
    /// </summary>
    System.UInt32 Checksum { get; }

    /// <summary>
    /// Gets the timestamp when the packet was created.
    /// </summary>
    System.Int64 Timestamp { get; }

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
    System.ReadOnlyMemory<System.Byte> Payload { get; }

    #endregion Metadata

    #region Properties

    /// <summary>
    /// Gets a value indicating whether the packet is encrypted.
    /// </summary>
    System.Boolean IsEncrypted => (Flags & PacketFlags.Encrypted) != 0;

    /// <summary>
    /// Gets a value indicating whether the packet is compressed.
    /// </summary>
    System.Boolean IsCompression => (Flags & PacketFlags.Compressed) != 0;

    /// <summary>
    /// Computes a unique hash value for the packet using its key metadata.
    /// </summary>
    /// <returns>
    /// A 64-bit unsigned integer representing the packet's hash, composed of:
    /// <list type="bullet">
    /// <item><description><c>ProtocolType</c> (8 bits)</description></item>
    /// <item><description><c>OpCode</c> (16 bits)</description></item>
    /// <item><description><c>Type</c> (8 bits)</description></item>
    /// <item><description><c>OpCode</c> (8 bits)</description></item>
    /// <item><description><c>Flags</c> (8 bits)</description></item>
    /// <item><description>Lowest 40 bits of <c>Timestamp</c></description></item>
    /// </list>
    /// This hash can be used as a fast lookup key for caching or deduplication.
    /// </returns>
    System.Int32 Hash { get; }

    #endregion Properties

    #region Packet Methods

    /// <summary>
    /// Verifies if the packet's checksum is valid.
    /// </summary>
    System.Boolean IsValid();

    /// <summary>
    /// Checks if the packet has expired.
    /// </summary>
    /// <param name="timeout">The expiration timeout.</param>
    System.Boolean IsExpired(System.Int64 timeout);

    /// <summary>
    /// Checks if the packet has expired.
    /// </summary>
    /// <param name="timeout">The expiration timeout.</param>
    System.Boolean IsExpired(System.TimeSpan timeout);

    /// <summary>
    /// Serializes the packet into a byte array for transmission or storage.
    /// </summary>
    /// <returns>
    /// A byte array representing the serialized packet.
    /// </returns>
    System.Memory<System.Byte> Serialize();

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
    void Serialize(System.Span<System.Byte> buffer);

    /// <summary>
    /// Returns a string representation of the packet, useful for debugging or logging.
    /// </summary>
    /// <returns>
    /// A string that describes the packet's key attributes.
    /// </returns>
    System.String ToString();

    #endregion Packet Methods
}