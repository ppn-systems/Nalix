using Nalix.Cryptography.Internal;

namespace Nalix.Cryptography.Hmac;

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
public sealed class Poly1305 : System.IDisposable
{
    #region Constants

    /// <summary>
    /// The size of the authentication tag produced by Poly1305 (16 bytes).
    /// </summary>
    public const System.Byte KeySize = 32;

    /// <summary>
    /// The size of the authentication tag produced by Poly1305 (16 bytes).
    /// </summary>
    public const System.Byte TagSize = 16;

    #endregion Constants

    #region Fields

    /// <summary>
    /// The prime TransportProtocol (2^130 - 5) used in Poly1305 algorithm.
    /// </summary>
    private static readonly System.UInt32[] s_prime = [0xFFFFFFFB, 0xFFFFFFFF, 0xFFFFFFFF, 0xFFFFFFFF, 0x3];

    /// <summary>
    /// Represents the r part of the key (clamped).
    /// </summary>
    private System.UInt32[] _r;

    /// <summary>
    /// Represents the s part of the key.
    /// </summary>
    private System.UInt32[] _s;

    /// <summary>
    /// Flag indicating if this instance has been disposed.
    /// </summary>
    private System.Boolean _disposed;

    #endregion Fields

    #region Constructors

    /// <summary>
    /// Initializes a new instance of the <see cref="Poly1305"/> class using a 32-byte key.
    /// </summary>
    /// <param name="key">A 32-byte key. The first 16 bytes are used for r (after clamping),
    /// and the last 16 bytes are used as s.</param>
    /// <exception cref="System.ArgumentNullException">Thrown when key is null.</exception>
    /// <exception cref="System.ArgumentException">Thrown when key length is not 32 bytes.</exception>
    public Poly1305(System.ReadOnlySpan<System.Byte> key)
    {
        if (key.Length != KeySize)
        {
            throw new System.ArgumentException($"Key must be {KeySize} bytes.", nameof(key));
        }

        _r = new System.UInt32[5];
        _s = new System.UInt32[4];

        // Extract and clamp r (first 16 bytes) according to RFC 8439
        System.ReadOnlySpan<System.Byte> rBytes = key[..16];
        ClampR(rBytes, _r);

        // Extract s (last 16 bytes) - stored as 4 uint words
        System.ReadOnlySpan<System.Byte> sBytes = key.Slice(16, 16);
        for (System.Byte i = 0; i < 4; i++)
        {
            _s[i] = System.Buffers.Binary.BinaryPrimitives.ReadUInt32LittleEndian(sBytes.Slice(i * 4, 4));
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
    /// <exception cref="System.ArgumentNullException">Thrown when key or message is null.</exception>
    /// <exception cref="System.ArgumentException">Thrown when key length is not 32 bytes or destination size is less than 16 bytes.</exception>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public static void Compute(
        System.ReadOnlySpan<System.Byte> key,
        System.ReadOnlySpan<System.Byte> message,
        System.Span<System.Byte> destination)
    {
        if (key.Length != KeySize)
        {
            throw new System.ArgumentException($"Key must be {KeySize} bytes.", nameof(key));
        }

        if (destination.Length < TagSize)
        {
            throw new System.ArgumentException(
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
    /// <exception cref="System.ArgumentNullException">Thrown when key or message is null.</exception>
    /// <exception cref="System.ArgumentException">Thrown when key length is not 32 bytes.</exception>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public static System.Byte[] Compute(
        System.ReadOnlySpan<System.Byte> key,
        System.ReadOnlySpan<System.Byte> message)
    {
        System.Byte[] tag = new System.Byte[TagSize];
        Compute(key, message, tag);
        return tag;
    }

    /// <summary>
    /// Computes the Poly1305 MAC for a message and returns it as a new byte array.
    /// </summary>
    /// <param name="key">A 32-byte key.</param>
    /// <param name="message">The message to authenticate.</param>
    /// <returns>A 16-byte authentication tag.</returns>
    /// <exception cref="System.ArgumentNullException">Thrown when key or message is null.</exception>
    /// <exception cref="System.ArgumentException">Thrown when key length is not 32 bytes.</exception>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public static System.Byte[] Compute(System.Byte[] key, System.Byte[] message)
    {
        System.ArgumentNullException.ThrowIfNull(key);
        System.ArgumentNullException.ThrowIfNull(message);

        return Compute(System.MemoryExtensions.AsSpan(key), System.MemoryExtensions.AsSpan(message));
    }

    /// <summary>
    /// Verifies a Poly1305 MAC against a message using the specified key.
    /// </summary>
    /// <param name="key">A 32-byte key.</param>
    /// <param name="message">The message to verify.</param>
    /// <param name="tag">The authentication tag to verify against.</param>
    /// <returns>True if the tag is valid for the message, false otherwise.</returns>
    /// <exception cref="System.ArgumentNullException">Thrown when key, message, or tag is null.</exception>
    /// <exception cref="System.ArgumentException">Thrown when key length is not 32 bytes or tag length is not 16 bytes.</exception>
    public static System.Boolean Verify(
        System.ReadOnlySpan<System.Byte> key,
        System.ReadOnlySpan<System.Byte> message,
        System.ReadOnlySpan<System.Byte> tag)
    {
        if (tag.Length != TagSize)
        {
            throw new System.ArgumentException($"Tag must be {TagSize} bytes.", nameof(tag));
        }

        System.Span<System.Byte> computedTag = stackalloc System.Byte[TagSize];
        Compute(key, message, computedTag);

        return BitwiseUtils.FixedTimeEquals(tag, computedTag);
    }

    /// <summary>
    /// Computes the Poly1305 MAC for a message.
    /// </summary>
    /// <param name="message">The message to authenticate.</param>
    /// <param name="destination">The span where the MAC will be written.</param>
    /// <exception cref="System.ObjectDisposedException">Thrown when the object has been disposed.</exception>
    public void ComputeTag(
        System.ReadOnlySpan<System.Byte> message,
        System.Span<System.Byte> destination)
    {
        System.ObjectDisposedException.ThrowIf(
            _disposed, $"This {nameof(Poly1305)} instance has been disposed.");

        if (destination.Length < TagSize)
        {
            throw new System.ArgumentException(
                $"Destination buffer must be at least {TagSize} bytes.", nameof(destination));
        }

        // Initialize accumulator
        System.Span<System.UInt32> accumulator = stackalloc System.UInt32[5];

        // Process message in blocks
        System.Int32 offset = 0;
        System.Int32 messageLength = message.Length;
        System.Span<System.Byte> block = stackalloc System.Byte[17]; // 16 bytes + 1 byte for the padding

        while (offset < messageLength)
        {
            // Clear the block to ensure we don't leave any sensitive data
            block.Clear();

            // Determine block size (final block may be shorter than 16 bytes)
            System.Int32 blockSize = System.Math.Min(16, messageLength - offset);

            // Copy message block
            message.Slice(offset, blockSize).CopyTo(block);

            // Append padding byte (0x01) after the message block
            block[blockSize] = 0x01;

            // Push this block to the accumulator
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
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    private static void ClampR(
        System.ReadOnlySpan<System.Byte> rBytes,
        System.Span<System.UInt32> r)
    {
        System.Diagnostics.Debug.Assert(rBytes.Length >= 16);
        System.Diagnostics.Debug.Assert(r.Length >= 5);

        // Convert to uint array (little-endian)
        r[0] = System.Buffers.Binary.BinaryPrimitives.ReadUInt32LittleEndian(rBytes[..4]) & 0x0FFF_FFFC;
        r[1] = System.Buffers.Binary.BinaryPrimitives.ReadUInt32LittleEndian(rBytes.Slice(4, 4)) & 0x0FFF_FFF0;
        r[2] = System.Buffers.Binary.BinaryPrimitives.ReadUInt32LittleEndian(rBytes.Slice(8, 4)) & 0x0FFF_FFF0;
        r[3] = System.Buffers.Binary.BinaryPrimitives.ReadUInt32LittleEndian(rBytes.Slice(12, 4)) & 0x0FFF_FFF0;
        r[4] = 0;
    }

    /// <summary>
    /// Adds a message block to the accumulator.
    /// </summary>
    /// <param name="accumulator">The current accumulator value.</param>
    /// <param name="block">The block data to add (already padded).</param>
    /// <param name="isFinalBlock">Whether this is the final block.</param>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    private void AddBlock(
        System.Span<System.UInt32> accumulator,
        System.ReadOnlySpan<System.Byte> block,
        System.Boolean isFinalBlock)
    {
        // Convert block to uint array with proper little-endian handling
        System.Span<System.UInt32> n = stackalloc System.UInt32[5];
        for (System.Byte i = 0; i < 4; i++)
        {
            System.Int32 offset = i * 4;
            n[i] = isFinalBlock && block.Length < offset + 4
                ? GetUInt32OrZero(block, offset)
                : System.Buffers.Binary.BinaryPrimitives.ReadUInt32LittleEndian(block.Slice(offset, 4));
        }
        n[4] = (System.UInt32)(isFinalBlock && block.Length <= 16 ? 0 : block[16]);

        // Push the message block to the accumulator
        Add(accumulator, n);

        // Multiply by r
        Multiply(accumulator, _r);

        // Reduce modulo 2^130 - 5
        Modulo(accumulator);
    }

    /// <summary>
    /// Safely reads a UInt32 from a span that might be too short, returning 0 for out of bounds access.
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    private static System.UInt32 GetUInt32OrZero(System.ReadOnlySpan<System.Byte> data, System.Int32 offset)
    {
        System.UInt32 result = 0;
        System.Int32 bytesAvailable = System.Math.Min(4, System.Math.Max(0, data.Length - offset));

        for (System.Int32 i = 0; i < bytesAvailable; i++)
        {
            result |= (System.UInt32)data[offset + i] << (8 * i);
        }

        return result;
    }

    /// <summary>
    /// Adds two 130-bit integers represented as uint arrays.
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    private static void Add(
        System.Span<System.UInt32> a,
        System.ReadOnlySpan<System.UInt32> b)
    {
        System.UInt64 carry = 0;
        for (System.Byte i = 0; i < 5; i++)
        {
            carry += (System.UInt64)a[i] + b[i];
            a[i] = (System.UInt32)carry;
            carry >>= 32;
        }
    }

    /// <summary>
    /// Multiplies a 130-bit integer by another 130-bit integer.
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    private static void Multiply(
        System.Span<System.UInt32> a,
        System.ReadOnlySpan<System.UInt32> b)
    {
        System.Span<System.UInt32> product = stackalloc System.UInt32[10];

        // Multiply each component
        for (System.Byte i = 0; i < 5; i++)
        {
            System.UInt64 carry = 0;
            for (System.Byte j = 0; j < 5; j++)
            {
                System.UInt64 t = ((System.UInt64)a[i] * b[j]) + product[i + j] + carry;
                product[i + j] = (System.UInt32)t;
                carry = t >> 32;
            }

            if (i + 5 < 10)
            {
                product[i + 5] = (System.UInt32)carry;
            }
        }

        // Reduce modulo 2^130 - 5
        ReduceProduct(a, product);
    }

    /// <summary>
    /// Reduces a 260-bit product modulo 2^130 - 5 to a 130-bit result.
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    private static void ReduceProduct(
        System.Span<System.UInt32> result,
        System.ReadOnlySpan<System.UInt32> product)
    {
        // Copy the low 130 bits
        for (System.Byte i = 0; i < 5; i++)
        {
            result[i] = product[i];
        }

        // Multiply the high 130 bits by 5 (because 2^130 ≡ 5 (mod 2^130 - 5))
        // and add to the result
        System.UInt32 carry = 0;
        for (System.Byte i = 0; i < 5; i++)
        {
            System.UInt64 t = ((System.UInt64)product[i + 5] * 5) + result[i] + carry;
            result[i] = (System.UInt32)t;
            carry = (System.UInt32)(t >> 32);
        }

        // Final reduction if needed (result might be >= 2^130 - 5)
        Modulo(result);
    }

    /// <summary>
    /// Reduces a value modulo 2^130 - 5.
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    private static void Modulo(System.Span<System.UInt32> value)
    {
        // Check if the value needs reduction
        if (IsGreaterOrEqual(value, s_prime))
        {
            // Subtract the prime
            Subtract(value, s_prime);
        }
    }

    /// <summary>
    /// Determines if one TransportProtocol is greater than or equal to another.
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    private static System.Boolean IsGreaterOrEqual(
        System.ReadOnlySpan<System.UInt32> a,
        System.ReadOnlySpan<System.UInt32> b)
    {
        // Compare from most significant word down
        for (System.Byte i = 4; i >= 0; i--)
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
    /// Subtracts one TransportProtocol from another.
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    private static void Subtract(
        System.Span<System.UInt32> a,
        System.ReadOnlySpan<System.UInt32> b)
    {
        System.UInt32 borrow = 0;
        for (System.Byte i = 0; i < 5; i++)
        {
            System.UInt64 diff = (System.UInt64)a[i] - b[i] - borrow;
            a[i] = (System.UInt32)diff;
            borrow = (System.UInt32)((diff >> 32) & 1);
        }
    }

    /// <summary>
    /// Finalizes the authentication tag by adding s and ensuring it's exactly 16 bytes.
    /// </summary>
    /// <param name="accumulator">The current accumulator value.</param>
    /// <param name="tag">The span where the tag will be written.</param>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    private void FinalizeTag(
        System.ReadOnlySpan<System.UInt32> accumulator,
        System.Span<System.Byte> tag)
    {
        System.Diagnostics.Debug.Assert(tag.Length >= TagSize);

        // Create a copy of the accumulator for the final operations
        System.Span<System.UInt32> result = stackalloc System.UInt32[5];
        accumulator.CopyTo(result);

        // Ensure the result is fully reduced modulo 2^130 - 5
        Modulo(result);

        // Push s
        System.Span<System.UInt32> finalResult = stackalloc System.UInt32[4];
        System.UInt64 carry = 0;
        for (System.Byte i = 0; i < 4; i++)
        {
            carry += (System.UInt64)result[i] + _s[i];
            finalResult[i] = (System.UInt32)carry;
            carry >>= 32;
        }

        // Convert to bytes (little-endian)
        for (System.Byte i = 0; i < 4; i++)
        {
            System.Buffers.Binary.BinaryPrimitives.WriteUInt32LittleEndian(
                tag.Slice(i * 4, 4), finalResult[i]);
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
                System.Array.Clear(_r, 0, _r.Length);
                _r = null;
            }

            if (_s != null)
            {
                System.Array.Clear(_s, 0, _s.Length);
                _s = null;
            }

            _disposed = true;
        }

        System.GC.SuppressFinalize(this);
    }

    #endregion IDisposable
}
