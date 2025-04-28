using Nalix.Common.Package.Metadata;
using System;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;

namespace Nalix.Network.Package.Engine.Serialization;

public static partial class PacketSerializer
{
    /// <summary>
    /// Deserializes the specified byte array to a packet.
    /// </summary>
    /// <param name="data">The byte array to deserialize.</param>
    /// <returns>The deserialized packet.</returns>
    public static Packet Deserialize(ReadOnlySpan<byte> data) => ReadPacket(data);

    /// <summary>
    /// Deserializes the specified ReadOnlyMemory to a packet.
    /// </summary>
    /// <param name="data">The ReadOnlyMemory to deserialize.</param>
    /// <returns>The deserialized packet.</returns>
    public static Packet Deserialize(ReadOnlyMemory<byte> data) => Deserialize(data.Span);

    /// <summary>
    /// Deserializes the specified byte array to a packet.
    /// </summary>
    /// <param name="data">The byte array to deserialize.</param>
    /// <returns>The deserialized packet.</returns>
    public static Packet Deserialize(byte[] data) => Deserialize(new ReadOnlySpan<byte>(data));

    /// <summary>
    /// Attempts to deserialize the specified source span to a packet.
    /// </summary>
    /// <param name="source">The source span to deserialize.</param>
    /// <param name="packet">When this method returns, contains the deserialized packet if the operation was successful; otherwise, the default packet value.</param>
    /// <returns>Returns true if deserialization was successful; otherwise, false.</returns>
    public static bool TryDeserialize(ReadOnlySpan<byte> source, [NotNullWhen(true)] out Packet packet)
    {
        packet = default;

        if (source.Length < PacketSize.Header)
            return false;

        try
        {
            short length = MemoryMarshal.Read<short>(source);
            if (length < PacketSize.Header || length > source.Length)
                return false;

            packet = ReadPacket(source[..length]);
            return true;
        }
        catch
        {
            return false;
        }
    }
}
