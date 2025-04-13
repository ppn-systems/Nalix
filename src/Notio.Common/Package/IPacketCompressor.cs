using Notio.Common.Security;

namespace Notio.Common.Package;

/// <summary>
/// Provides a contract for compressing and decompressing a packet of type <typeparamref name="T"/>.
/// </summary>
/// <typeparam name="T">
/// The packet type that implements <see cref="IPacket"/> and supports static compression and decompression.
/// </typeparam>
public interface IPacketCompressor<T> where T : IPacket
{
    /// <summary>
    /// Compresses a packet of type <typeparamref name="T"/> into the given buffer.
    /// </summary>
    /// <param name="packet">
    /// The packet to be compressed.
    /// </param>
    /// <param name="type">
    /// The compression type used for the payload.
    /// </param>
    /// <returns>
    /// A span of bytes containing the compressed packet data.
    /// </returns>
    static abstract T Compress(T packet, CompressionMode type);

    /// <summary>
    /// Decompresses a packet of type <typeparamref name="T"/> from the given buffer.
    /// </summary>
    /// <param name="packet">
    /// The packet to be decompress.
    /// </param>
    /// <param name="type">
    /// The compression type used for the payload.
    /// </param>
    /// <returns>
    /// An instance of <typeparamref name="T"/> that was decompressed from the buffer.
    /// </returns>
    static abstract T Decompress(T packet, CompressionMode type);
}
