using Notio.Common.Exceptions;
using Notio.Network.Package.Enums;
using Notio.Network.Package.Helpers;
using Notio.Network.Package.Utilities;
using System;
using System.IO;
using System.IO.Compression;
using System.Runtime.CompilerServices;

namespace Notio.Network.Package.Extensions;

/// <summary>
/// Provides operations for compressing and decompressing packets.
/// </summary>
public static partial class PackageExtensions
{
    /// <summary>
    /// Compresses the payload of the @this.
    /// </summary>
    /// <param name="this">The @this whose payload is to be compressed.</param>
    /// <returns>A new @this with the compressed payload.</returns>
    /// <exception cref="PackageException">Thrown when an error occurs during compression.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Packet CompressPayload(this in Packet @this)
    {
        PacketVerifier.ValidateCompressionEligibility(@this);

        try
        {
            int estimatedCompressedSize = Math.Max(128, @this.Payload.Length / 2);
            using MemoryStream outputStream = new(estimatedCompressedSize);
            using (GZipStream gzipStream = new(outputStream, CompressionLevel.Optimal, leaveOpen: true))
            {
                gzipStream.Write(@this.Payload.Span);
                gzipStream.Flush();
            }

            return new Packet(
                @this.Type,
                @this.Flags.AddFlag(PacketFlags.IsCompressed),
                @this.Priority,
                @this.Command,
                outputStream.ToArray()
            );
        }
        catch (Exception ex) when (ex is IOException or ObjectDisposedException)
        {
            throw new PackageException("Error occurred during payload compression.", ex);
        }
    }

    /// <summary>
    /// Decompresses the payload of the @this.
    /// </summary>
    /// <param name="this">The @this whose payload is to be decompressed.</param>
    /// <returns>A new @this with the decompressed payload.</returns>
    /// <exception cref="PackageException">Thrown when an error occurs during decompression or if the payload is not marked as compressed.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Packet DecompressPayload(this in Packet @this)
    {
        PacketVerifier.ValidateCompressionEligibility(@this);

        if (!@this.Flags.HasFlag(PacketFlags.IsCompressed))
            throw new PackageException("Payload is not marked as compressed.");

        try
        {
            int estimatedDecompressedSize = Math.Max(128, @this.Payload.Length * 4);
            using MemoryStream inputStream = new(@this.Payload.ToArray());
            using MemoryStream outputStream = new(estimatedDecompressedSize);
            using (GZipStream gzipStream = new(inputStream, CompressionMode.Decompress))
            {
                gzipStream.CopyTo(outputStream, 8192);
            }

            return new Packet(
                @this.Type,
                @this.Flags.RemoveFlag(PacketFlags.IsCompressed),
                @this.Priority,
                @this.Command,
                outputStream.ToArray()
            );
        }
        catch (InvalidDataException ex)
        {
            throw new PackageException("Invalid compressed data.", ex);
        }
        catch (Exception ex) when (ex is IOException or ObjectDisposedException)
        {
            throw new PackageException("Error occurred during payload decompression.", ex);
        }
    }

    /// <summary>
    /// Tries to perform an operation on the @this.
    /// </summary>
    /// <param name="this">The @this on which the operation is to be performed.</param>
    /// <param name="operation">The operation to be performed.</param>
    /// <param name="result">The result of the operation.</param>
    /// <returns><c>true</c> if the operation succeeded; otherwise, <c>false</c>.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool TryCompress(this Packet @this, Func<Packet, Packet> operation, out Packet result)
    {
        try
        {
            result = operation(@this);
            return true;
        }
        catch (PackageException)
        {
            result = default;
            return false;
        }
    }

    /// <summary>
    /// Tries to compress the payload of the @this.
    /// </summary>
    /// <param name="this">The @this whose payload is to be compressed.</param>
    /// <param name="out">The compressed @this.</param>
    /// <returns><c>true</c> if the payload was compressed successfully; otherwise, <c>false</c>.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool TryCompressPayload(this Packet @this, out Packet @out)
        => @this.TryCompress(p => p.CompressPayload(), out @out);

    /// <summary>
    /// Tries to decompress the payload of the @this.
    /// </summary>
    /// <param name="this">The @this whose payload is to be decompressed.</param>
    /// <param name="out">The decompressed @this.</param>
    /// <returns><c>true</c> if the payload was decompressed successfully; otherwise, <c>false</c>.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool TryDecompressPayload(this Packet @this, out Packet @out)
        => @this.TryCompress(p => p.DecompressPayload(), out @out);
}