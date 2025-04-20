namespace Notio.Network.Package;

public readonly partial struct Packet : System.IEquatable<Packet>
{
    /// <summary>
    /// Returns a hash code for this packet.
    /// </summary>
    /// <returns>A hash code value.</returns>
    [System.Runtime.CompilerServices.MethodImpl(
            System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public override int GetHashCode()
    {
        // Initial hash with key fields
        int hash = (byte)Type;
        hash = (hash * 397) ^ (byte)Flags;
        hash = (hash * 397) ^ (byte)Id;
        hash = (hash * 397) ^ (byte)Priority;

        // For small payloads, use the full content
        System.ReadOnlySpan<byte> span = Payload.Span;

        // Add payload contribution - handle cases efficiently
        if (span.Length > 0)
        {
            // Create a robust hash that includes size, start, and end of payload
            hash = (hash * 397) ^ span.Length;

            if (span.Length <= MaxStackAllocSize)
            {
                for (int i = 0; i < span.Length; i += sizeof(int))
                {
                    int chunk = 0;
                    int bytesToRead = System.Math.Min(sizeof(int), span.Length - i);
                    for (int j = 0; j < bytesToRead; j++)
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
                int bytesToSample = System.Math.Min(64, span.Length);
                for (int i = 0; i < bytesToSample; i += sizeof(int))
                {
                    int chunk = 0;
                    int bytesToRead = System.Math.Min(sizeof(int), bytesToSample - i);
                    for (int j = 0; j < bytesToRead; j++)
                    {
                        chunk |= span[i + j] << (j * 8);
                    }
                    hash = (hash * 397) ^ chunk;
                }

                // End (up to 64 bytes)
                if (span.Length > 128)
                {
                    int startIndex = span.Length - 64;
                    for (int i = 0; i < 64; i += sizeof(int))
                    {
                        int chunk = 0;
                        int bytesToRead = System.Math.Min(sizeof(int), 64 - i);
                        for (int j = 0; j < bytesToRead; j++)
                        {
                            chunk |= span[startIndex + i + j] << (j * 8);
                        }
                        hash = (hash * 397) ^ chunk;
                    }
                }

                // Include the checksum for additional mixing
                hash = (hash * 397) ^ (int)Checksum;
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
    public bool Equals(Common.Package.IPacket? other)
    {
        if (other is null)
            return false;

        // Quick field comparison first
        if (Type != other.Type ||
            Flags != other.Flags ||
            Id != other.Id ||
            Priority != other.Priority ||
            Payload.Length != other.Payload.Length)
        {
            return false;
        }

        System.ReadOnlySpan<byte> span1 = Payload.Span;
        System.ReadOnlySpan<byte> span2 = other.Payload.Span;

        if (span1.Length < 32)
            return System.MemoryExtensions.SequenceEqual(span1, span2);

        return System.MemoryExtensions.SequenceEqual(span1[..16], span2[..16]) &&
            System.MemoryExtensions.SequenceEqual(span1[^16..], span2[^16..]);
    }

    /// <summary>
    /// Compares this packet with another packet for equality.
    /// </summary>
    /// <param name="other">The packet to compare with.</param>
    /// <returns>True if the packets are equal; otherwise, false.</returns>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public bool Equals(Packet other) =>
        Type == other.Type &&
        Flags == other.Flags &&
        Id == other.Id &&
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
    public override bool Equals(object? obj) =>
        obj is Packet packet && Equals(packet);

    /// <summary>
    /// Determines whether two packets are equal.
    /// </summary>
    /// <param name="left">The first packet.</param>
    /// <param name="right">The second packet.</param>
    /// <returns>True if the packets are equal; otherwise, false.</returns>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public static bool operator ==(Packet left, Packet right) => left.Equals(right);

    /// <summary>
    /// Determines whether two packets are not equal.
    /// </summary>
    /// <param name="left">The first packet.</param>
    /// <param name="right">The second packet.</param>
    /// <returns>True if the packets are not equal; otherwise, false.</returns>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public static bool operator !=(Packet left, Packet right) => !left.Equals(right);
}
