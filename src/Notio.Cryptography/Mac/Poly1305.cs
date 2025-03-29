using Notio.Cryptography.Utilities;
using System;
using System.Buffers.Binary;
using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace Notio.Cryptography.Mac;

/// <summary>
/// High-performance implementation of the Poly1305 message authentication code (MAC) algorithm.
/// </summary>
/// <remarks>
/// <para>
/// Poly1305 is a cryptographically strong MAC algorithm designed by Daniel J. Bernstein.
/// It's used in various cryptographic protocols including ChaCha20-Poly1305 cipher suite in TLS.
/// </para>
/// <para>
/// This implementation follows RFC 8439 and provides both heap allocation optimized and 
/// constant-time operations for enhanced security.
/// </para>
/// </remarks>
public sealed class Poly1305 : IDisposable
{
    /// <summary>
    /// The size of the authentication tag produced by Poly1305 (16 bytes).
    /// </summary>
    public const int KeySize = 32;

    /// <summary>
    /// The size of the authentication tag produced by Poly1305 (16 bytes).
    /// </summary>
    public const int TagSize = 16;

    /// <summary>
    /// The prime number (2^130 - 5) used in Poly1305 algorithm.
    /// </summary>
    private static readonly uint[] s_prime = [0xFFFFFFFB, 0xFFFFFFFF, 0xFFFFFFFF, 0xFFFFFFFF, 0x3];

    /// <summary>
    /// Represents the r part of the key (clamped).
    /// </summary>
    private uint[] _r;

    /// <summary>
    /// Represents the s part of the key.
    /// </summary>
    private uint[] _s;

    /// <summary>
    /// Flag indicating if this instance has been disposed.
    /// </summary>
    private bool _disposed;

    /// <summary>
    /// Initializes a new instance of the <see cref="Poly1305"/> class using a 32-byte key.
    /// </summary>
    /// <param name="key">A 32-byte key. The first 16 bytes are used for r (after clamping),
    /// and the last 16 bytes are used as s.</param>
    /// <exception cref="ArgumentNullException">Thrown when key is null.</exception>
    /// <exception cref="ArgumentException">Thrown when key length is not 32 bytes.</exception>
    public Poly1305(ReadOnlySpan<byte> key)
    {
        if (key.Length != KeySize)
            throw new ArgumentException($"Key must be {KeySize} bytes.", nameof(key));

        _r = new uint[5];
        _s = new uint[4];

        // Extract and clamp r (first 16 bytes) according to RFC 8439
        ReadOnlySpan<byte> rBytes = key[..16];
        ClampR(rBytes, _r);

        // Extract s (last 16 bytes) - stored as 4 uint words
        ReadOnlySpan<byte> sBytes = key.Slice(16, 16);
        for (int i = 0; i < 4; i++)
        {
            _s[i] = BinaryPrimitives.ReadUInt32LittleEndian(sBytes.Slice(i * 4, 4));
        }
    }

    /// <summary>
    /// Computes the Poly1305 MAC for a message using the specified key.
    /// </summary>
    /// <param name="key">A 32-byte key.</param>
    /// <param name="message">The message to authenticate.</param>
    /// <param name="destination">The span to which the MAC will be written (must be at least 16 bytes).</param>
    /// <exception cref="ArgumentNullException">Thrown when key or message is null.</exception>
    /// <exception cref="ArgumentException">Thrown when key length is not 32 bytes or destination size is less than 16 bytes.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Compute(ReadOnlySpan<byte> key, ReadOnlySpan<byte> message, Span<byte> destination)
    {
        if (key.Length != KeySize)
            throw new ArgumentException($"Key must be {KeySize} bytes.", nameof(key));

        if (destination.Length < TagSize)
            throw new ArgumentException(
                $"Destination buffer must be at least {TagSize} bytes.", nameof(destination));

        using var poly = new Poly1305(key);
        poly.ComputeTag(message, destination);
    }

    /// <summary>
    /// Computes the Poly1305 MAC for a message and returns it as a new byte array.
    /// </summary>
    /// <param name="key">A 32-byte key.</param>
    /// <param name="message">The message to authenticate.</param>
    /// <returns>A 16-byte authentication tag.</returns>
    /// <exception cref="ArgumentNullException">Thrown when key or message is null.</exception>
    /// <exception cref="ArgumentException">Thrown when key length is not 32 bytes.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static byte[] Compute(ReadOnlySpan<byte> key, ReadOnlySpan<byte> message)
    {
        byte[] tag = new byte[TagSize];
        Compute(key, message, tag);
        return tag;
    }

    /// <summary>
    /// Computes the Poly1305 MAC for a message and returns it as a new byte array.
    /// </summary>
    /// <param name="key">A 32-byte key.</param>
    /// <param name="message">The message to authenticate.</param>
    /// <returns>A 16-byte authentication tag.</returns>
    /// <exception cref="ArgumentNullException">Thrown when key or message is null.</exception>
    /// <exception cref="ArgumentException">Thrown when key length is not 32 bytes.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static byte[] Compute(byte[] key, byte[] message)
    {
        ArgumentNullException.ThrowIfNull(key);
        ArgumentNullException.ThrowIfNull(message);

        return Compute(key.AsSpan(), message.AsSpan());
    }

    /// <summary>
    /// Verifies a Poly1305 MAC against a message using the specified key.
    /// </summary>
    /// <param name="key">A 32-byte key.</param>
    /// <param name="message">The message to verify.</param>
    /// <param name="tag">The authentication tag to verify against.</param>
    /// <returns>True if the tag is valid for the message, false otherwise.</returns>
    /// <exception cref="ArgumentNullException">Thrown when key, message, or tag is null.</exception>
    /// <exception cref="ArgumentException">Thrown when key length is not 32 bytes or tag length is not 16 bytes.</exception>
    public static bool Verify(ReadOnlySpan<byte> key, ReadOnlySpan<byte> message, ReadOnlySpan<byte> tag)
    {
        if (tag.Length != TagSize)
            throw new ArgumentException($"Tag must be {TagSize} bytes.", nameof(tag));

        Span<byte> computedTag = stackalloc byte[TagSize];
        Compute(key, message, computedTag);

        return BitwiseUtils.FixedTimeEquals(tag, computedTag);
    }

    /// <summary>
    /// Computes the Poly1305 MAC for a message.
    /// </summary>
    /// <param name="message">The message to authenticate.</param>
    /// <param name="destination">The span where the MAC will be written.</param>
    /// <exception cref="ObjectDisposedException">Thrown when the object has been disposed.</exception>
    public void ComputeTag(ReadOnlySpan<byte> message, Span<byte> destination)
    {
        ObjectDisposedException.ThrowIf(_disposed, $"This {nameof(Poly1305)} instance has been disposed.");

        if (destination.Length < TagSize)
            throw new ArgumentException(
                $"Destination buffer must be at least {TagSize} bytes.", nameof(destination));

        // Initialize accumulator
        Span<uint> accumulator = stackalloc uint[5];

        // Process message in blocks
        int offset = 0;
        int messageLength = message.Length;
        Span<byte> block = stackalloc byte[17]; // 16 bytes + 1 byte for the padding

        while (offset < messageLength)
        {
            // Clear the block to ensure we don't leave any sensitive data
            block.Clear();

            // Determine block size (final block may be shorter than 16 bytes)
            int blockSize = Math.Min(16, messageLength - offset);

            // Copy message block
            message.Slice(offset, blockSize).CopyTo(block);

            // Append padding byte (0x01) after the message block
            block[blockSize] = 0x01;

            // Add this block to the accumulator
            AddBlock(accumulator, block[..(blockSize + 1)], blockSize < 16);

            offset += blockSize;
        }

        // Finalize the tag
        FinalizeTag(accumulator, destination);
    }

    /// <summary>
    /// Clamps the r value according to RFC 8439.
    /// </summary>
    /// <param name="rBytes">The r portion of the key (16 bytes).</param>
    /// <param name="r">Array to store the clamped r value.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void ClampR(ReadOnlySpan<byte> rBytes, Span<uint> r)
    {
        Debug.Assert(rBytes.Length >= 16);
        Debug.Assert(r.Length >= 5);

        // Convert to uint array (little-endian)
        r[0] = BinaryPrimitives.ReadUInt32LittleEndian(rBytes[..4]) & 0x0FFF_FFFC;
        r[1] = BinaryPrimitives.ReadUInt32LittleEndian(rBytes.Slice(4, 4)) & 0x0FFF_FFF0;
        r[2] = BinaryPrimitives.ReadUInt32LittleEndian(rBytes.Slice(8, 4)) & 0x0FFF_FFF0;
        r[3] = BinaryPrimitives.ReadUInt32LittleEndian(rBytes.Slice(12, 4)) & 0x0FFF_FFF0;
        r[4] = 0;
    }

    /// <summary>
    /// Adds a message block to the accumulator.
    /// </summary>
    /// <param name="accumulator">The current accumulator value.</param>
    /// <param name="block">The block data to add (already padded).</param>
    /// <param name="isFinalBlock">Whether this is the final block.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void AddBlock(Span<uint> accumulator, ReadOnlySpan<byte> block, bool isFinalBlock)
    {
        // Convert block to uint array with proper little-endian handling
        Span<uint> n =
        [
            isFinalBlock && block.Length < 5 ? GetUInt32OrZero(block, 0) :
            BinaryPrimitives.ReadUInt32LittleEndian(block[..4]),
            isFinalBlock && block.Length < 9 ? GetUInt32OrZero(block, 4) :
            BinaryPrimitives.ReadUInt32LittleEndian(block.Slice(4, 4)),
            isFinalBlock && block.Length < 13 ? GetUInt32OrZero(block, 8) :
            BinaryPrimitives.ReadUInt32LittleEndian(block.Slice(8, 4)),
            isFinalBlock && block.Length < 17 ? GetUInt32OrZero(block, 12) :
            BinaryPrimitives.ReadUInt32LittleEndian(block.Slice(12, 4)),
            (uint)(isFinalBlock && block.Length <= 16 ? 0 : block[16]),
        ];

        // Add the message block to the accumulator
        Add(accumulator, n);

        // Multiply by r
        Multiply(accumulator, _r);

        // Reduce modulo 2^130 - 5
        Modulo(accumulator);
    }

    /// <summary>
    /// Safely reads a UInt32 from a span that might be too short, returning 0 for out of bounds access.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static uint GetUInt32OrZero(ReadOnlySpan<byte> data, int offset)
    {
        uint result = 0;
        int bytesAvailable = Math.Min(4, Math.Max(0, data.Length - offset));

        for (int i = 0; i < bytesAvailable; i++)
        {
            result |= (uint)data[offset + i] << (8 * i);
        }

        return result;
    }

    /// <summary>
    /// Adds two 130-bit integers represented as uint arrays.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void Add(Span<uint> a, ReadOnlySpan<uint> b)
    {
        ulong carry = 0;
        for (int i = 0; i < 5; i++)
        {
            carry += (ulong)a[i] + b[i];
            a[i] = (uint)carry;
            carry >>= 32;
        }
    }

    /// <summary>
    /// Multiplies a 130-bit integer by another 130-bit integer.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void Multiply(Span<uint> a, ReadOnlySpan<uint> b)
    {
        Span<uint> product = stackalloc uint[10];

        // Multiply each component
        for (int i = 0; i < 5; i++)
        {
            ulong carry = 0;
            for (int j = 0; j < 5; j++)
            {
                ulong t = (ulong)a[i] * b[j] + product[i + j] + carry;
                product[i + j] = (uint)t;
                carry = t >> 32;
            }

            if (i + 5 < 10)
                product[i + 5] = (uint)carry;
        }

        // Reduce modulo 2^130 - 5
        ReduceProduct(a, product);
    }

    /// <summary>
    /// Reduces a 260-bit product modulo 2^130 - 5 to a 130-bit result.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void ReduceProduct(Span<uint> result, ReadOnlySpan<uint> product)
    {
        // Copy the low 130 bits
        for (int i = 0; i < 5; i++)
        {
            result[i] = product[i];
        }

        // Multiply the high 130 bits by 5 (because 2^130 ≡ 5 (mod 2^130 - 5))
        // and add to the result
        uint carry = 0;
        for (int i = 0; i < 5; i++)
        {
            ulong t = (ulong)product[i + 5] * 5 + result[i] + carry;
            result[i] = (uint)t;
            carry = (uint)(t >> 32);
        }

        // Final reduction if needed (result might be >= 2^130 - 5)
        Modulo(result);
    }

    /// <summary>
    /// Reduces a value modulo 2^130 - 5.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void Modulo(Span<uint> value)
    {
        // Check if the value needs reduction
        if (IsGreaterOrEqual(value, s_prime))
        {
            // Subtract the prime
            Subtract(value, s_prime);
        }
    }

    /// <summary>
    /// Determines if one number is greater than or equal to another.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool IsGreaterOrEqual(ReadOnlySpan<uint> a, ReadOnlySpan<uint> b)
    {
        // Compare from most significant word down
        for (int i = 4; i >= 0; i--)
        {
            if (a[i] > b[i])
                return true;
            if (a[i] < b[i])
                return false;
        }

        // All words are equal
        return true;
    }

    /// <summary>
    /// Subtracts one number from another.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void Subtract(Span<uint> a, ReadOnlySpan<uint> b)
    {
        uint borrow = 0;
        for (int i = 0; i < 5; i++)
        {
            ulong diff = (ulong)a[i] - b[i] - borrow;
            a[i] = (uint)diff;
            borrow = (uint)((diff >> 32) & 1);
        }
    }

    /// <summary>
    /// Finalizes the authentication tag by adding s and ensuring it's exactly 16 bytes.
    /// </summary>
    /// <param name="accumulator">The current accumulator value.</param>
    /// <param name="tag">The span where the tag will be written.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void FinalizeTag(ReadOnlySpan<uint> accumulator, Span<byte> tag)
    {
        Debug.Assert(tag.Length >= TagSize);

        // Create a copy of the accumulator for the final operations
        Span<uint> result = stackalloc uint[5];
        accumulator.CopyTo(result);

        // Ensure the result is fully reduced modulo 2^130 - 5
        Modulo(result);

        // Add s
        Span<uint> finalResult = stackalloc uint[4];
        ulong carry = 0;
        for (int i = 0; i < 4; i++)
        {
            carry += (ulong)result[i] + _s[i];
            finalResult[i] = (uint)carry;
            carry >>= 32;
        }

        // Convert to bytes (little-endian)
        for (int i = 0; i < 4; i++)
        {
            BinaryPrimitives.WriteUInt32LittleEndian(tag.Slice(i * 4, 4), finalResult[i]);
        }
    }

    /// <summary>
    /// Securely clears sensitive data when the object is disposed.
    /// </summary>
    public void Dispose()
    {
        if (!_disposed)
        {
            // Clear sensitive key material
            if (_r != null)
            {
                Array.Clear(_r, 0, _r.Length);
                _r = null;
            }

            if (_s != null)
            {
                Array.Clear(_s, 0, _s.Length);
                _s = null;
            }

            _disposed = true;
        }

        GC.SuppressFinalize(this);
    }
}
