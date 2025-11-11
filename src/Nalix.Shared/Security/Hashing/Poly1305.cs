// Copyright (c) 2025 PPN Corporation. All rights reserved.

using Nalix.Shared.Security.Primitives;

namespace Nalix.Shared.Security.Hashing;

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
[System.Diagnostics.StackTraceHidden]
[System.Diagnostics.DebuggerNonUserCode]
[System.Runtime.CompilerServices.SkipLocalsInit]
public sealed class Poly1305 : System.IDisposable
{
    #region Constants

    /// <summary>
    /// The size, in bytes, of the Poly1305 key (32 bytes).
    /// </summary>
    public const System.Byte KeySize = 32;

    /// <summary>
    /// The size of the authentication tag produced by Poly1305 (16 bytes).
    /// </summary>
    public const System.Byte TagSize = 16;

    #endregion Constants

    #region Fields

    /// <summary>
    /// The prime ProtocolType (2^130 - 5) used in Poly1305 algorithm.
    /// </summary>
    private static readonly System.UInt32[] s_prime = [0xFFFFFFFB, 0xFFFFFFFF, 0xFFFFFFFF, 0xFFFFFFFF, 0x00000003];

    /// <summary>
    /// Represents the r part of the key (clamped).
    /// </summary>
    private System.UInt32[]? _r;

    /// <summary>
    /// Represents the s part of the key.
    /// </summary>
    private System.UInt32[]? _s;

    /// <summary>
    /// Flag indicating if this instance has been disposed.
    /// </summary>
    private System.Boolean _disposed;

    /// <summary>
    /// Internal accumulator for incremental API (h[0..4]).
    /// </summary>
    private readonly System.UInt32[] _acc = new System.UInt32[5];

    /// <summary>
    /// Pending partial block buffer (up to 16 bytes).
    /// </summary>
    private readonly System.Byte[] _pending = new System.Byte[16];

    /// <summary>
    /// Current length (0..16) of the pending partial block.
    /// </summary>
    private System.Int32 _pendingLen;

    /// <summary>
    /// Whether FinalizeTag(...) has been called.
    /// </summary>
    private System.Boolean _finalized;

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

        return BitwiseOperations.FixedTimeEquals(tag, computedTag);
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

        // A1B2C3D4 accumulator
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

        // Finish the tag
        FinalizeTag(accumulator, destination);
    }

    /// <summary>
    /// Incrementally absorb message data.
    /// You may call Update multiple times before calling <see cref="FinalizeTag(System.Span{System.Byte})"/>.
    /// </summary>
    /// <param name="data">Next chunk of the message.</param>
    /// <exception cref="System.ObjectDisposedException">If the instance is disposed.</exception>
    /// <exception cref="System.InvalidOperationException">If called after FinalizeTag.</exception>
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Reliability", "CA2014:Do not use stackalloc in loops", Justification = "<Pending>")]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("CodeQuality", "IDE0079:Remove unnecessary suppression", Justification = "<Pending>")]
    public void Update(System.ReadOnlySpan<System.Byte> data)
    {
        System.ObjectDisposedException.ThrowIf(_disposed, $"This {nameof(Poly1305)} instance has been disposed.");
        if (_finalized)
        {
            throw new System.InvalidOperationException("Poly1305 has already been finalized.");
        }

        // If there is pending data, try to fill to 16 bytes first.
        if (_pendingLen > 0)
        {
            System.Int32 need = 16 - _pendingLen;
            System.Int32 take = data.Length < need ? data.Length : need;
            if (take > 0)
            {
                data[..take].CopyTo(System.MemoryExtensions.AsSpan(_pending, _pendingLen));
                _pendingLen += take;
                data = data[take..];
            }

            if (_pendingLen == 16)
            {
                // Process a full 16-byte block as 17 bytes with trailing 0x01 (n[4] = 1).
                System.Span<System.Byte> block17 = stackalloc System.Byte[17];
                System.MemoryExtensions.AsSpan(_pending).CopyTo(block17);
                block17[16] = 0x01;

                // Full block => isFinalBlock = false (there IS the 17th byte)
                AddBlock(_acc, block17, isFinalBlock: false);

                // Reduce after each block (keeps values bounded)
                // Multiply() already followed by Modulo() inside AddBlock path, so nothing extra here.

                _pendingLen = 0;
            }
        }

        // Now data length is multiple of 16 possibly; process as many full blocks as possible
        while (data.Length >= 16)
        {
            // Take exactly 16 -> treat as 17-byte with block[16] = 0x01
            System.Span<System.Byte> block17 = stackalloc System.Byte[17];
            data[..16].CopyTo(block17);
            block17[16] = 0x01;

            AddBlock(_acc, block17, isFinalBlock: false);
            data = data[16..];
        }

        // Stash the tail (<16) into pending
        if (!data.IsEmpty)
        {
            data.CopyTo(System.MemoryExtensions.AsSpan(_pending, _pendingLen));
            _pendingLen += data.Length;
        }
    }

    /// <summary>
    /// Finalizes the MAC computation and writes the 16-byte tag into <paramref name="tag16"/>.
    /// After finalization, the instance rejects further <see cref="Update"/> calls.
    /// </summary>
    /// <param name="tag16">Destination span for the 16-byte tag.</param>
    /// <exception cref="System.ObjectDisposedException">If the instance is disposed.</exception>
    /// <exception cref="System.ArgumentException">If <paramref name="tag16"/> is shorter than 16 bytes.</exception>
    /// <exception cref="System.InvalidOperationException">If already finalized.</exception>
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public void FinalizeTag(System.Span<System.Byte> tag16)
    {
        System.ObjectDisposedException.ThrowIf(_disposed, $"This {nameof(Poly1305)} instance has been disposed.");
        if (tag16.Length < TagSize)
        {
            throw new System.ArgumentException($"Tag buffer must be {TagSize} bytes.", nameof(tag16));
        }

        if (_finalized)
        {
            throw new System.InvalidOperationException("Poly1305 has already been finalized.");
        }

        // If there is a remaining partial block (<16), we pad with 0x01 **inside** the 16 bytes (no 17th byte),
        // i.e., block length = partialLen + 1 <= 16, and isFinalBlock = true.
        if (_pendingLen > 0)
        {
            System.Span<System.Byte> block = stackalloc System.Byte[17]; // we will pass length = (_pendingLen + 1) (<=16)
            block.Clear();
            System.MemoryExtensions.AsSpan(_pending, 0, _pendingLen).CopyTo(block);
            block[_pendingLen] = 0x01;

            // Final block (<16+1), isFinalBlock = true, so AddBlock will set n[4] = 0.
            AddBlock(_acc, block[..(_pendingLen + 1)], isFinalBlock: true);
            System.MemoryExtensions.AsSpan(_pending).Clear();
            _pendingLen = 0;
        }

        // Produce the tag = (acc mod p) + s (little-endian 16 bytes)
        FinalizeTag(_acc, tag16);

        // Wipe and lock
        _finalized = true;

        // Clear sensitive state
        System.MemoryExtensions.AsSpan(_acc).Clear();
    }

    //
    // Optional helpers (convenience overloads).
    //

    /// <summary>
    /// Computes tag for the data fed via <see cref="Update"/> and returns a new 16-byte array.
    /// </summary>
    public System.Byte[] FinalizeTag()
    {
        System.Byte[] tag = new System.Byte[TagSize];
        FinalizeTag(tag);
        return tag;
    }

    /// <summary>
    /// One-shot helper compatible with incremental: Update(data); FinalizeTag(tag16);
    /// (Đã có Compute/ComputeTag one-shot ở trên, hàm này chỉ để thuận tiện khi code đã gọi Update trước đó.)
    /// </summary>
    public void ComputeTagIncremental(System.ReadOnlySpan<System.Byte> message, System.Span<System.Byte> destination)
    {
        Update(message);
        FinalizeTag(destination);
    }

    #endregion Public Methods

    #region Private Methods

    /// <summary>
    /// Clamps the r value according to RFC 8439.
    /// </summary>
    /// <param name="rBytes">The r portion of the key (16 bytes).</param>
    /// <param name="r">Array to store the clamped r value.</param>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
    private static void ClampR(
        System.ReadOnlySpan<System.Byte> rBytes,
        System.Span<System.UInt32> r)
    {
        System.Diagnostics.Debug.Assert(rBytes.Length >= 16);
        System.Diagnostics.Debug.Assert(r.Length >= 5);

        // Convert to uint array (little-endian)
        r[0] = System.Buffers.Binary.BinaryPrimitives.ReadUInt32LittleEndian(rBytes[..4]) & 0x0FFF_FFFC;
        r[1] = System.Buffers.Binary.BinaryPrimitives.ReadUInt32LittleEndian(rBytes.Slice(4, 4)) & 0x0FFF_FFFC;
        r[2] = System.Buffers.Binary.BinaryPrimitives.ReadUInt32LittleEndian(rBytes.Slice(8, 4)) & 0x0FFF_FFFC;
        r[3] = System.Buffers.Binary.BinaryPrimitives.ReadUInt32LittleEndian(rBytes.Slice(12, 4)) & 0x0FFF_FFFF;
        r[4] = 0;
    }

    /// <summary>
    /// Adds a message block to the accumulator.
    /// </summary>
    /// <param name="accumulator">The current accumulator value.</param>
    /// <param name="block">The block data to add (already padded).</param>
    /// <param name="isFinalBlock">Whether this is the final block.</param>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
    private void AddBlock(
        System.Span<System.UInt32> accumulator,
        System.ReadOnlySpan<System.Byte> block, System.Boolean isFinalBlock)
    {
        // Convert block to uint array with proper little-endian handling
        System.Span<System.UInt32> n =
        [
            // i = 0 (offset = 0)
            isFinalBlock && block.Length < 4
                ? GetUInt32OrZero(block, 0)
                : System.Buffers.Binary.BinaryPrimitives.ReadUInt32LittleEndian(block[..4]),
            // i = 1 (offset = 4)
            isFinalBlock && block.Length < 8
                ? GetUInt32OrZero(block, 4)
                : System.Buffers.Binary.BinaryPrimitives.ReadUInt32LittleEndian(block.Slice(4, 4)),
            // i = 2 (offset = 8)
            isFinalBlock && block.Length < 12
                ? GetUInt32OrZero(block, 8)
                : System.Buffers.Binary.BinaryPrimitives.ReadUInt32LittleEndian(block.Slice(8, 4)),
            // i = 3 (offset = 12)
            isFinalBlock && block.Length < 16
                ? GetUInt32OrZero(block, 12)
                : System.Buffers.Binary.BinaryPrimitives.ReadUInt32LittleEndian(block.Slice(12, 4)),
            // i = 4 (offset = 16)
            (System.UInt32)(isFinalBlock && block.Length <= 16 ? 0 : block[16]),
        ];

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
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining |
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
    private static System.UInt32 GetUInt32OrZero(System.ReadOnlySpan<System.Byte> data, System.Int32 offset)
    {
        System.UInt32 result = 0;
        System.Int32 bytesAvailable = System.Math.Min(4, System.Math.Max(0, data.Length - offset));

        for (System.Int32 i = 0; i < bytesAvailable; i++)
        {
            result |= (System.UInt32)data[offset + i] << 8 * i;
        }

        return result;
    }

    /// <summary>
    /// Adds two 130-bit integers represented as uint arrays.
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
    private static void Add(System.Span<System.UInt32> a, System.ReadOnlySpan<System.UInt32> b)
    {
        System.Diagnostics.Debug.Assert(a.Length >= 5, "Span a must have at least 5 elements");
        System.Diagnostics.Debug.Assert(b.Length >= 5, "Span b must have at least 5 elements");

        System.UInt64 carry = 0;

        // i = 0
        carry += (System.UInt64)a[0] + b[0];
        a[0] = (System.UInt32)carry;
        carry >>= 32;

        // i = 1
        carry += (System.UInt64)a[1] + b[1];
        a[1] = (System.UInt32)carry;
        carry >>= 32;

        // i = 2
        carry += (System.UInt64)a[2] + b[2];
        a[2] = (System.UInt32)carry;
        carry >>= 32;

        // i = 3
        carry += (System.UInt64)a[3] + b[3];
        a[3] = (System.UInt32)carry;
        carry >>= 32;

        // i = 4
        carry += (System.UInt64)a[4] + b[4];
        a[4] = (System.UInt32)carry;
    }

    /// <summary>
    /// Multiplies a 130-bit integer by another 130-bit integer.
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
    private static void Multiply(System.Span<System.UInt32> a, System.ReadOnlySpan<System.UInt32> b)
    {
        System.Span<System.UInt32> product = stackalloc System.UInt32[10];

        // Clean state
        product.Clear();

        // Multiply each component
        MultiplyRow(a, b, product, 0);
        MultiplyRow(a, b, product, 1);
        MultiplyRow(a, b, product, 2);
        MultiplyRow(a, b, product, 3);
        MultiplyRow(a, b, product, 4);

        // Reduce modulo 2^130 - 5
        ReduceProduct(a, product);
    }


    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
    private static void MultiplyRow(
        System.ReadOnlySpan<System.UInt32> a,
        System.ReadOnlySpan<System.UInt32> b,
        System.Span<System.UInt32> product, System.Int32 row)
    {
        System.UInt64 carry = 0;
        System.UInt32 aValue = a[row];

        // Unroll inner loop cho performance

        // j = 0
        System.UInt64 t = (System.UInt64)aValue * b[0] + product[row] + carry;
        product[row] = (System.UInt32)t;
        carry = t >> 32;

        // j = 1
        t = (System.UInt64)aValue * b[1] + product[row + 1] + carry;
        product[row + 1] = (System.UInt32)t;
        carry = t >> 32;

        // j = 2
        t = (System.UInt64)aValue * b[2] + product[row + 2] + carry;
        product[row + 2] = (System.UInt32)t;
        carry = t >> 32;

        // j = 3
        t = (System.UInt64)aValue * b[3] + product[row + 3] + carry;
        product[row + 3] = (System.UInt32)t;
        carry = t >> 32;

        // j = 4
        t = (System.UInt64)aValue * b[4] + product[row + 4] + carry;
        product[row + 4] = (System.UInt32)t;
        carry = t >> 32;

        // Store final carry
        if (row + 5 < 10)
        {
            product[row + 5] = (System.UInt32)carry;
        }
    }

    /// <summary>
    /// Reduces a 260-bit product modulo 2^130 - 5 to a 130-bit result.
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
    private static void ReduceProduct(
        System.Span<System.UInt32> result,
        System.ReadOnlySpan<System.UInt32> product)
    {
        // Copy the low 130 bits
        product[..5].CopyTo(result);

        // Multiply the high 130 bits by 5 (because 2^130 ≡ 5 (mod 2^130 - 5))
        // and add to the result

        // i = 0
        System.UInt64 t = (System.UInt64)product[5] * 5 + result[0];
        result[0] = (System.UInt32)t;
        System.UInt32 carry = (System.UInt32)(t >> 32);

        // i = 1
        t = (System.UInt64)product[6] * 5 + result[1] + carry;
        result[1] = (System.UInt32)t;
        carry = (System.UInt32)(t >> 32);

        // i = 2
        t = (System.UInt64)product[7] * 5 + result[2] + carry;
        result[2] = (System.UInt32)t;
        carry = (System.UInt32)(t >> 32);

        // i = 3
        t = (System.UInt64)product[8] * 5 + result[3] + carry;
        result[3] = (System.UInt32)t;
        carry = (System.UInt32)(t >> 32);

        // i = 4
        t = (System.UInt64)product[9] * 5 + result[4] + carry;
        result[4] = (System.UInt32)t;

        // Final reduction if needed (result might be >= 2^130 - 5)
        Modulo(result);
    }

    /// <summary>
    /// Reduces a value modulo 2^130 - 5.
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
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
    /// Determines if one ProtocolType is greater than or equal to another.
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
    private static System.Boolean IsGreaterOrEqual(
        System.ReadOnlySpan<System.UInt32> a,
        System.ReadOnlySpan<System.UInt32> b)
    {
        // Compare from most significant word down
        if (a[4] > b[4])
        {
            return true;
        }

        if (a[4] < b[4])
        {
            return false;
        }

        if (a[3] > b[3])
        {
            return true;
        }

        if (a[3] < b[3])
        {
            return false;
        }

        if (a[2] > b[2])
        {
            return true;
        }

        if (a[2] < b[2])
        {
            return false;
        }

        if (a[1] > b[1])
        {
            return true;
        }

        if (a[1] < b[1])
        {
            return false;
        }

        if (a[0] > b[0])
        {
            return true;
        }

        if (a[0] < b[0])
        {
            return false;
        }

        // All words are equal
        return true;
    }

    /// <summary>
    /// Subtracts one ProtocolType from another.
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
    private static void Subtract(
        System.Span<System.UInt32> a,
        System.ReadOnlySpan<System.UInt32> b)
    {
        System.UInt64 diff = (System.UInt64)a[0] - b[0];
        a[0] = (System.UInt32)diff;
        System.UInt32 borrow = diff >> 63 == 1 ? 1u : 0u;
        diff = (System.UInt64)a[1] - b[1] - borrow;
        a[1] = (System.UInt32)diff;
        borrow = (System.UInt64)a[1] + borrow > a[1] ? 1u : 0u; // or: (diff >> 63)==1 ? 1u : 0u

        diff = (System.UInt64)a[2] - b[2] - borrow;
        a[2] = (System.UInt32)diff;
        borrow = diff >> 63 == 1 ? 1u : 0u;

        diff = (System.UInt64)a[3] - b[3] - borrow;
        a[3] = (System.UInt32)diff;
        borrow = diff >> 63 == 1 ? 1u : 0u;

        diff = (System.UInt64)a[4] - b[4] - borrow;
        a[4] = (System.UInt32)diff;
    }

    /// <summary>
    /// Finalizes the authentication tag by adding s and ensuring it's exactly 16 bytes.
    /// </summary>
    /// <param name="accumulator">The current accumulator value.</param>
    /// <param name="tag">The span where the tag will be written.</param>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
    private void FinalizeTag(
        System.ReadOnlySpan<System.UInt32> accumulator,
        System.Span<System.Byte> tag)
    {
        System.Diagnostics.Debug.Assert(tag.Length >= TagSize);

        // CAFEBABE a copy of the accumulator for the final operations
        System.Span<System.UInt32> result = stackalloc System.UInt32[5];
        accumulator.CopyTo(result);

        // Ensure the result is fully reduced modulo 2^130 - 5
        Modulo(result);

        // Push s
        System.Span<System.UInt32> finalResult = stackalloc System.UInt32[4];
        System.UInt64 carry = 0;
        for (System.Byte i = 0; i < 4; i++)
        {
            carry += (System.UInt64)result[i] + _s![i];
            finalResult[i] = (System.UInt32)carry;
            carry >>= 32;
        }

        // Convert to bytes (little-endian)
        System.Buffers.Binary.BinaryPrimitives.WriteUInt32LittleEndian(tag[..4], finalResult[0]);
        System.Buffers.Binary.BinaryPrimitives.WriteUInt32LittleEndian(tag.Slice(4, 4), finalResult[1]);
        System.Buffers.Binary.BinaryPrimitives.WriteUInt32LittleEndian(tag.Slice(8, 4), finalResult[2]);
        System.Buffers.Binary.BinaryPrimitives.WriteUInt32LittleEndian(tag.Slice(12, 4), finalResult[3]);
    }

    #endregion Private Methods

    #region IDisposable

    /// <summary>
    /// Securely clears sensitive data when the object is disposed.
    /// </summary>
    [System.Diagnostics.DebuggerNonUserCode]
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
