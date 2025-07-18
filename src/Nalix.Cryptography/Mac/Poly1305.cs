using Nalix.Cryptography.Internal;
using System;
using System.Buffers.Binary;
using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace Nalix.Cryptography.Mac;

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
    #region Constants

    /// <summary>
    /// The size of the authentication tag produced by Poly1305 (16 bytes).
    /// </summary>
    public const Int32 KeySize = 32;

    /// <summary>
    /// The size of the authentication tag produced by Poly1305 (16 bytes).
    /// </summary>
    public const Int32 TagSize = 16;

    #endregion Constants

    #region Fields

    /// <summary>
    /// The prime Number (2^130 - 5) used in Poly1305 algorithm.
    /// </summary>
    private static readonly UInt32[] s_prime = [0xFFFFFFFB, 0xFFFFFFFF, 0xFFFFFFFF, 0xFFFFFFFF, 0x3];

    /// <summary>
    /// Represents the r part of the key (clamped).
    /// </summary>
    private UInt32[] _r;

    /// <summary>
    /// Represents the s part of the key.
    /// </summary>
    private UInt32[] _s;

    /// <summary>
    /// Flag indicating if this instance has been disposed.
    /// </summary>
    private Boolean _disposed;

    #endregion Fields

    #region Constructors

    /// <summary>
    /// Initializes a new instance of the <see cref="Poly1305"/> class using a 32-byte key.
    /// </summary>
    /// <param name="key">A 32-byte key. The first 16 bytes are used for r (after clamping),
    /// and the last 16 bytes are used as s.</param>
    /// <exception cref="ArgumentNullException">Thrown when key is null.</exception>
    /// <exception cref="ArgumentException">Thrown when key length is not 32 bytes.</exception>
    public Poly1305(ReadOnlySpan<Byte> key)
    {
        if (key.Length != KeySize)
        {
            throw new ArgumentException($"Key must be {KeySize} bytes.", nameof(key));
        }

        _r = new UInt32[5];
        _s = new UInt32[4];

        // Extract and clamp r (first 16 bytes) according to RFC 8439
        ReadOnlySpan<Byte> rBytes = key[..16];
        ClampR(rBytes, _r);

        // Extract s (last 16 bytes) - stored as 4 uint words
        ReadOnlySpan<Byte> sBytes = key.Slice(16, 16);
        for (Int32 i = 0; i < 4; i++)
        {
            _s[i] = BinaryPrimitives.ReadUInt32LittleEndian(sBytes.Slice(i * 4, 4));
        }
    }

    #endregion Constructors

    #region Public Methods

    /// <summary>
    /// Computes the Poly1305 MAC for a message using the specified key.
    /// </summary>
    /// <param name="key">A 32-byte key.</param>
    /// <param name="message">The message to authenticate.</param>
    /// <param name="destination">The span to which the MAC will be written (must be at least 16 bytes).</param>
    /// <exception cref="ArgumentNullException">Thrown when key or message is null.</exception>
    /// <exception cref="ArgumentException">Thrown when key length is not 32 bytes or destination size is less than 16 bytes.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Compute(ReadOnlySpan<Byte> key, ReadOnlySpan<Byte> message, Span<Byte> destination)
    {
        if (key.Length != KeySize)
        {
            throw new ArgumentException($"Key must be {KeySize} bytes.", nameof(key));
        }

        if (destination.Length < TagSize)
        {
            throw new ArgumentException(
                $"Destination buffer must be at least {TagSize} bytes.", nameof(destination));
        }

        using Poly1305 poly = new(key);
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
    public static Byte[] Compute(ReadOnlySpan<Byte> key, ReadOnlySpan<Byte> message)
    {
        Byte[] tag = new Byte[TagSize];
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
    public static Byte[] Compute(Byte[] key, Byte[] message)
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
    public static Boolean Verify(ReadOnlySpan<Byte> key, ReadOnlySpan<Byte> message, ReadOnlySpan<Byte> tag)
    {
        if (tag.Length != TagSize)
        {
            throw new ArgumentException($"Tag must be {TagSize} bytes.", nameof(tag));
        }

        Span<Byte> computedTag = stackalloc Byte[TagSize];
        Compute(key, message, computedTag);

        return BitwiseUtils.FixedTimeEquals(tag, computedTag);
    }

    /// <summary>
    /// Computes the Poly1305 MAC for a message.
    /// </summary>
    /// <param name="message">The message to authenticate.</param>
    /// <param name="destination">The span where the MAC will be written.</param>
    /// <exception cref="ObjectDisposedException">Thrown when the object has been disposed.</exception>
    public void ComputeTag(ReadOnlySpan<Byte> message, Span<Byte> destination)
    {
        ObjectDisposedException.ThrowIf(_disposed, $"This {nameof(Poly1305)} instance has been disposed.");

        if (destination.Length < TagSize)
        {
            throw new ArgumentException(
                $"Destination buffer must be at least {TagSize} bytes.", nameof(destination));
        }

        // Initialize accumulator
        Span<UInt32> accumulator = stackalloc UInt32[5];

        // Process message in blocks
        Int32 offset = 0;
        Int32 messageLength = message.Length;
        Span<Byte> block = stackalloc Byte[17]; // 16 bytes + 1 byte for the padding

        while (offset < messageLength)
        {
            // Clear the block to ensure we don't leave any sensitive data
            block.Clear();

            // Determine block size (final block may be shorter than 16 bytes)
            Int32 blockSize = Math.Min(16, messageLength - offset);

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

    #endregion Public Methods

    #region Private Methods

    /// <summary>
    /// Clamps the r value according to RFC 8439.
    /// </summary>
    /// <param name="rBytes">The r portion of the key (16 bytes).</param>
    /// <param name="r">Array to store the clamped r value.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void ClampR(ReadOnlySpan<Byte> rBytes, Span<UInt32> r)
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
    private void AddBlock(Span<UInt32> accumulator, ReadOnlySpan<Byte> block, Boolean isFinalBlock)
    {
        // Convert block to uint array with proper little-endian handling
        Span<UInt32> n = stackalloc UInt32[5];
        for (Int32 i = 0; i < 4; i++)
        {
            Int32 offset = i * 4;
            n[i] = (isFinalBlock && block.Length < offset + 4)
                ? GetUInt32OrZero(block, offset)
                : BinaryPrimitives.ReadUInt32LittleEndian(block.Slice(offset, 4));
        }
        n[4] = (UInt32)(isFinalBlock && block.Length <= 16 ? 0 : block[16]);

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
    private static UInt32 GetUInt32OrZero(ReadOnlySpan<Byte> data, Int32 offset)
    {
        UInt32 result = 0;
        Int32 bytesAvailable = Math.Min(4, Math.Max(0, data.Length - offset));

        for (Int32 i = 0; i < bytesAvailable; i++)
        {
            result |= (UInt32)data[offset + i] << (8 * i);
        }

        return result;
    }

    /// <summary>
    /// Adds two 130-bit integers represented as uint arrays.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void Add(Span<UInt32> a, ReadOnlySpan<UInt32> b)
    {
        UInt64 carry = 0;
        for (Int32 i = 0; i < 5; i++)
        {
            carry += (UInt64)a[i] + b[i];
            a[i] = (UInt32)carry;
            carry >>= 32;
        }
    }

    /// <summary>
    /// Multiplies a 130-bit integer by another 130-bit integer.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void Multiply(Span<UInt32> a, ReadOnlySpan<UInt32> b)
    {
        Span<UInt32> product = stackalloc UInt32[10];

        // Multiply each component
        for (Int32 i = 0; i < 5; i++)
        {
            UInt64 carry = 0;
            for (Int32 j = 0; j < 5; j++)
            {
                UInt64 t = ((UInt64)a[i] * b[j]) + product[i + j] + carry;
                product[i + j] = (UInt32)t;
                carry = t >> 32;
            }

            if (i + 5 < 10)
            {
                product[i + 5] = (UInt32)carry;
            }
        }

        // Reduce modulo 2^130 - 5
        ReduceProduct(a, product);
    }

    /// <summary>
    /// Reduces a 260-bit product modulo 2^130 - 5 to a 130-bit result.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void ReduceProduct(Span<UInt32> result, ReadOnlySpan<UInt32> product)
    {
        // Copy the low 130 bits
        for (Int32 i = 0; i < 5; i++)
        {
            result[i] = product[i];
        }

        // Multiply the high 130 bits by 5 (because 2^130 ≡ 5 (mod 2^130 - 5))
        // and add to the result
        UInt32 carry = 0;
        for (Int32 i = 0; i < 5; i++)
        {
            UInt64 t = ((UInt64)product[i + 5] * 5) + result[i] + carry;
            result[i] = (UInt32)t;
            carry = (UInt32)(t >> 32);
        }

        // Final reduction if needed (result might be >= 2^130 - 5)
        Modulo(result);
    }

    /// <summary>
    /// Reduces a value modulo 2^130 - 5.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void Modulo(Span<UInt32> value)
    {
        // Check if the value needs reduction
        if (IsGreaterOrEqual(value, s_prime))
        {
            // Subtract the prime
            Subtract(value, s_prime);
        }
    }

    /// <summary>
    /// Determines if one Number is greater than or equal to another.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Boolean IsGreaterOrEqual(ReadOnlySpan<UInt32> a, ReadOnlySpan<UInt32> b)
    {
        // Compare from most significant word down
        for (Int32 i = 4; i >= 0; i--)
        {
            if (a[i] > b[i])
            {
                return true;
            }

            if (a[i] < b[i])
            {
                return false;
            }
        }

        // All words are equal
        return true;
    }

    /// <summary>
    /// Subtracts one Number from another.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void Subtract(Span<UInt32> a, ReadOnlySpan<UInt32> b)
    {
        UInt32 borrow = 0;
        for (Int32 i = 0; i < 5; i++)
        {
            UInt64 diff = (UInt64)a[i] - b[i] - borrow;
            a[i] = (UInt32)diff;
            borrow = (UInt32)((diff >> 32) & 1);
        }
    }

    /// <summary>
    /// Finalizes the authentication tag by adding s and ensuring it's exactly 16 bytes.
    /// </summary>
    /// <param name="accumulator">The current accumulator value.</param>
    /// <param name="tag">The span where the tag will be written.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void FinalizeTag(ReadOnlySpan<UInt32> accumulator, Span<Byte> tag)
    {
        Debug.Assert(tag.Length >= TagSize);

        // Create a copy of the accumulator for the final operations
        Span<UInt32> result = stackalloc UInt32[5];
        accumulator.CopyTo(result);

        // Ensure the result is fully reduced modulo 2^130 - 5
        Modulo(result);

        // Add s
        Span<UInt32> finalResult = stackalloc UInt32[4];
        UInt64 carry = 0;
        for (Int32 i = 0; i < 4; i++)
        {
            carry += (UInt64)result[i] + _s[i];
            finalResult[i] = (UInt32)carry;
            carry >>= 32;
        }

        // Convert to bytes (little-endian)
        for (Int32 i = 0; i < 4; i++)
        {
            BinaryPrimitives.WriteUInt32LittleEndian(tag.Slice(i * 4, 4), finalResult[i]);
        }
    }

    #endregion Private Methods

    #region IDisposable

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

    #endregion IDisposable
}
