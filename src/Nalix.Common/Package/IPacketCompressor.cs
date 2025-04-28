namespace Nalix.Common.Package;

/// <summary>
/// Provides a contract for compressing and decompressing a packet of type <typeparamref name="TPacket"/>.
/// </summary>
/// <typeparam name="TPacket">
/// The packet type that implements <see cref="IPacket"/> and supports static compression and decompression.
/// </typeparam>
public interface IPacketCompressor<TPacket> where TPacket : IPacket
{
    /// <summary>
    /// Compresses a packet of type <typeparamref name="TPacket"/> into the given buffer.
    /// </summary>
    /// <param name="packet">
    /// The packet to be compressed.
    /// </param>
    /// <returns>
    /// A span of bytes containing the compressed packet data.
    /// </returns>
    static abstract TPacket Compress(TPacket packet);

    /// <summary>
    /// Decompresses a packet of type <typeparamref name="TPacket"/> from the given buffer.
    /// </summary>
    /// <param name="packet">
    /// The packet to be decompress.
    /// </param>
    /// <returns>
    /// An instance of <typeparamref name="TPacket"/> that was decompressed from the buffer.
    /// </returns>
    static abstract TPacket Decompress(TPacket packet);
}
