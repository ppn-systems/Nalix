// Copyright (c) 2025-2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using Nalix.Framework.Security.Primitives;

namespace Nalix.Framework.Security.Hashing;

/// <summary>
/// High-performance, zero-allocation implementation of the Poly1305 message authentication code
/// (MAC) algorithm as a <see langword="ref struct"/>.
/// </summary>
/// <remarks>
/// <para>
/// Poly1305 is a cryptographically strong MAC algorithm designed by Daniel J. Bernstein.
/// It is used in various cryptographic protocols including the ChaCha20-Poly1305 AEAD
/// cipher suite in TLS 1.3.
/// </para>
/// <para>
/// This implementation follows RFC 8439 and provides constant-time operations for
/// enhanced security. All internal buffers use <c>InlineArray</c> structs, so the
/// entire instance lives on the stack with <b>zero heap allocations</b>.
/// </para>
/// <para>
/// Because this is a <see langword="ref struct"/>, it cannot be boxed, captured by async
/// methods, or stored in fields of reference types. Use the static one-shot APIs
/// (<see cref="Compute(System.ReadOnlySpan{byte}, System.ReadOnlySpan{byte}, System.Span{byte})"/>
/// and <see cref="Verify"/>) when possible; use the incremental
/// (<see cref="Update"/>/<see cref="FinalizeTag(System.Span{byte})"/>) API when
/// message data arrives in chunks.
/// </para>
/// <para>
/// <strong>Lifetime:</strong> Call <see cref="Clear"/> when finished to securely zero all
/// sensitive key material. Since <c>ref struct</c> cannot implement
/// <see cref="System.IDisposable"/>, <c>using</c> statements are not available; prefer
/// a <c>try/finally</c> block instead.
/// </para>
/// </remarks>
[System.Diagnostics.StackTraceHidden]
[System.Diagnostics.DebuggerNonUserCode]
[System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never)]
public struct Poly1305
{
    #region Constants

    /// <summary>
    /// The size, in bytes, of the Poly1305 key (32 bytes = 256 bits).
    /// </summary>
    public const byte KeySize = 32;

    /// <summary>
    /// The size, in bytes, of the authentication tag produced by Poly1305 (16 bytes = 128 bits).
    /// </summary>
    public const byte TagSize = 16;

    /// <summary>
    /// Number of 32-bit words in the accumulator / r / prime representation (130 bits -> 5 words).
    /// </summary>
    private const byte WordCount = 5;

    /// <summary>
    /// Number of 32-bit words in the s part of the key (128 bits -> 4 words).
    /// </summary>
    private const byte SWordCount = 4;

    /// <summary>
    /// Block size in bytes for Poly1305 message processing.
    /// </summary>
    private const byte BlockBytes = 16;

    /// <summary>
    /// Block size + 1 byte for the 0x01 padding sentinel.
    /// </summary>
    private const byte PaddedBlockBytes = 17;

    #endregion Constants

    #region Inline Array Definitions

    /// <summary>
    /// Inline buffer: 5 × <see cref="uint"/> = 20 bytes.
    /// Used for the accumulator, r key part, and arithmetic scratch space.
    /// </summary>
    [System.Runtime.CompilerServices.InlineArray(WordCount)]
    private struct UInt32x5
    {
        private uint _e0;
    }

    /// <summary>
    /// Inline buffer: 4 × <see cref="uint"/> = 16 bytes.
    /// Used for the s key part.
    /// </summary>
    [System.Runtime.CompilerServices.InlineArray(SWordCount)]
    private struct UInt32x4
    {
        private uint _e0;
    }

    /// <summary>
    /// Inline buffer: 10 × <see cref="uint"/> = 40 bytes.
    /// Used as scratch space during 130-bit multiplication.
    /// </summary>
    [System.Runtime.CompilerServices.InlineArray(10)]
    private struct UInt32x10
    {
        private uint _e0;
    }

    /// <summary>
    /// Inline buffer: 16 × <see cref="byte"/> = 16 bytes.
    /// Used to hold a pending partial message block (0–16 bytes).
    /// </summary>
    [System.Runtime.CompilerServices.InlineArray(BlockBytes)]
    private struct ByteBlock16
    {
        private byte _e0;
    }

    /// <summary>
    /// Inline buffer: 17 × <see cref="byte"/> = 17 bytes.
    /// Used to hold a padded message block (16 data bytes + 0x01 sentinel).
    /// </summary>
    [System.Runtime.CompilerServices.InlineArray(PaddedBlockBytes)]
    private struct ByteBlock17
    {
        private byte _e0;
    }

    #endregion Inline Array Definitions

    #region Static Read-Only

    /// <summary>
    /// The prime number p = 2¹³⁰ − 5, represented as five 32-bit little-endian words.
    /// </summary>
    private static readonly UInt32x5 s_prime = CreatePrime();

    /// <summary>
    /// Initializes the compile-time constant for the Poly1305 prime.
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    private static UInt32x5 CreatePrime()
    {
        UInt32x5 p = default;
        System.Span<uint> span = p;
        span[0] = 0xFFFF_FFFB;
        span[1] = 0xFFFF_FFFF;
        span[2] = 0xFFFF_FFFF;
        span[3] = 0xFFFF_FFFF;
        span[4] = 0x0000_0003;
        return p;
    }

    #endregion Static Read-Only

    #region Fields

    /// <summary>
    /// The clamped r part of the key (5 words, 130-bit representation).
    /// </summary>
    private UInt32x5 _r;

    /// <summary>
    /// The s part of the key (4 words, 128-bit).
    /// </summary>
    private UInt32x4 _s;

    /// <summary>
    /// The running accumulator h (5 words, 130-bit).
    /// </summary>
    private UInt32x5 _acc;

    /// <summary>
    /// Buffer holding a partial (not-yet-full) message block between <see cref="Update"/> calls.
    /// </summary>
    private ByteBlock16 _pending;

    /// <summary>
    /// Number of valid bytes in <see cref="_pending"/> (0–16).
    /// </summary>
    private int _pendingLen;

    /// <summary>
    /// Whether <see cref="FinalizeTag(System.Span{byte})"/> has already been called.
    /// </summary>
    private bool _finalized;

    /// <summary>
    /// Whether <see cref="Clear"/> has been called (analogous to disposed).
    /// </summary>
    private bool _cleared;

    #endregion Fields

    #region Constructors

    /// <summary>
    /// Initializes a new <see cref="Poly1305"/> instance using a 32-byte key.
    /// </summary>
    /// <param name="key">
    /// A 32-byte key. The first 16 bytes are clamped and used as <c>r</c>;
    /// the last 16 bytes are used as <c>s</c>.
    /// </param>
    /// <exception cref="System.ArgumentException">
    /// <paramref name="key"/> length is not <see cref="KeySize"/> (32) bytes.
    /// </exception>
    public Poly1305(System.ReadOnlySpan<byte> key)
    {
        if (key.Length != KeySize)
        {
            throw new System.ArgumentException(
                $"Key must be {KeySize} bytes.", nameof(key));
        }

        _acc = default;
        _pending = default;
        _pendingLen = 0;
        _finalized = false;
        _cleared = false;

        // Extract and clamp r (first 16 bytes) per RFC 8439 §2.5
        ClampR(key[..16], _r);

        // Extract s (last 16 bytes) as four little-endian 32-bit words
        System.ReadOnlySpan<byte> sBytes = key.Slice(16, 16);
        System.Span<uint> sSpan = _s;

        for (int i = 0; i < SWordCount; i++)
        {
            sSpan[i] = System.Buffers.Binary.BinaryPrimitives.ReadUInt32LittleEndian(
                sBytes.Slice(i * 4, 4));
        }
    }

    #endregion Constructors

    #region Public — Static One-Shot API

    /// <summary>
    /// Computes the Poly1305 MAC for <paramref name="message"/> using <paramref name="key"/>
    /// and writes the 16-byte tag into <paramref name="destination"/>.
    /// </summary>
    /// <param name="key">A 32-byte key.</param>
    /// <param name="message">The message to authenticate.</param>
    /// <param name="destination">
    /// Destination span; must be at least <see cref="TagSize"/> (16) bytes.
    /// </param>
    /// <exception cref="System.ArgumentException">
    /// <paramref name="key"/> is not 32 bytes or <paramref name="destination"/> is too small.
    /// </exception>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public static void Compute(
        System.ReadOnlySpan<byte> key,
        System.ReadOnlySpan<byte> message,
        System.Span<byte> destination)
    {
        if (key.Length != KeySize)
        {
            throw new System.ArgumentException(
                $"Key must be {KeySize} bytes.", nameof(key));
        }

        if (destination.Length < TagSize)
        {
            throw new System.ArgumentException(
                $"Destination buffer must be at least {TagSize} bytes.", nameof(destination));
        }

        Poly1305 poly = new(key);

        try
        {
            poly.ComputeTag(message, destination);
        }
        finally
        {
            poly.Clear();
        }
    }

    /// <summary>
    /// Computes the Poly1305 MAC and returns a new 16-byte array.
    /// </summary>
    /// <param name="key">A 32-byte key.</param>
    /// <param name="message">The message to authenticate.</param>
    /// <returns>A 16-byte authentication tag.</returns>
    /// <exception cref="System.ArgumentException">Thrown when <paramref name="key"/> is not <see cref="KeySize"/> bytes.</exception>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public static byte[] Compute(
        System.ReadOnlySpan<byte> key,
        System.ReadOnlySpan<byte> message)
    {
        byte[] tag = new byte[TagSize];
        Compute(key, message, tag);
        return tag;
    }

    /// <summary>
    /// Computes the Poly1305 MAC and returns a new 16-byte array (array overload).
    /// </summary>
    /// <param name="key">A 32-byte key.</param>
    /// <param name="message">The message to authenticate.</param>
    /// <returns>A 16-byte authentication tag.</returns>
    /// <exception cref="System.ArgumentNullException">
    /// <paramref name="key"/> or <paramref name="message"/> is <see langword="null"/>.
    /// </exception>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public static byte[] Compute(byte[] key, byte[] message)
    {
        System.ArgumentNullException.ThrowIfNull(key);
        System.ArgumentNullException.ThrowIfNull(message);

        return Compute(
            System.MemoryExtensions.AsSpan(key),
            System.MemoryExtensions.AsSpan(message));
    }

    /// <summary>
    /// Verifies a Poly1305 MAC against a message using the specified key.
    /// Uses constant-time comparison to prevent timing side-channel attacks.
    /// </summary>
    /// <param name="key">A 32-byte key.</param>
    /// <param name="message">The message to verify.</param>
    /// <param name="tag">The 16-byte authentication tag to verify against.</param>
    /// <returns>
    /// <see langword="true"/> if <paramref name="tag"/> is valid; otherwise <see langword="false"/>.
    /// </returns>
    /// <exception cref="System.ArgumentException">
    /// <paramref name="tag"/> is not <see cref="TagSize"/> (16) bytes, or <paramref name="key"/> is not <see cref="KeySize"/> bytes.
    /// </exception>
    public static bool Verify(
        System.ReadOnlySpan<byte> key,
        System.ReadOnlySpan<byte> message,
        System.ReadOnlySpan<byte> tag)
    {
        if (tag.Length != TagSize)
        {
            throw new System.ArgumentException(
                $"Tag must be {TagSize} bytes.", nameof(tag));
        }

        System.Span<byte> computedTag = stackalloc byte[TagSize];
        Compute(key, message, computedTag);

        return BitwiseOperations.FixedTimeEquals(tag, computedTag);
    }

    #endregion Public — Static One-Shot API

    #region Public — Instance One-Shot

    /// <summary>
    /// Computes the Poly1305 MAC for <paramref name="message"/> and writes the 16-byte tag
    /// into <paramref name="destination"/>.
    /// </summary>
    /// <param name="message">The message to authenticate.</param>
    /// <param name="destination">
    /// Destination span; must be at least <see cref="TagSize"/> (16) bytes.
    /// </param>
    /// <exception cref="System.ObjectDisposedException">This instance has been cleared.</exception>
    /// <exception cref="System.ArgumentException">
    /// <paramref name="destination"/> is too small.
    /// </exception>
    public readonly void ComputeTag(
        System.ReadOnlySpan<byte> message,
        System.Span<byte> destination)
    {
        this.ThrowIfCleared();

        if (destination.Length < TagSize)
        {
            throw new System.ArgumentException(
                $"Destination buffer must be at least {TagSize} bytes.", nameof(destination));
        }

        // Fresh accumulator for the one-shot path
        UInt32x5 accumulator = default;

        int offset = 0;
        int messageLength = message.Length;

        // Scratch block (17 bytes: 16 data + 0x01 padding)
        ByteBlock17 block17 = default;

        while (offset < messageLength)
        {
            // Clear block to avoid stale data from the previous iteration
            ((System.Span<byte>)block17).Clear();

            // Determine block size (final block may be < 16 bytes)
            int blockSize = System.Math.Min(BlockBytes, messageLength - offset);

            // Copy message slice into the block
            message.Slice(offset, blockSize).CopyTo(block17);

            // Append 0x01 padding byte after the data
            ((System.Span<byte>)block17)[blockSize] = 0x01;

            // Absorb: isFinalBlock = true only when blockSize < 16 (last partial block)
            this.AddBlock(accumulator, ((System.ReadOnlySpan<byte>)block17)[..(blockSize + 1)], blockSize < BlockBytes);

            offset += blockSize;
        }

        // Produce the tag
        this.FinalizeTagCore(accumulator, destination);
    }

    #endregion Public — Instance One-Shot

    #region Public — Incremental API

    /// <summary>
    /// Incrementally absorbs message data. May be called multiple times before
    /// <see cref="FinalizeTag(System.Span{byte})"/>.
    /// </summary>
    /// <param name="data">Next chunk of the message.</param>
    /// <exception cref="System.ObjectDisposedException">This instance has been cleared.</exception>
    /// <exception cref="System.InvalidOperationException">
    /// Called after <see cref="FinalizeTag(System.Span{byte})"/>.
    /// </exception>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public void Update(scoped System.ReadOnlySpan<byte> data)
    {
        this.ThrowIfCleared();

        if (_finalized)
        {
            throw new System.InvalidOperationException(
                "Poly1305 has already been finalized.");
        }

        ByteBlock17 block17 = default;
        System.Span<byte> block17Span = block17;
        System.Span<byte> pendingSpan = _pending;

        // ── Try to fill the pending buffer to a full 16-byte block ──
        if (_pendingLen > 0)
        {
            int need = BlockBytes - _pendingLen;
            int take = data.Length < need ? data.Length : need;

            if (take > 0)
            {
                data[..take].CopyTo(pendingSpan[_pendingLen..]);
                _pendingLen += take;
                data = data[take..];
            }

            if (_pendingLen is BlockBytes)
            {
                // Full block ready — absorb it
                pendingSpan.CopyTo(block17Span);
                block17Span[BlockBytes] = 0x01;

                this.AddBlock(_acc, ((System.ReadOnlySpan<byte>)block17)[..PaddedBlockBytes], isFinalBlock: false);

                _pendingLen = 0;
            }
        }

        // ── Process as many full 16-byte blocks as possible ──
        while (data.Length >= BlockBytes)
        {
            block17Span.Clear();
            data[..BlockBytes].CopyTo(block17Span);
            block17Span[BlockBytes] = 0x01;

            this.AddBlock(_acc, ((System.ReadOnlySpan<byte>)block17)[..PaddedBlockBytes], isFinalBlock: false);

            data = data[BlockBytes..];
        }

        // ── Stash remaining tail (< 16 bytes) ──
        if (!data.IsEmpty)
        {
            data.CopyTo(pendingSpan[_pendingLen..]);
            _pendingLen += data.Length;
        }
    }

    /// <summary>
    /// Finalizes the MAC computation and writes the 16-byte tag into <paramref name="tag16"/>.
    /// After finalization, further <see cref="Update"/> calls will throw.
    /// </summary>
    /// <param name="tag16">
    /// Destination span; must be at least <see cref="TagSize"/> (16) bytes.
    /// </param>
    /// <exception cref="System.ObjectDisposedException">This instance has been cleared.</exception>
    /// <exception cref="System.ArgumentException">
    /// <paramref name="tag16"/> is shorter than 16 bytes.
    /// </exception>
    /// <exception cref="System.InvalidOperationException">Already finalized.</exception>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public void FinalizeTag(System.Span<byte> tag16)
    {
        this.ThrowIfCleared();

        if (tag16.Length < TagSize)
        {
            throw new System.ArgumentException(
                $"Tag buffer must be {TagSize} bytes.", nameof(tag16));
        }

        if (_finalized)
        {
            throw new System.InvalidOperationException(
                "Poly1305 has already been finalized.");
        }

        // ── Absorb any remaining partial block ──
        if (_pendingLen > 0)
        {
            ByteBlock17 block = default;
            System.Span<byte> blockSpan = block;
            blockSpan.Clear();

            ((System.ReadOnlySpan<byte>)_pending)[.._pendingLen].CopyTo(blockSpan);
            blockSpan[_pendingLen] = 0x01;

            // Partial final block: isFinalBlock = true -> n[4] = 0
            this.AddBlock(
                _acc,
                ((System.ReadOnlySpan<byte>)block)[..(_pendingLen + 1)],
                isFinalBlock: true);

            ((System.Span<byte>)_pending).Clear();
            _pendingLen = 0;
        }

        // ── Produce the tag = (accumulator mod p) + s ──
        this.FinalizeTagCore(_acc, tag16);

        _finalized = true;

        // Clear the accumulator (sensitive state)
        ((System.Span<uint>)_acc).Clear();
    }

    /// <summary>
    /// Finalizes the MAC computation and returns a new 16-byte array containing the tag.
    /// </summary>
    /// <returns>A 16-byte authentication tag.</returns>
    /// <exception cref="System.ObjectDisposedException">This instance has been cleared.</exception>
    /// <exception cref="System.InvalidOperationException">Already finalized.</exception>
    public byte[] FinalizeTag()
    {
        byte[] tag = new byte[TagSize];
        this.FinalizeTag(tag);
        return tag;
    }

    /// <summary>
    /// Convenience method: absorbs <paramref name="message"/> via <see cref="Update"/> and then
    /// finalizes via <see cref="FinalizeTag(System.Span{byte})"/>.
    /// </summary>
    /// <param name="message">The full message to authenticate.</param>
    /// <param name="destination">
    /// Destination span; must be at least <see cref="TagSize"/> (16) bytes.
    /// </param>
    /// <exception cref="System.ObjectDisposedException">This instance has been cleared.</exception>
    /// <exception cref="System.ArgumentException"><paramref name="destination"/> is too small.</exception>
    /// <exception cref="System.InvalidOperationException">Thrown if the instance has already been finalized.</exception>
    public void ComputeTagIncremental(
        System.ReadOnlySpan<byte> message,
        System.Span<byte> destination)
    {
        this.Update(message);
        this.FinalizeTag(destination);
    }

    #endregion Public — Incremental API

    #region Public — Clear (replaces Dispose)

    /// <summary>
    /// Securely zeroes all sensitive key material and internal state.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Because <see langword="ref struct"/> cannot implement <see cref="System.IDisposable"/>,
    /// call this method explicitly (preferably in a <c>finally</c> block) when done.
    /// </para>
    /// <para>
    /// Uses <see cref="MemorySecurity.ZeroMemory(System.Span{byte})"/>
    /// to guarantee the JIT will not elide the zeroing.
    /// </para>
    /// </remarks>
    [System.Diagnostics.DebuggerNonUserCode]
    public void Clear()
    {
        if (!_cleared)
        {
            MemorySecurity.ZeroMemory(System.Runtime.InteropServices.MemoryMarshal.AsBytes((System.Span<uint>)_r));
            MemorySecurity.ZeroMemory(System.Runtime.InteropServices.MemoryMarshal.AsBytes((System.Span<uint>)_s));
            MemorySecurity.ZeroMemory(System.Runtime.InteropServices.MemoryMarshal.AsBytes((System.Span<uint>)_acc));
            MemorySecurity.ZeroMemory(_pending);

            _pendingLen = 0;
            _cleared = true;
        }
    }

    #endregion Public — Clear (replaces Dispose)

    #region Private — Initialization

    /// <summary>
    /// Clamps the r value according to RFC 8439 §2.5.
    /// Certain bits of r are cleared to ensure that multiplication stays within bounds.
    /// </summary>
    /// <param name="rBytes">The first 16 bytes of the key.</param>
    /// <param name="r">Destination: 5-word clamped r value.</param>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
    private static void ClampR(
        System.ReadOnlySpan<byte> rBytes,
        System.Span<uint> r)
    {
        System.Diagnostics.Debug.Assert(rBytes.Length >= 16);
        System.Diagnostics.Debug.Assert(r.Length >= WordCount);

        r[0] = System.Buffers.Binary.BinaryPrimitives.ReadUInt32LittleEndian(rBytes[..4]) & 0x0FFF_FFFC;
        r[1] = System.Buffers.Binary.BinaryPrimitives.ReadUInt32LittleEndian(rBytes.Slice(4, 4)) & 0x0FFF_FFFC;
        r[2] = System.Buffers.Binary.BinaryPrimitives.ReadUInt32LittleEndian(rBytes.Slice(8, 4)) & 0x0FFF_FFFC;
        r[3] = System.Buffers.Binary.BinaryPrimitives.ReadUInt32LittleEndian(rBytes.Slice(12, 4)) & 0x0FFF_FFFF;
        r[4] = 0;
    }

    #endregion Private — Initialization

    #region Private — Guard

    /// <summary>
    /// Throws <see cref="System.ObjectDisposedException"/> if <see cref="Clear"/> has been called.
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    private readonly void ThrowIfCleared()
    {
        if (_cleared)
        {
            throw new System.ObjectDisposedException(
                nameof(Poly1305),
                $"This {nameof(Poly1305)} instance has been cleared.");
        }
    }

    #endregion Private — Guard

    #region Private — Block Processing

    /// <summary>
    /// Adds a (possibly partial) padded message block to the accumulator, multiplies by r,
    /// and reduces modulo 2¹³⁰ − 5.
    /// </summary>
    /// <param name="accumulator">The running accumulator (modified in-place).</param>
    /// <param name="block">
    /// Padded block data: up to 17 bytes (16 message + 0x01 sentinel).
    /// </param>
    /// <param name="isFinalBlock">
    /// <see langword="true"/> for the last (possibly short) block — the high word is set to 0
    /// instead of reading the 17th byte.
    /// </param>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
    private readonly void AddBlock(
        scoped System.Span<uint> accumulator,
        scoped System.ReadOnlySpan<byte> block,
        bool isFinalBlock)
    {
        UInt32x5 nBuf = default;
        System.Span<uint> n = nBuf;

        n[0] = isFinalBlock && block.Length < 4
            ? ReadPartialUInt32(block, 0)
            : System.Buffers.Binary.BinaryPrimitives.ReadUInt32LittleEndian(block[..4]);

        n[1] = isFinalBlock && block.Length < 8
            ? ReadPartialUInt32(block, 4)
            : System.Buffers.Binary.BinaryPrimitives.ReadUInt32LittleEndian(block.Slice(4, 4));

        n[2] = isFinalBlock && block.Length < 12
            ? ReadPartialUInt32(block, 8)
            : System.Buffers.Binary.BinaryPrimitives.ReadUInt32LittleEndian(block.Slice(8, 4));

        n[3] = isFinalBlock && block.Length < 16
            ? ReadPartialUInt32(block, 12)
            : System.Buffers.Binary.BinaryPrimitives.ReadUInt32LittleEndian(block.Slice(12, 4));

        n[4] = (uint)(isFinalBlock && block.Length <= 16 ? 0 : block[16]);

        // accumulator += n
        Add(accumulator, nBuf);

        // accumulator *= r
        Multiply(accumulator, _r);

        // accumulator %= p
        Modulo(accumulator);
    }

    /// <summary>
    /// Reads up to 4 bytes starting at <paramref name="offset"/> from <paramref name="data"/>,
    /// returning them as a little-endian <see cref="uint"/>.
    /// Out-of-range bytes are treated as zero.
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining | System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
    private static uint ReadPartialUInt32(
        System.ReadOnlySpan<byte> data,
        int offset)
    {
        uint result = 0;
        int available = System.Math.Min(4, System.Math.Max(0, data.Length - offset));

        for (int i = 0; i < available; i++)
        {
            result |= (uint)data[offset + i] << (8 * i);
        }

        return result;
    }

    #endregion Private — Block Processing

    #region Private — 130-bit Arithmetic

    /// <summary>
    /// Adds two 130-bit integers: <c>a += b</c>.
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
    private static void Add(
        System.Span<uint> a,
        System.ReadOnlySpan<uint> b)
    {
        System.Diagnostics.Debug.Assert(a.Length >= WordCount);
        System.Diagnostics.Debug.Assert(b.Length >= WordCount);

        ulong carry = 0;

        carry += (ulong)a[0] + b[0];
        a[0] = (uint)carry;
        carry >>= 32;

        carry += (ulong)a[1] + b[1];
        a[1] = (uint)carry;
        carry >>= 32;

        carry += (ulong)a[2] + b[2];
        a[2] = (uint)carry;
        carry >>= 32;

        carry += (ulong)a[3] + b[3];
        a[3] = (uint)carry;
        carry >>= 32;

        carry += (ulong)a[4] + b[4];
        a[4] = (uint)carry;
    }

    /// <summary>
    /// Multiplies two 130-bit integers: <c>a = a × b mod p</c>.
    /// Uses a 10-word intermediate product buffer (inline array, stack-allocated).
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
    private static void Multiply(
        System.Span<uint> a,
        System.ReadOnlySpan<uint> b)
    {
        UInt32x10 productBuf = default;
        System.Span<uint> product = productBuf;
        product.Clear();

        // Schoolbook multiplication: 5 × 5 -> 10 words
        MultiplyRow(a, b, product, 0);
        MultiplyRow(a, b, product, 1);
        MultiplyRow(a, b, product, 2);
        MultiplyRow(a, b, product, 3);
        MultiplyRow(a, b, product, 4);

        // Reduce the 260-bit product modulo p = 2¹³⁰ − 5
        ReduceProduct(a, productBuf);
    }

    /// <summary>
    /// Computes one row of the schoolbook multiplication:
    /// <c>product[row..row+5] += a[row] × b[0..4]</c>.
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
    private static void MultiplyRow(
        System.ReadOnlySpan<uint> a,
        System.ReadOnlySpan<uint> b,
        System.Span<uint> product,
        int row)
    {
        ulong carry = 0;
        uint aVal = a[row];

        // j = 0
        ulong t = ((ulong)aVal * b[0]) + product[row] + carry;
        product[row] = (uint)t;
        carry = t >> 32;

        // j = 1
        t = ((ulong)aVal * b[1]) + product[row + 1] + carry;
        product[row + 1] = (uint)t;
        carry = t >> 32;

        // j = 2
        t = ((ulong)aVal * b[2]) + product[row + 2] + carry;
        product[row + 2] = (uint)t;
        carry = t >> 32;

        // j = 3
        t = ((ulong)aVal * b[3]) + product[row + 3] + carry;
        product[row + 3] = (uint)t;
        carry = t >> 32;

        // j = 4
        t = ((ulong)aVal * b[4]) + product[row + 4] + carry;
        product[row + 4] = (uint)t;
        carry = t >> 32;

        // Store the final carry into the next word (if within bounds)
        if (row + 5 < 10)
        {
            product[row + 5] = (uint)carry;
        }
    }

    /// <summary>
    /// Reduces a 260-bit product modulo p = 2¹³⁰ − 5.
    /// Since 2¹³⁰ ≡ 5 (mod p), the high 130 bits are multiplied by 5 and added to the low 130 bits.
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
    private static void ReduceProduct(
        System.Span<uint> result,
        System.ReadOnlySpan<uint> product)
    {
        // Copy low 130 bits (words 0–4)
        product[..WordCount].CopyTo(result);

        // High words [5..9] × 5, added to result
        ulong t = ((ulong)product[5] * 5) + result[0];
        result[0] = (uint)t;
        uint carry = (uint)(t >> 32);

        t = ((ulong)product[6] * 5) + result[1] + carry;
        result[1] = (uint)t;
        carry = (uint)(t >> 32);

        t = ((ulong)product[7] * 5) + result[2] + carry;
        result[2] = (uint)t;
        carry = (uint)(t >> 32);

        t = ((ulong)product[8] * 5) + result[3] + carry;
        result[3] = (uint)t;
        carry = (uint)(t >> 32);

        t = ((ulong)product[9] * 5) + result[4] + carry;
        result[4] = (uint)t;

        // One final conditional reduction
        Modulo(result);
    }

    /// <summary>
    /// Conditionally subtracts p if <paramref name="value"/> ≥ p.
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
    private static void Modulo(System.Span<uint> value)
    {
        if (IsGreaterOrEqual(value, s_prime))
        {
            Subtract(value, s_prime);
        }
    }

    /// <summary>
    /// Determines if <paramref name="a"/> ≥ <paramref name="b"/> (unsigned, most-significant-first).
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
    private static bool IsGreaterOrEqual(
        System.ReadOnlySpan<uint> a,
        System.ReadOnlySpan<uint> b)
    {
        // Compare from most significant word (index 4) down to least (index 0).
        for (int i = WordCount - 1; i >= 0; i--)
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

        // All words equal -> a == b -> a ≥ b is true.
        return true;
    }

    /// <summary>
    /// Subtracts <paramref name="b"/> from <paramref name="a"/>: <c>a -= b</c>.
    /// Assumes a ≥ b (no underflow).
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
    private static void Subtract(
        System.Span<uint> a,
        System.ReadOnlySpan<uint> b)
    {
        ulong borrow = 0;

        // i = 0
        ulong diff = (ulong)a[0] - b[0] - borrow;
        a[0] = (uint)diff;
        borrow = (diff >> 63) & 1;

        // i = 1
        diff = (ulong)a[1] - b[1] - borrow;
        a[1] = (uint)diff;
        borrow = (diff >> 63) & 1;

        // i = 2
        diff = (ulong)a[2] - b[2] - borrow;
        a[2] = (uint)diff;
        borrow = (diff >> 63) & 1;

        // i = 3
        diff = (ulong)a[3] - b[3] - borrow;
        a[3] = (uint)diff;
        borrow = (diff >> 63) & 1;

        // i = 4
        diff = (ulong)a[4] - b[4] - borrow;
        a[4] = (uint)diff;
    }

    #endregion Private — 130-bit Arithmetic

    #region Private — Tag Finalization

    /// <summary>
    /// Produces the final 16-byte tag: <c>tag = (accumulator mod p) + s</c>, serialized as
    /// four little-endian 32-bit words. The 5th accumulator word (bits 128–129) is discarded
    /// after the addition because the tag is only 128 bits.
    /// </summary>
    /// <param name="accumulator">The fully-reduced 130-bit accumulator.</param>
    /// <param name="tag">Destination for the 16-byte tag.</param>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
    private readonly void FinalizeTagCore(
        System.ReadOnlySpan<uint> accumulator,
        System.Span<byte> tag)
    {
        System.Diagnostics.Debug.Assert(tag.Length >= TagSize);

        // Copy accumulator for final operations
        UInt32x5 resultBuf = default;
        System.Span<uint> result = resultBuf;
        accumulator.CopyTo(result);

        // Ensure fully reduced modulo p
        Modulo(result);

        // Add s (128-bit addition — only 4 words)
        System.ReadOnlySpan<uint> sSpan = _s;
        ulong carry = 0;

        for (int i = 0; i < SWordCount; i++)
        {
            carry += (ulong)result[i] + sSpan[i];
            result[i] = (uint)carry;
            carry >>= 32;
        }

        // Serialize the low 4 words (128 bits) as little-endian bytes
        System.Buffers.Binary.BinaryPrimitives.WriteUInt32LittleEndian(tag[..4], result[0]);
        System.Buffers.Binary.BinaryPrimitives.WriteUInt32LittleEndian(tag.Slice(4, 4), result[1]);
        System.Buffers.Binary.BinaryPrimitives.WriteUInt32LittleEndian(tag.Slice(8, 4), result[2]);
        System.Buffers.Binary.BinaryPrimitives.WriteUInt32LittleEndian(tag.Slice(12, 4), result[3]);
    }

    #endregion Private — Tag Finalization
}
