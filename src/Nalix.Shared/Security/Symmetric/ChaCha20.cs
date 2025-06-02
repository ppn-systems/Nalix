// Copyright (c) 2025-2026 PPN Corporation. All rights reserved.

using Nalix.Common.Enums;
using Nalix.Shared.Memory.Internal;
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
public ref struct ChaCha20
{
    #region Constants

    /// <summary>
    /// Required key length in bytes (256-bit).
    /// </summary>
    public const System.Byte KeySize = 32;

    /// <summary>
    /// Required nonce length in bytes (96-bit).
    /// </summary>
    public const System.Byte NonceSize = 12;

    /// <summary>
    /// Size of a single keystream block in bytes.
    /// </summary>
    public const System.Byte BlockSize = 64;

    /// <summary>
    /// Number of 32-bit words in the ChaCha20 state matrix.
    /// </summary>
    public const System.Byte StateLength = 16;

    #endregion Constants

    #region Inline Array Definitions

    /// <summary>
    /// Inline buffer holding 16 × <see cref="System.UInt32"/> = 64 bytes.
    /// Used for the ChaCha20 state matrix.
    /// </summary>
    [System.Runtime.CompilerServices.InlineArray(StateLength)]
    private struct StateBuffer
    {
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Roslynator", "RCS1169:Make field read-only", Justification = "<Pending>")]
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Roslynator", "RCS1213:Remove unused member declaration", Justification = "<Pending>")]
        private System.UInt32 _e0;
    }

    /// <summary>
    /// Inline buffer holding 16 × <see cref="System.UInt32"/> = 64 bytes.
    /// Used as a scratch working copy during the block function.
    /// </summary>
    [System.Runtime.CompilerServices.InlineArray(StateLength)]
    private struct WorkingBuffer
    {
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Roslynator", "RCS1169:Make field read-only", Justification = "<Pending>")]
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Roslynator", "RCS1213:Remove unused member declaration", Justification = "<Pending>")]
        private System.UInt32 _e0;
    }

    /// <summary>
    /// Inline buffer holding 64 × <see cref="System.Byte"/> = 64 bytes.
    /// Used to store the serialized keystream output of a single block.
    /// </summary>
    [System.Runtime.CompilerServices.InlineArray(BlockSize)]
    private struct KeystreamBuffer
    {
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Roslynator", "RCS1169:Make field read-only", Justification = "<Pending>")]
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Roslynator", "RCS1213:Remove unused member declaration", Justification = "<Pending>")]
        private System.Byte _e0;
    }

    #endregion Inline Array Definitions

    #region Fields

    /// <summary>
    /// Whether <see cref="Clear"/> has been called (analogous to disposed).
    /// </summary>
    private System.Boolean _cleared;

    /// <summary>
    /// The ChaCha20 state matrix (constants + key + counter + nonce).
    /// </summary>
    private StateBuffer _state;

    /// <summary>
    /// Reusable scratch buffer for the 20-round block function.
    /// </summary>
    private WorkingBuffer _working;

    /// <summary>
    /// Reusable buffer for the 64-byte keystream output per block.
    /// </summary>
    private KeystreamBuffer _keystream;

    #endregion Fields

    #region Constructors

    /// <summary>
    /// Initializes a new <see cref="ChaCha20"/> instance with the specified key, nonce, and
    /// initial block counter.
    /// </summary>
    /// <remarks>
    /// See <see href="https://tools.ietf.org/html/rfc7539#page-10">RFC 7539 Section 2.4</see>
    /// for a detailed description of the inputs.
    /// </remarks>
    /// <param name="key">
    /// A 32-byte (256-bit) key, treated as a concatenation of eight 32-bit little-endian integers.
    /// </param>
    /// <param name="nonce">
    /// A 12-byte (96-bit) nonce, treated as a concatenation of three 32-bit little-endian integers.
    /// </param>
    /// <param name="counter">
    /// The initial 32-bit block counter value.
    /// </param>
    /// <exception cref="System.ArgumentNullException">
    /// <paramref name="key"/> or <paramref name="nonce"/> is <see langword="null"/>.
    /// </exception>
    /// <exception cref="System.ArgumentException">
    /// <paramref name="key"/> length is not 32 or <paramref name="nonce"/> length is not 12.
    /// </exception>
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
    /// <inheritdoc cref="ChaCha20(System.Byte[], System.Byte[], System.UInt32)"
    ///             path="/remarks|/param|/exception"/>
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
    /// <param name="dst">
    /// Destination span to receive the keystream block.
    /// If <c>dst.Length &lt; 64</c>, only the first <c>dst.Length</c> bytes are written.
    /// </param>
    /// <exception cref="System.ObjectDisposedException">
    /// This instance has already been disposed.
    /// </exception>
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
    /// Encrypts <paramref name="numBytes"/> bytes from <paramref name="input"/> into the
    /// preallocated <paramref name="output"/> buffer.
    /// </summary>
    /// <remarks>
    /// Since ChaCha20 is a symmetric XOR cipher, encryption and decryption are the same operation.
    /// </remarks>
    /// <param name="output">Output byte array; must have at least <paramref name="numBytes"/> capacity.</param>
    /// <param name="input">Input byte array to encrypt.</param>
    /// <param name="numBytes">Number of bytes to encrypt from <paramref name="input"/>.</param>
    /// <param name="simdMode">SIMD acceleration mode (default is auto-detect).</param>
    /// <exception cref="System.ArgumentNullException">
    /// <paramref name="output"/> or <paramref name="input"/> is <see langword="null"/>.
    /// </exception>
    /// <exception cref="System.ArgumentOutOfRangeException">
    /// <paramref name="numBytes"/> is negative, exceeds <paramref name="input"/> length,
    /// or <paramref name="output"/> is too small.
    /// </exception>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
    public void EncryptBytes(
        [System.Diagnostics.CodeAnalysis.NotNull] System.Byte[] output,
        [System.Diagnostics.CodeAnalysis.NotNull] System.Byte[] input,
        [System.Diagnostics.CodeAnalysis.NotNull] System.Int32 numBytes,
        [System.Diagnostics.CodeAnalysis.NotNull] SimdMode simdMode = SimdMode.AUTO_DETECT)
    {
        ThrowIfCleared();

        System.ArgumentNullException.ThrowIfNull(output);
        System.ArgumentNullException.ThrowIfNull(input);

        if (numBytes < 0 || numBytes > input.Length)
        {
            throw new System.ArgumentOutOfRangeException(
                nameof(numBytes),
                "The number of bytes to read must be between [0..input.Length]");
        }

        if (output.Length < numBytes)
        {
            throw new System.ArgumentOutOfRangeException(
                nameof(output),
                $"Output byte array should be able to take at least {numBytes}");
        }

        if (simdMode is SimdMode.AUTO_DETECT)
        {
            simdMode = DetectSimdMode();
        }

        EncryptBytesInternal(output, input, numBytes, simdMode);
    }

    /// <summary>
    /// Encrypts all bytes from <paramref name="input"/> into the preallocated
    /// <paramref name="output"/> buffer.
    /// </summary>
    /// <inheritdoc cref="EncryptBytes(System.Byte[], System.Byte[], System.Int32, SimdMode)"
    ///             path="/remarks|/exception"/>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
    public void EncryptBytes(
        [System.Diagnostics.CodeAnalysis.NotNull] System.Byte[] output,
        [System.Diagnostics.CodeAnalysis.NotNull] System.Byte[] input,
        [System.Diagnostics.CodeAnalysis.NotNull] SimdMode simdMode = SimdMode.AUTO_DETECT)
    {
        ThrowIfCleared();

        System.ArgumentNullException.ThrowIfNull(output);
        System.ArgumentNullException.ThrowIfNull(input);

        if (simdMode is SimdMode.AUTO_DETECT)
        {
            simdMode = DetectSimdMode();
        }

        EncryptBytesInternal(output, input, input.Length, simdMode);
    }

    /// <summary>
    /// Encrypts <paramref name="numBytes"/> bytes from <paramref name="input"/> and returns a
    /// newly allocated byte array containing the ciphertext.
    /// </summary>
    /// <param name="input">Input byte array to encrypt.</param>
    /// <param name="numBytes">Number of bytes to encrypt from <paramref name="input"/>.</param>
    /// <param name="simdMode">SIMD acceleration mode (default is auto-detect).</param>
    /// <returns>A new byte array containing the encrypted data.</returns>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
    [return: System.Diagnostics.CodeAnalysis.NotNull]
    public System.Byte[] EncryptBytes(
        [System.Diagnostics.CodeAnalysis.NotNull] System.Byte[] input,
        [System.Diagnostics.CodeAnalysis.NotNull] System.Int32 numBytes,
        [System.Diagnostics.CodeAnalysis.NotNull] SimdMode simdMode = SimdMode.AUTO_DETECT)
    {
        ThrowIfCleared();

        System.ArgumentNullException.ThrowIfNull(input);

        if (numBytes < 0 || numBytes > input.Length)
        {
            throw new System.ArgumentOutOfRangeException(
                nameof(numBytes),
                "The number of bytes to read must be between [0..input.Length]");
        }

        if (simdMode is SimdMode.AUTO_DETECT)
        {
            simdMode = DetectSimdMode();
        }

        System.Byte[] result = new System.Byte[numBytes];
        EncryptBytesInternal(result, input, numBytes, simdMode);
        return result;
    }

    /// <summary>
    /// Encrypts all bytes from <paramref name="input"/> and returns a newly allocated byte array
    /// containing the ciphertext.
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
    [return: System.Diagnostics.CodeAnalysis.NotNull]
    public System.Byte[] EncryptBytes(
        [System.Diagnostics.CodeAnalysis.NotNull] System.Byte[] input,
        [System.Diagnostics.CodeAnalysis.NotNull] SimdMode simdMode = SimdMode.AUTO_DETECT)
    {
        ThrowIfCleared();

        System.ArgumentNullException.ThrowIfNull(input);

        if (simdMode is SimdMode.AUTO_DETECT)
        {
            simdMode = DetectSimdMode();
        }

        System.Byte[] result = new System.Byte[input.Length];
        EncryptBytesInternal(result, input, input.Length, simdMode);
        return result;
    }

    /// <summary>
    /// Encrypts <paramref name="src"/> into <paramref name="dst"/> using the current state
    /// (XOR with the keystream).
    /// </summary>
    /// <param name="src">Source data to encrypt.</param>
    /// <param name="dst">Destination span; must be the same length as <paramref name="src"/>.</param>
    /// <exception cref="System.ArgumentException">
    /// <paramref name="dst"/> length does not equal <paramref name="src"/> length.
    /// </exception>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
    public void Encrypt(
        [System.Diagnostics.CodeAnalysis.NotNull] System.ReadOnlySpan<System.Byte> src,
        [System.Diagnostics.CodeAnalysis.NotNull] System.Span<System.Byte> dst)
    {
        ThrowIfCleared();

        if (dst.Length != src.Length)
        {
            ThrowHelper.ThrowOutputLengthMismatchException();
        }

        EncryptSpanInternal(src, dst, src.Length);
    }

    /// <summary>
    /// Tries to encrypt <paramref name="src"/> into <paramref name="dst"/>.
    /// Returns <see langword="false"/> if <paramref name="dst"/> is too small.
    /// </summary>
    /// <param name="src">Source data to encrypt.</param>
    /// <param name="dst">Destination span.</param>
    /// <param name="written">
    /// When this method returns <see langword="true"/>, contains the number of bytes written.
    /// </param>
    /// <returns>
    /// <see langword="true"/> if encryption succeeded; otherwise <see langword="false"/>.
    /// </returns>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
    [return: System.Diagnostics.CodeAnalysis.NotNull]
    public System.Boolean Encrypt(
        [System.Diagnostics.CodeAnalysis.NotNull] System.ReadOnlySpan<System.Byte> src,
        [System.Diagnostics.CodeAnalysis.NotNull] System.Span<System.Byte> dst,
        [System.Diagnostics.CodeAnalysis.NotNullWhen(true)] out System.Int32 written)
    {
        ThrowIfCleared();

        written = 0;

        if (dst.Length < src.Length)
        {
            return false;
        }

        Encrypt(src, dst);
        written = src.Length;
        return true;
    }

    #endregion Encryption Methods

    #region Decryption Methods

    /// <summary>
    /// Decrypts <paramref name="numBytes"/> bytes from <paramref name="input"/> into the
    /// preallocated <paramref name="output"/> buffer.
    /// </summary>
    /// <remarks>
    /// Since ChaCha20 is a symmetric XOR cipher, encryption and decryption are the same operation.
    /// </remarks>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
    public void DecryptBytes(
        [System.Diagnostics.CodeAnalysis.NotNull] System.Byte[] output,
        [System.Diagnostics.CodeAnalysis.NotNull] System.Byte[] input,
        [System.Diagnostics.CodeAnalysis.NotNull] System.Int32 numBytes,
        [System.Diagnostics.CodeAnalysis.NotNull] SimdMode simdMode = SimdMode.AUTO_DETECT)
    {
        ThrowIfCleared();

        System.ArgumentNullException.ThrowIfNull(output);
        System.ArgumentNullException.ThrowIfNull(input);

        if (numBytes < 0 || numBytes > input.Length)
        {
            throw new System.ArgumentOutOfRangeException(
                nameof(numBytes),
                "The number of bytes to read must be between [0..input.Length]");
        }

        if (output.Length < numBytes)
        {
            throw new System.ArgumentOutOfRangeException(
                nameof(output),
                $"Output byte array should be able to take at least {numBytes}");
        }

        if (simdMode is SimdMode.AUTO_DETECT)
        {
            simdMode = DetectSimdMode();
        }

        EncryptBytesInternal(output, input, numBytes, simdMode);
    }

    /// <summary>
    /// Decrypts all bytes from <paramref name="input"/> into the preallocated
    /// <paramref name="output"/> buffer.
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
    public void DecryptBytes(
        [System.Diagnostics.CodeAnalysis.NotNull] System.Byte[] output,
        [System.Diagnostics.CodeAnalysis.NotNull] System.Byte[] input,
        [System.Diagnostics.CodeAnalysis.NotNull] SimdMode simdMode = SimdMode.AUTO_DETECT)
    {
        ThrowIfCleared();

        System.ArgumentNullException.ThrowIfNull(output);
        System.ArgumentNullException.ThrowIfNull(input);

        if (simdMode is SimdMode.AUTO_DETECT)
        {
            simdMode = DetectSimdMode();
        }

        EncryptBytesInternal(output, input, input.Length, simdMode);
    }

    /// <summary>
    /// Decrypts <paramref name="numBytes"/> bytes from <paramref name="input"/> and returns
    /// a newly allocated byte array.
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
    [return: System.Diagnostics.CodeAnalysis.NotNull]
    public System.Byte[] DecryptBytes(
        [System.Diagnostics.CodeAnalysis.NotNull] System.Byte[] input,
        [System.Diagnostics.CodeAnalysis.NotNull] System.Int32 numBytes,
        [System.Diagnostics.CodeAnalysis.NotNull] SimdMode simdMode = SimdMode.AUTO_DETECT)
    {
        ThrowIfCleared();

        System.ArgumentNullException.ThrowIfNull(input);

        if (numBytes < 0 || numBytes > input.Length)
        {
            throw new System.ArgumentOutOfRangeException(
                nameof(numBytes),
                "The number of bytes to read must be between [0..input.Length]");
        }

        if (simdMode is SimdMode.AUTO_DETECT)
        {
            simdMode = DetectSimdMode();
        }

        System.Byte[] result = new System.Byte[numBytes];
        EncryptBytesInternal(result, input, numBytes, simdMode);
        return result;
    }

    /// <summary>
    /// Decrypts all bytes from <paramref name="input"/> and returns a newly allocated byte array.
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
    [return: System.Diagnostics.CodeAnalysis.NotNull]
    public System.Byte[] DecryptBytes(
        [System.Diagnostics.CodeAnalysis.NotNull] System.Byte[] input,
        [System.Diagnostics.CodeAnalysis.NotNull] SimdMode simdMode = SimdMode.AUTO_DETECT)
    {
        ThrowIfCleared();
        System.ArgumentNullException.ThrowIfNull(input);

        if (simdMode is SimdMode.AUTO_DETECT)
        {
            simdMode = DetectSimdMode();
        }

        System.Byte[] result = new System.Byte[input.Length];
        EncryptBytesInternal(result, input, input.Length, simdMode);
        return result;
    }

    /// <summary>
    /// Decrypts <paramref name="src"/> into <paramref name="dst"/>.
    /// For ChaCha20 this is identical to <see cref="Encrypt(System.ReadOnlySpan{System.Byte}, System.Span{System.Byte})"/>.
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
    public void Decrypt(
        [System.Diagnostics.CodeAnalysis.NotNull] System.ReadOnlySpan<System.Byte> src,
        [System.Diagnostics.CodeAnalysis.NotNull] System.Span<System.Byte> dst)
    {
        ThrowIfCleared();
        Encrypt(src, dst);
    }

    #endregion Decryption Methods

    #region In-Place Methods

    /// <summary>
    /// In-place encryption (XOR with keystream) of <paramref name="buffer"/>.
    /// </summary>
    /// <param name="buffer">Data buffer to encrypt in-place.</param>
    /// <exception cref="System.ObjectDisposedException">This instance has been disposed.</exception>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
    public void EncryptInPlace([System.Diagnostics.CodeAnalysis.NotNull] System.Span<System.Byte> buffer)
    {
        System.Span<System.UInt32> stateSpan = _state;
        System.Span<System.UInt32> workingSpan = _working;
        System.Span<System.Byte> keystreamSpan = _keystream;

        System.Int32 offset = 0;
        System.Int32 remaining = buffer.Length;

        while (remaining >= BlockSize)
        {
            GenerateBlock(stateSpan, workingSpan, keystreamSpan);

            for (System.Int32 i = 0; i < BlockSize; i++)
            {
                buffer[offset + i] = (System.Byte)(buffer[offset + i] ^ keystreamSpan[i]);
            }

            offset += BlockSize;
            remaining -= BlockSize;
        }

        if (remaining > 0)
        {
            GenerateBlock(stateSpan, workingSpan, keystreamSpan);

            for (System.Int32 i = 0; i < remaining; i++)
            {
                buffer[offset + i] = (System.Byte)(buffer[offset + i] ^ keystreamSpan[i]);
            }
        }
    }

    /// <summary>
    /// In-place decryption of <paramref name="buffer"/> (identical to
    /// <see cref="EncryptInPlace"/>).
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
    public void DecryptInPlace([System.Diagnostics.CodeAnalysis.NotNull] System.Span<System.Byte> buffer) => EncryptInPlace(buffer);

    #endregion In-Place Methods

    #region Static One-Shot API

    /// <summary>
    /// One-shot static API: encrypts (or decrypts) <paramref name="input"/> using ChaCha20.
    /// </summary>
    /// <param name="key">32-byte key.</param>
    /// <param name="nonce">12-byte nonce.</param>
    /// <param name="counter">Initial block counter.</param>
    /// <param name="input">Input data to encrypt/decrypt.</param>
    /// <param name="simdMode">SIMD acceleration mode (default is auto-detect).</param>
    /// <returns>A new byte array containing the encrypted/decrypted data.</returns>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
    [return: System.Diagnostics.CodeAnalysis.NotNull]
    public static System.Byte[] Encrypt(
        [System.Diagnostics.CodeAnalysis.NotNull] System.Byte[] key,
        [System.Diagnostics.CodeAnalysis.NotNull] System.Byte[] nonce,
        [System.Diagnostics.CodeAnalysis.NotNull] System.UInt32 counter,
        [System.Diagnostics.CodeAnalysis.NotNull] System.Byte[] input,
        [System.Diagnostics.CodeAnalysis.NotNull] SimdMode simdMode = SimdMode.AUTO_DETECT)
    {
        ChaCha20 chacha = new(key, nonce, counter);

        try
        {
            return chacha.EncryptBytes(input, simdMode);
        }
        finally
        {
            chacha.Clear();
        }
    }

    /// <summary>
    /// One-shot static API: decrypts <paramref name="input"/> using ChaCha20.
    /// (Identical to <see cref="Encrypt(System.Byte[], System.Byte[], System.UInt32, System.Byte[], SimdMode)"/>.)
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
    [return: System.Diagnostics.CodeAnalysis.NotNull]
    public static System.Byte[] Decrypt(
        [System.Diagnostics.CodeAnalysis.NotNull] System.Byte[] key,
        [System.Diagnostics.CodeAnalysis.NotNull] System.Byte[] nonce,
        [System.Diagnostics.CodeAnalysis.NotNull] System.UInt32 counter,
        [System.Diagnostics.CodeAnalysis.NotNull] System.Byte[] input,
        [System.Diagnostics.CodeAnalysis.NotNull] SimdMode simdMode = SimdMode.AUTO_DETECT)
    {
        ChaCha20 chacha = new(key, nonce, counter);

        try
        {
            return chacha.DecryptBytes(input, simdMode);
        }
        finally
        {
            chacha.Clear();
        }
    }

    #endregion Static One-Shot API

    /// <summary>
    /// Securely zeroes all sensitive key material and internal state.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Because <see langword="ref struct"/> cannot implement <see cref="System.IDisposable"/>,
    /// call this method explicitly (preferably in a <c>finally</c> block) when done.
    /// </para>
    /// <para>
    /// Uses <see cref="MemorySecurity.ZeroMemory(System.Span{System.Byte})"/>
    /// to guarantee the JIT will not elide the zeroing.
    /// </para>
    /// </remarks>
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
                nameof(ChaCha20),
                $"This {nameof(ChaCha20)} instance has been cleared.");
        }
    }

    #endregion Private — Guard

    #region Private — Initialization

    /// <summary>
    /// Reads a 32-bit little-endian unsigned integer from the given span at <paramref name="offset"/>.
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    private static System.UInt32 LoadLittleEndian32(
        System.ReadOnlySpan<System.Byte> source,
        System.Int32 offset)
    {
        return System.Buffers.Binary.BinaryPrimitives.ReadUInt32LittleEndian(
            source[offset..]);
    }

    /// <summary>
    /// Sets up the first 12 words of the state matrix (constants + key).
    /// </summary>
    /// <param name="key">A 32-byte key.</param>
    /// <exception cref="System.ArgumentException"><paramref name="key"/> is not 32 bytes.</exception>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
    private void InitializeKey(System.ReadOnlySpan<System.Byte> key)
    {
        if (key.Length != KeySize)
        {
            throw new System.ArgumentException(
                $"Key length must be {KeySize}. Actual: {key.Length}");
        }

        System.Span<System.UInt32> s = _state;

        // "expand 32-byte k" — four constant words (RFC 7539 Section 2.3)
        s[0] = 0x61707865;
        s[1] = 0x3320646e;
        s[2] = 0x79622d32;
        s[3] = 0x6b206574;

        // Words 4..11 — key material
        for (System.Int32 i = 0; i < 8; i++)
        {
            s[4 + i] = LoadLittleEndian32(key, i * 4);
        }
    }

    /// <summary>
    /// Sets up words 12..15 of the state matrix (counter + nonce).
    /// </summary>
    /// <param name="nonce">A 12-byte nonce.</param>
    /// <param name="counter">Initial block counter value.</param>
    /// <exception cref="System.ArgumentException"><paramref name="nonce"/> is not 12 bytes.</exception>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
    private void InitializeNonce(System.ReadOnlySpan<System.Byte> nonce, System.UInt32 counter)
    {
        if (nonce.Length != NonceSize)
        {
            throw new System.ArgumentException(
                $"Nonce length must be {NonceSize}. Actual: {nonce.Length}");
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

    /// <summary>
    /// Detects the best available SIMD width on the current hardware.
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    private static SimdMode DetectSimdMode()
    {
        return System.Runtime.Intrinsics.Vector512.IsHardwareAccelerated
            ? SimdMode.V512
            : System.Runtime.Intrinsics.Vector256.IsHardwareAccelerated
            ? SimdMode.V256
            : System.Runtime.Intrinsics.Vector128.IsHardwareAccelerated ? SimdMode.V128 : SimdMode.NONE;
    }

    #endregion Private — SIMD Detection

    #region Private — Core Block Function

    /// <summary>
    /// Executes one ChaCha20 block function: copies state into <paramref name="working"/>,
    /// applies 20 rounds, serializes the result to <paramref name="keystream"/>,
    /// and increments the counter in <paramref name="state"/>.
    /// </summary>
    /// <param name="state">The mutable state matrix (counter is advanced).</param>
    /// <param name="working">Scratch working buffer.</param>
    /// <param name="keystream">64-byte output buffer.</param>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
    private static void GenerateBlock(
        System.Span<System.UInt32> state,
        System.Span<System.UInt32> working,
        System.Span<System.Byte> keystream)
    {
        // Copy state → working
        state.CopyTo(working);

        // 20 rounds = 10 double-rounds
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

        // Add original state and serialize as little-endian bytes
        for (System.Int32 i = 0; i < StateLength; i++)
        {
            System.Buffers.Binary.BinaryPrimitives.WriteUInt32LittleEndian(
                keystream[(4 * i)..],
                BitwiseOperations.Add(working[i], state[i]));
        }

        // Advance the block counter
        state[12] = BitwiseOperations.AddOne(state[12]);

        if (state[12] <= 0)
        {
            // Counter overflow — increment the next word.
            // Stopping at 2^70 bytes per nonce is the caller's responsibility.
            state[13] = BitwiseOperations.AddOne(state[13]);
        }
    }

    /// <summary>
    /// The ChaCha20 Quarter Round operation (RFC 7539 Section 2.1).
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    private static void QuarterRound(
        System.Span<System.UInt32> x,
        System.Int32 a, System.Int32 b,
        System.Int32 c, System.Int32 d)
    {
        x[a] = BitwiseOperations.Add(x[a], x[b]);
        x[d] = System.Numerics.BitOperations.RotateLeft(
            BitwiseOperations.XOr(x[d], x[a]), 16);

        x[c] = BitwiseOperations.Add(x[c], x[d]);
        x[b] = System.Numerics.BitOperations.RotateLeft(
            BitwiseOperations.XOr(x[b], x[c]), 12);

        x[a] = BitwiseOperations.Add(x[a], x[b]);
        x[d] = System.Numerics.BitOperations.RotateLeft(
            BitwiseOperations.XOr(x[d], x[a]), 8);

        x[c] = BitwiseOperations.Add(x[c], x[d]);
        x[b] = System.Numerics.BitOperations.RotateLeft(
            BitwiseOperations.XOr(x[b], x[c]), 7);
    }

    #endregion Private — Core Block Function

    #region Private — Span-Based Encrypt Core

    /// <summary>
    /// Core span-based XOR encryption without SIMD branching.
    /// Used by <see cref="Encrypt(System.ReadOnlySpan{System.Byte}, System.Span{System.Byte})"/>
    /// and related span overloads.
    /// </summary>
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

    /// <summary>
    /// Core array-based XOR encryption with SIMD-accelerated block processing.
    /// </summary>
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

        // We need a temporary byte[] for SIMD Vector.Create(byte[], offset) overloads.
        // Copy from InlineArray → stackalloc is not possible with Vector.Create(byte[], int),
        // so we slice into a local pinned array for the SIMD path.
        //
        // For the SIMD path, we materialize the keystream into a small pooled/stack buffer.
        // NOTE: Vector512/256/128.Create overloads require byte[], so we rent a small array
        // only when SIMD is actually used.  For SimdMode.NONE, we use Span directly.
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
                    // Scalar XOR with small unroll
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
                    // Copy inline keystream to the rented array for SIMD vector creation
                    keystreamSpan.CopyTo(System.MemoryExtensions.AsSpan(keystreamArray, 0, BlockSize));

                    if (simdMode is SimdMode.V512)
                    {
                        // 1 × 64-byte operation
                        var inputV = System.Runtime.Intrinsics.Vector512.Create(input, offset);
                        var tmpV = System.Runtime.Intrinsics.Vector512.Create(keystreamArray!, 0);
                        var resultV = inputV ^ tmpV;
                        System.Runtime.Intrinsics.Vector512.CopyTo(resultV, output, offset);
                    }
                    else if (simdMode is SimdMode.V256)
                    {
                        // 2 × 32-byte operations
                        var inV0 = System.Runtime.Intrinsics.Vector256.Create(input, offset);
                        var tmpV0 = System.Runtime.Intrinsics.Vector256.Create(keystreamArray!, 0);
                        System.Runtime.Intrinsics.Vector256.CopyTo(inV0 ^ tmpV0, output, offset);

                        var inV1 = System.Runtime.Intrinsics.Vector256.Create(input, offset + 32);
                        var tmpV1 = System.Runtime.Intrinsics.Vector256.Create(keystreamArray!, 32);
                        System.Runtime.Intrinsics.Vector256.CopyTo(inV1 ^ tmpV1, output, offset + 32);
                    }
                    else // SimdMode.V128
                    {
                        // 4 × 16-byte operations
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

            // Handle remaining tail bytes (always scalar)
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