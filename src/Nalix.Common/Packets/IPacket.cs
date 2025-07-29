using Nalix.Common.Caching;
using Nalix.Common.Connection.Protocols;
using Nalix.Common.Packets.Enums;
using Nalix.Common.Serialization;
using Nalix.Common.Serialization.Attributes;

namespace Nalix.Common.Packets;

/// <summary>
/// Defines the contract for a network packet.
/// </summary>
[SerializePackable(SerializeLayout.Sequential)]
public interface IPacket : IPoolable
{
    #region Metadata

    /// <summary>
    /// Gets the total length of the packet.
    /// </summary>
    System.UInt16 Length { get; }

    /// <summary>
    /// Gets the magic number used to identify the packet type or protocol.
    /// </summary>
    System.UInt32 MagicNumber { get; }

    /// <summary>
    /// Gets the command associated with the packet.
    /// </summary>
    System.UInt16 OpCode { get; }

    /// <summary>
    /// Gets or sets the packet flags.
    /// </summary>
    PacketFlags Flags { get; }

    /// <summary>
    /// Gets the packet priority.
    /// </summary>
    PacketPriority Priority { get; }

    /// <summary>
    /// Gets the sequence number of the packet.
    /// </summary>
    TransportProtocol Transport { get; }

    /// <summary>
    /// Computes a unique hash value for the packet using its key metadata.
    /// </summary>
    /// <returns>
    /// A 64-bit unsigned integer representing the packet's hash, composed of:
    /// This hash can be used as a fast lookup key for caching or deduplication.
    /// </returns>
    [SerializeIgnore]
    System.Int32 Hash { get; }

    #endregion Metadata

    #region Packet Methods

    /// <summary>
    /// Serializes the packet into a byte array for transmission or storage.
    /// </summary>
    /// <returns>
    /// A byte array representing the serialized packet.
    /// </returns>
    System.Byte[] Serialize();

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

    #endregion Packet Methods
}