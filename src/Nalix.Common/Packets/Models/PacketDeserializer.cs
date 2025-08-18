using Nalix.Common.Packets.Abstractions;

namespace Nalix.Common.Packets.Models;

/// <summary>
/// Represents a delegate that constructs an <see cref="IPacket"/> 
/// instance from a raw byte buffer.
/// </summary>
/// <param name="raw">
/// The raw byte span containing the serialized packet data.
/// </param>
/// <returns>
/// An <see cref="IPacket"/> instance created from the provided buffer.
/// </returns>
public delegate IPacket PacketDeserializer(System.ReadOnlySpan<System.Byte> raw);
