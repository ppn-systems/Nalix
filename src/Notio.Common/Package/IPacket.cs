using System;

namespace Notio.Common.Package;

/// <summary>
/// Defines the contract for a network packet.
/// </summary>
public interface IPacket : IDisposable, IEquatable<IPacket>
{
    /// <summary>
    /// Gets the total length of the packet.
    /// </summary>
    ushort Length { get; }

    /// <summary>
    /// Gets the packet identifier.
    /// </summary>
    byte Id { get; }

    /// <summary>
    /// Gets the packet type.
    /// </summary>
    byte Type { get; }

    /// <summary>
    /// Gets or sets the packet flags.
    /// </summary>
    byte Flags { get; }

    /// <summary>
    /// Gets the packet priority.
    /// </summary>
    byte Priority { get; }

    /// <summary>
    /// Gets the command associated with the packet.
    /// </summary>
    ushort Command { get; }

    /// <summary>
    /// Gets the timestamp when the packet was created.
    /// </summary>
    ulong Timestamp { get; }

    /// <summary>
    /// Gets the checksum of the packet.
    /// </summary>
    uint Checksum { get; }

    /// <summary>
    /// Gets the payload of the packet.
    /// </summary>
    Memory<byte> Payload { get; }

    /// <summary>
    /// Verifies if the packet's checksum is valid.
    /// </summary>
    bool IsValid();

    /// <summary>
    /// Checks if the packet has expired.
    /// </summary>
    /// <param name="timeout">The expiration timeout.</param>
    bool IsExpired(TimeSpan timeout);
}
