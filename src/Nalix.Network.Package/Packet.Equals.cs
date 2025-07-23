using Nalix.Common.Packets;

namespace Nalix.Network.Package;

public readonly partial struct Packet : System.IEquatable<Packet>
{
    /// <summary>
    /// Returns a hash code for this packet.
    /// </summary>
    /// <returns>A hash code value.</returns>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public override System.Int32 GetHashCode()
    {
        // Initial hash with key fields
        System.Int32 hash = (System.Byte)Type;
        hash = (hash * 397) ^ (System.Byte)Flags;
        hash = (hash * 397) ^ (System.Byte)OpCode;
        hash = (hash * 397) ^ (System.Byte)Priority;

        // For small payloads, use the full content
        System.ReadOnlySpan<System.Byte> span = Payload.Span;

        // Add payload contribution - handle cases efficiently
        if (span.Length > 0)
        {
            // Create a robust hash that includes size, start, and end of payload
            hash = (hash * 397) ^ span.Length;

            if (span.Length <= MaxStackAllocSize)
            {
                for (System.Int32 i = 0; i < span.Length; i += sizeof(System.Int32))
                {
                    System.Int32 chunk = 0;
                    System.Int32 bytesToRead = System.Math.Min(sizeof(System.Int32), span.Length - i);
                    for (System.Int32 j = 0; j < bytesToRead; j++)
                    {
                        chunk |= span[i + j] << (j * 8);
                    }
                    hash = (hash * 397) ^ chunk;
                }
            }
            else
            {
                // For larger payloads, use beginning, middle and end samples

                // Beginning (up to 64 bytes)
                System.Int32 bytesToSample = System.Math.Min(64, span.Length);
                for (System.Int32 i = 0; i < bytesToSample; i += sizeof(System.Int32))
                {
                    System.Int32 chunk = 0;
                    System.Int32 bytesToRead = System.Math.Min(sizeof(System.Int32), bytesToSample - i);
                    for (System.Int32 j = 0; j < bytesToRead; j++)
                    {
                        chunk |= span[i + j] << (j * 8);
                    }
                    hash = (hash * 397) ^ chunk;
                }

                // End (up to 64 bytes)
                if (span.Length > 128)
                {
                    System.Int32 startIndex = span.Length - 64;
                    for (System.Int32 i = 0; i < 64; i += sizeof(System.Int32))
                    {
                        System.Int32 chunk = 0;
                        System.Int32 bytesToRead = System.Math.Min(sizeof(System.Int32), 64 - i);
                        for (System.Int32 j = 0; j < bytesToRead; j++)
                        {
                            chunk |= span[startIndex + i + j] << (j * 8);
                        }
                        hash = (hash * 397) ^ chunk;
                    }
                }

                // Include the checksum for additional mixing
                hash = (hash * 397) ^ (System.Int32)Checksum;
            }
        }

        return hash;
    }

    /// <summary>
    /// Compares this packet with another packet for equality.
    /// </summary>
    /// <param name="other">The packet to compare with.</param>
    /// <returns>True if the packets are equal; otherwise, false.</returns>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public System.Boolean Equals(IPacket? other)
    {
        if (other is null)
        {
            return false;
        }

        // Quick field comparison first
        if (Type != other.Type ||
            Flags != other.Flags ||
            OpCode != other.OpCode ||
            Priority != other.Priority ||
            Payload.Length != other.Payload.Length)
        {
            return false;
        }

        System.ReadOnlySpan<System.Byte> span1 = Payload.Span;
        System.ReadOnlySpan<System.Byte> span2 = other.Payload.Span;

        return span1.Length < 32
            ? System.MemoryExtensions.SequenceEqual(span1, span2)
            : System.MemoryExtensions.SequenceEqual(span1[..16], span2[..16]) &&
            System.MemoryExtensions.SequenceEqual(span1[^16..], span2[^16..]);
    }

    /// <summary>
    /// Compares this packet with another packet for equality.
    /// </summary>
    /// <param name="other">The packet to compare with.</param>
    /// <returns>True if the packets are equal; otherwise, false.</returns>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public System.Boolean Equals(Packet other) =>
        Type == other.Type &&
        Flags == other.Flags &&
        OpCode == other.OpCode &&
        Priority == other.Priority &&
        Payload.Length == other.Payload.Length &&
        System.MemoryExtensions.SequenceEqual(Payload.Span, other.Payload.Span);

    /// <summary>
    /// Compares this packet with another object for equality.
    /// </summary>
    /// <param name="obj">The object to compare with.</param>
    /// <returns>True if the objects are equal; otherwise, false.</returns>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public override System.Boolean Equals(System.Object? obj) => obj is Packet packet && Equals(packet);

    /// <summary>
    /// Determines whether two packets are equal.
    /// </summary>
    /// <param name="left">The first packet.</param>
    /// <param name="right">The second packet.</param>
    /// <returns>True if the packets are equal; otherwise, false.</returns>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public static System.Boolean operator ==(Packet left, Packet right) => left.Equals(right);

    /// <summary>
    /// Determines whether two packets are not equal.
    /// </summary>
    /// <param name="left">The first packet.</param>
    /// <param name="right">The second packet.</param>
    /// <returns>True if the packets are not equal; otherwise, false.</returns>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public static System.Boolean operator !=(Packet left, Packet right) => !left.Equals(right);
}
