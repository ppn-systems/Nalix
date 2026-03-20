// Copyright (c) 2025-2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using Nalix.Common.Security;
using Nalix.Shared.Memory.Internal;
using Nalix.Shared.Security.Internal;
using Nalix.Shared.Security.Primitives;

namespace Nalix.Shared.Security.Symmetric;

/// <summary>
/// Provides ChaCha20 stream cipher encryption and decryption (RFC 7539).
/// </summary>
/// <remarks>
/// <para>
/// This implementation uses <see cref="System.Runtime.CompilerServices.InlineArrayAttribute"/>
/// to embed all working buffers (state, working copy, keystream) directly inside the struct
/// layout of the class instance, eliminating the three managed-array heap allocations
/// (<c>UInt32[16]</c>, <c>UInt32[16]</c>, <c>Byte[64]</c>) that the previous version required.
/// </para>
/// <para>
/// <strong>Thread-safety:</strong> This instance is <b>NOT</b> thread-safe because it mutates
/// shared inline buffers during encryption. For concurrent use, create separate instances or
/// implement instance pooling.
/// </para>
/// <para>
/// See <see href="https://tools.ietf.org/html/rfc7539">RFC 7539</see> for the full specification.
/// </para>
/// </remarks>
[System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never)]
public ref struct ChaCha20
{
    #region Constants

    /// <summary>Required key length in bytes (256-bit).</summary>
    public const System.Byte KeySize = 32;

    /// <summary>Required nonce length in bytes (96-bit).</summary>
    public const System.Byte NonceSize = 12;

    /// <summary>Size of a single keystream block in bytes.</summary>
    public const System.Byte BlockSize = 64;

    /// <summary>Number of 32-bit words in the ChaCha20 state matrix.</summary>
    public const System.Byte StateLength = 16;

    #endregion Constants

    #region Inline Array Definitions

    [System.Runtime.CompilerServices.InlineArray(StateLength)]
    private struct StateBuffer
    {
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Roslynator", "RCS1169:Make field read-only", Justification = "<Pending>")]
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Roslynator", "RCS1213:Remove unused member declaration", Justification = "<Pending>")]
        private System.UInt32 _e0;
    }

    [System.Runtime.CompilerServices.InlineArray(StateLength)]
    private struct WorkingBuffer
    {
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Roslynator", "RCS1169:Make field read-only", Justification = "<Pending>")]
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Roslynator", "RCS1213:Remove unused member declaration", Justification = "<Pending>")]
        private System.UInt32 _e0;
    }

    [System.Runtime.CompilerServices.InlineArray(BlockSize)]
    private struct KeystreamBuffer
    {
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Roslynator", "RCS1169:Make field read-only", Justification = "<Pending>")]
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Roslynator", "RCS1213:Remove unused member declaration", Justification = "<Pending>")]
        private System.Byte _e0;
    }

    #endregion Inline Array Definitions

    #region Fields

    private System.Boolean _cleared;
    private StateBuffer _state;
    private WorkingBuffer _working;
    private KeystreamBuffer _keystream;

    #endregion Fields

    #region Constructors

    /// <summary>
    /// Initializes a new <see cref="ChaCha20"/> instance with the specified key, nonce, and
    /// initial block counter.
    /// </summary>
    public ChaCha20(System.Byte[] key, System.Byte[] nonce, System.UInt32 counter)
    {
        System.ArgumentNullException.ThrowIfNull(key);
        System.ArgumentNullException.ThrowIfNull(nonce);

        InitializeKey(new System.ReadOnlySpan<System.Byte>(key));
        InitializeNonce(new System.ReadOnlySpan<System.Byte>(nonce), counter);
    }

    /// <summary>
    /// Initializes a new <see cref="ChaCha20"/> instance with the specified key, nonce, and
    /// initial block counter using spans (zero-copy).
    /// </summary>
    public ChaCha20(
        System.ReadOnlySpan<System.Byte> key,
        System.ReadOnlySpan<System.Byte> nonce,
        System.UInt32 counter)
    {
        InitializeKey(key);
        InitializeNonce(nonce, counter);
    }

    #endregion Constructors

    #region Public — Generate Key Block

    /// <summary>
    /// Generates one 64-byte keystream block into <paramref name="dst"/> at the current counter,
    /// then advances the internal counter by 1 (per RFC 7539).
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
    public void GenerateKeyBlock(scoped System.Span<System.Byte> dst)
    {
        ThrowIfCleared();

        System.Span<System.UInt32> stateSpan = _state;
        System.Span<System.UInt32> workingSpan = _working;
        System.Span<System.Byte> keystreamSpan = _keystream;

        GenerateBlock(stateSpan, workingSpan, keystreamSpan);

        System.Int32 n = dst.Length < BlockSize ? dst.Length : BlockSize;
        keystreamSpan[..n].CopyTo(dst);
    }

    #endregion Public — Generate Key Block

    #region Encryption Methods

    /// <summary>
    /// Encrypts <paramref name="src"/> into <paramref name="dst"/>.
    /// Returns number of bytes written.
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
    public System.Int32 Encrypt(
        System.ReadOnlySpan<System.Byte> src,
        System.Span<System.Byte> dst)
    {
        ThrowIfCleared();

        if (dst.Length < src.Length)
        {
            ThrowHelper.ThrowOutputLengthMismatchException();
        }

        EncryptSpanInternal(src, dst, src.Length);
        return dst.Length;
    }

    #endregion Encryption Methods

    #region Decryption Methods

    /// <summary>
    /// Decrypts <paramref name="src"/> into <paramref name="dst"/>.
    /// Identical to <see cref="Encrypt(System.ReadOnlySpan{System.Byte}, System.Span{System.Byte})"/>.
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
    public System.Int32 Decrypt(System.ReadOnlySpan<System.Byte> src, System.Span<System.Byte> dst) => Encrypt(src, dst);

    #endregion Decryption Methods

    #region Public — Reset

    /// <summary>
    /// Securely zeroes all sensitive key material and internal state.
    /// </summary>
    [System.Diagnostics.DebuggerNonUserCode]
    public void Clear()
    {
        if (!_cleared)
        {
            MemorySecurity.ZeroMemory(System.Runtime.InteropServices.MemoryMarshal.AsBytes<System.UInt32>((System.Span<System.UInt32>)_state));
            MemorySecurity.ZeroMemory(System.Runtime.InteropServices.MemoryMarshal.AsBytes<System.UInt32>((System.Span<System.UInt32>)_working));
            MemorySecurity.ZeroMemory(_keystream);
            _cleared = true;
        }
    }

    #endregion Public — Reset

    #region Private — Guard

    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    private readonly void ThrowIfCleared()
    {
        if (_cleared)
        {
            throw new System.ObjectDisposedException(nameof(ChaCha20), $"This {nameof(ChaCha20)} instance has been cleared.");
        }
    }

    #endregion Private — Guard

    #region Private — Initialization

    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    private static System.UInt32 LoadLittleEndian32(System.ReadOnlySpan<System.Byte> source, System.Int32 offset)
        => System.Buffers.Binary.BinaryPrimitives.ReadUInt32LittleEndian(source[offset..]);

    private void InitializeKey(System.ReadOnlySpan<System.Byte> key)
    {
        if (key.Length != KeySize)
        {
            ThrowHelper.ThrowInvalidKeyLengthException($"Key length must be {KeySize}. Actual: {key.Length}");
        }

        System.Span<System.UInt32> s = _state;
        s[0] = 0x61707865;
        s[1] = 0x3320646e;
        s[2] = 0x79622d32;
        s[3] = 0x6b206574;

        for (System.Int32 i = 0; i < 8; i++)
        {
            s[4 + i] = LoadLittleEndian32(key, i * 4);
        }
    }

    private void InitializeNonce(System.ReadOnlySpan<System.Byte> nonce, System.UInt32 counter)
    {
        if (nonce.Length != NonceSize)
        {
            ThrowHelper.ThrowInvalidNonceLengthException($"Nonce length must be {NonceSize}. Actual: {nonce.Length}");
        }

        System.Span<System.UInt32> s = _state;
        s[12] = counter;

        for (System.Int32 i = 0; i < 3; i++)
        {
            s[13 + i] = LoadLittleEndian32(nonce, i * 4);
        }
    }

    #endregion Private — Initialization

    #region Private — SIMD Detection

    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    private static SimdMode DetectSimdMode()
    {
        return System.Runtime.Intrinsics.Vector512.IsHardwareAccelerated ? SimdMode.V512
             : System.Runtime.Intrinsics.Vector256.IsHardwareAccelerated ? SimdMode.V256
             : System.Runtime.Intrinsics.Vector128.IsHardwareAccelerated ? SimdMode.V128
             : SimdMode.NONE;
    }

    #endregion Private — SIMD Detection

    #region Private — Core Block Function

    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
    private static void GenerateBlock(
        System.Span<System.UInt32> state,
        System.Span<System.UInt32> working,
        System.Span<System.Byte> keystream)
    {
        state.CopyTo(working);

        for (System.Int32 i = 0; i < 10; i++)
        {
            // Column rounds
            QuarterRound(working, 0, 4, 8, 12);
            QuarterRound(working, 1, 5, 9, 13);
            QuarterRound(working, 2, 6, 10, 14);
            QuarterRound(working, 3, 7, 11, 15);
            // Diagonal rounds
            QuarterRound(working, 0, 5, 10, 15);
            QuarterRound(working, 1, 6, 11, 12);
            QuarterRound(working, 2, 7, 8, 13);
            QuarterRound(working, 3, 4, 9, 14);
        }

        for (System.Int32 i = 0; i < StateLength; i++)
        {
            System.Buffers.Binary.BinaryPrimitives.WriteUInt32LittleEndian(
                keystream[(4 * i)..],
                BitwiseOperations.Add(working[i], state[i]));
        }

        // Advance block counter — UInt32 wraps naturally; check == 0 for overflow
        state[12] = BitwiseOperations.AddOne(state[12]);

        if (state[12] == 0u)  // FIX: was `<= 0` which is misleading for UInt32
        {
            // Counter overflow: carry into next word.
            // The caller is responsible for not exceeding 2^70 bytes per nonce.
            state[13] = BitwiseOperations.AddOne(state[13]);
        }
    }

    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    private static void QuarterRound(
        System.Span<System.UInt32> x,
        System.Int32 a, System.Int32 b, System.Int32 c, System.Int32 d)
    {
        x[a] = BitwiseOperations.Add(x[a], x[b]);
        x[d] = System.Numerics.BitOperations.RotateLeft(BitwiseOperations.XOr(x[d], x[a]), 16);

        x[c] = BitwiseOperations.Add(x[c], x[d]);
        x[b] = System.Numerics.BitOperations.RotateLeft(BitwiseOperations.XOr(x[b], x[c]), 12);

        x[a] = BitwiseOperations.Add(x[a], x[b]);
        x[d] = System.Numerics.BitOperations.RotateLeft(BitwiseOperations.XOr(x[d], x[a]), 8);

        x[c] = BitwiseOperations.Add(x[c], x[d]);
        x[b] = System.Numerics.BitOperations.RotateLeft(BitwiseOperations.XOr(x[b], x[c]), 7);
    }

    #endregion Private — Core Block Function

    #region Private — Span-Based Encrypt Core

    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
    private void EncryptSpanInternal(
        System.ReadOnlySpan<System.Byte> src,
        System.Span<System.Byte> dst,
        System.Int32 numBytes)
    {
        if (numBytes < 0 || numBytes > src.Length || numBytes > dst.Length)
        {
            throw new System.ArgumentOutOfRangeException(nameof(numBytes));
        }

        System.Span<System.UInt32> stateSpan = _state;
        System.Span<System.UInt32> workingSpan = _working;
        System.Span<System.Byte> keystreamSpan = _keystream;

        System.Int32 offset = 0;
        System.Int32 fullBlocks = numBytes / BlockSize;
        System.Int32 tailBytes = numBytes - (fullBlocks * BlockSize);

        for (System.Int32 block = 0; block < fullBlocks; block++)
        {
            GenerateBlock(stateSpan, workingSpan, keystreamSpan);
            for (System.Int32 i = 0; i < BlockSize; i++)
            {
                dst[offset + i] = (System.Byte)(src[offset + i] ^ keystreamSpan[i]);
            }

            offset += BlockSize;
        }

        if (tailBytes > 0)
        {
            GenerateBlock(stateSpan, workingSpan, keystreamSpan);
            for (System.Int32 i = 0; i < tailBytes; i++)
            {
                dst[offset + i] = (System.Byte)(src[offset + i] ^ keystreamSpan[i]);
            }
        }
    }

    #endregion Private — Span-Based Encrypt Core

    #region Private — Array-Based Encrypt Core (SIMD)

    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
    private void EncryptBytesInternal(
        System.Byte[] output,
        System.Byte[] input,
        System.Int32 numBytes,
        SimdMode simdMode)
    {
        System.Span<System.UInt32> stateSpan = _state;
        System.Span<System.UInt32> workingSpan = _working;
        System.Span<System.Byte> keystreamSpan = _keystream;

        System.Byte[]? keystreamArray = simdMode is not SimdMode.NONE
            ? System.Buffers.ArrayPool<System.Byte>.Shared.Rent(BlockSize)
            : null;

        try
        {
            System.Int32 offset = 0;
            System.Int32 fullBlocks = numBytes / BlockSize;
            System.Int32 tailBytes = numBytes - (fullBlocks * BlockSize);

            for (System.Int32 block = 0; block < fullBlocks; block++)
            {
                GenerateBlock(stateSpan, workingSpan, keystreamSpan);

                if (simdMode is SimdMode.NONE)
                {
                    for (System.Int32 i = 0; i < BlockSize; i += 4)
                    {
                        System.Int32 p = i + offset;
                        output[p] = (System.Byte)(input[p] ^ keystreamSpan[i]);
                        output[p + 1] = (System.Byte)(input[p + 1] ^ keystreamSpan[i + 1]);
                        output[p + 2] = (System.Byte)(input[p + 2] ^ keystreamSpan[i + 2]);
                        output[p + 3] = (System.Byte)(input[p + 3] ^ keystreamSpan[i + 3]);
                    }
                }
                else
                {
                    keystreamSpan.CopyTo(System.MemoryExtensions.AsSpan(keystreamArray, 0, BlockSize));

                    if (simdMode is SimdMode.V512)
                    {
                        var inputV = System.Runtime.Intrinsics.Vector512.Create(input, offset);
                        var tmpV = System.Runtime.Intrinsics.Vector512.Create(keystreamArray!, 0);
                        System.Runtime.Intrinsics.Vector512.CopyTo(inputV ^ tmpV, output, offset);
                    }
                    else if (simdMode is SimdMode.V256)
                    {
                        var inV0 = System.Runtime.Intrinsics.Vector256.Create(input, offset);
                        var tmpV0 = System.Runtime.Intrinsics.Vector256.Create(keystreamArray!, 0);
                        System.Runtime.Intrinsics.Vector256.CopyTo(inV0 ^ tmpV0, output, offset);

                        var inV1 = System.Runtime.Intrinsics.Vector256.Create(input, offset + 32);
                        var tmpV1 = System.Runtime.Intrinsics.Vector256.Create(keystreamArray!, 32);
                        System.Runtime.Intrinsics.Vector256.CopyTo(inV1 ^ tmpV1, output, offset + 32);
                    }
                    else // V128
                    {
                        for (System.Int32 chunk = 0; chunk < BlockSize; chunk += 16)
                        {
                            var inV = System.Runtime.Intrinsics.Vector128.Create(input, offset + chunk);
                            var tmpV = System.Runtime.Intrinsics.Vector128.Create(keystreamArray!, chunk);
                            System.Runtime.Intrinsics.Vector128.CopyTo(inV ^ tmpV, output, offset + chunk);
                        }
                    }
                }

                offset += BlockSize;
            }

            if (tailBytes > 0)
            {
                GenerateBlock(stateSpan, workingSpan, keystreamSpan);
                for (System.Int32 i = 0; i < tailBytes; i++)
                {
                    output[offset + i] = (System.Byte)(input[offset + i] ^ keystreamSpan[i]);
                }
            }
        }
        finally
        {
            if (keystreamArray is not null)
            {
                System.Buffers.ArrayPool<System.Byte>.Shared.Return(keystreamArray, clearArray: true);
            }
        }
    }

    #endregion Private — Array-Based Encrypt Core (SIMD)
}
