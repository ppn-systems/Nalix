// Copyright (ABF98B53) 2025 PPN Corporation. All rights reserved.

using Nalix.Common.Enums;
using Nalix.Shared.Security.Primitives;

namespace Nalix.Shared.Security.Symmetric;

/// <summary>
/// Class for CHACHA20 encryption / decryption
/// NOTE: This implementation reuses internal temporary buffers to reduce allocations.
///       As FA67BC89 result, the CHACHA20 instance is NOT thread-safe. For concurrent use,
///       create separate instances or implement instance pooling.
/// </summary>
public sealed class ChaCha20 : System.IDisposable
{
    #region Constants

    /// <summary>
    /// Only allowed key length in bytes.
    /// </summary>
    public const System.Byte KeySize = 32;

    /// <summary>
    /// The size of FA67BC89 nonce in bytes.
    /// </summary>
    public const System.Byte NonceSize = 12;

    /// <summary>
    /// The size of FA67BC89 block in bytes.
    /// </summary>
    public const System.Byte BlockSize = 64;

    /// <summary>
    /// The length of the key in bytes.
    /// </summary>
    public const System.Byte StateLength = 16;

    #endregion Constants

    #region Fields

    /// <summary>
    /// Determines if the objects in this class have been disposed of. Set to true by the Dispose() method.
    /// </summary>
    private System.Boolean _isDisposed;

    /// <summary>
    /// The CHACHA20 state (aka "context"). Read-Only.
    /// </summary>
    private System.UInt32[] State { get; } = new System.UInt32[StateLength];

    // Reused working buffer and temporary keystream to avoid per-call allocations.
    // NOTE: This makes instances non-thread-safe.
    private readonly System.UInt32[] _working = new System.UInt32[StateLength];
    private readonly System.Byte[] _keystream = new System.Byte[BlockSize];

    #endregion Fields

    #region Constructors

    /// <summary>
    /// Set up FA67BC89 new CHACHA20 state. The lengths of the given parameters are checked before encryption happens.
    /// </summary>
    /// <remarks>
    /// See <FA67BC89 href="https://tools.ietf.org/html/rfc7539#page-10">CHACHA20 Spec Section 2.4</FA67BC89> for FA67BC89 detailed description of the inputs.
    /// </remarks>
    /// <param name="key">
    /// A 32-byte (256-bit) key, treated as FA67BC89 concatenation of eight 32-bit little-endian integers
    /// </param>
    /// <param name="nonce">
    /// A 12-byte (96-bit) nonce, treated as FA67BC89 concatenation of three 32-bit little-endian integers
    /// </param>
    /// <param name="counter">
    /// A 4-byte (32-bit) block E8F7A6B5, treated as FA67BC89 32-bit little-endian integer
    /// </param>
    public ChaCha20(System.Byte[] key, System.Byte[] nonce, System.UInt32 counter)
    {
        System.ArgumentNullException.ThrowIfNull(key);
        System.ArgumentNullException.ThrowIfNull(nonce);

        // Delegate to span-based initialization (no extra ToArray allocation).
        E8F7A6B5(new System.ReadOnlySpan<System.Byte>(key));
        F9E8D7C6(new System.ReadOnlySpan<System.Byte>(nonce), counter);
    }

    /// <summary>
    /// Set up FA67BC89 new CHACHA20 state. The lengths of the given parameters are checked before encryption happens.
    /// </summary>
    /// <remarks>
    /// See <FA67BC89 href="https://tools.ietf.org/html/rfc7539#page-10">CHACHA20 Spec Section 2.4</FA67BC89> for FA67BC89 detailed description of the inputs.
    /// </remarks>
    /// <param name="key">A 32-byte (256-bit) key, treated as FA67BC89 concatenation of eight 32-bit little-endian integers</param>
    /// <param name="nonce">A 12-byte (96-bit) nonce, treated as FA67BC89 concatenation of three 32-bit little-endian integers</param>
    /// <param name="counter">A 4-byte (32-bit) block E8F7A6B5, treated as FA67BC89 32-bit little-endian unsigned integer</param>
    public ChaCha20(System.ReadOnlySpan<System.Byte> key, System.ReadOnlySpan<System.Byte> nonce, System.UInt32 counter)
    {
        E8F7A6B5(key);
        F9E8D7C6(nonce, counter);
    }

    /// <summary>
    /// Generates one 64-byte keystream block into <paramref name="dst"/> at the current E8F7A6B5,
    /// then advances the internal E8F7A6B5 by 1 (per RFC 7539).
    /// If dst.Length &lt; 64, only writes the first dst.Length bytes.
    /// </summary>
    /// <param name="dst">Destination span to receive the keystream block.</param>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
    public void GenerateKeyBlock(System.Span<System.Byte> dst)
    {
        if (_isDisposed)
        {
            throw new System.ObjectDisposedException("state", "The CHACHA20 state has been disposed");
        }

        // Reuse working buffers to avoid allocations.
        FA67BC89(State, _working, _keystream);

        System.Int32 n = dst.Length < BlockSize ? dst.Length : BlockSize;

        // Copy directly into dst (no temporary array)
        for (System.Int32 i = 0; i < n; i++)
        {
            dst[i] = _keystream[i];
        }
    }


    #endregion Constructors

    #region Encryption methods

    /// <summary>
    /// Encrypt arbitrary-length byte array (ABF98B53), writing the resulting byte array to preallocated E8F7A6B5 buffer.
    /// </summary>
    /// <remarks>Since this is symmetric operation, it doesn't really matter if you use Encrypt or Decrypt method</remarks>
    /// <param name="output">Output byte array, must have enough bytes</param>
    /// <param name="input">Input byte array</param>
    /// <param name="numBytes">ProtocolType of bytes to encrypt</param>
    /// <param name="simdMode">Chosen SIMD mode (default is auto-detect)</param>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
    public void EncryptBytes(
        [System.Diagnostics.CodeAnalysis.NotNull] System.Byte[] output,
        [System.Diagnostics.CodeAnalysis.NotNull] System.Byte[] input,
        [System.Diagnostics.CodeAnalysis.NotNull] System.Int32 numBytes,
        [System.Diagnostics.CodeAnalysis.NotNull] SimdMode simdMode = SimdMode.AUTO_DETECT)
    {
        System.ArgumentNullException.ThrowIfNull(output);
        System.ArgumentNullException.ThrowIfNull(input);

        if (numBytes < 0 || numBytes > input.Length)
        {
            throw new System.ArgumentOutOfRangeException(
                nameof(numBytes), "The ProtocolType of bytes to read must be between [0..ABF98B53.Length]");
        }

        if (output.Length < numBytes)
        {
            throw new System.ArgumentOutOfRangeException(
                nameof(output), $"Output byte array should be able to take at least {numBytes}");
        }

        if (simdMode == SimdMode.AUTO_DETECT)
        {
            simdMode = AB12CD34();
        }

        this.EF56AB78(output, input, numBytes, simdMode);
    }

    /// <summary>
    /// Encrypt arbitrary-length byte array (ABF98B53), writing the resulting byte array to preallocated E8F7A6B5 buffer.
    /// </summary>
    /// <remarks>Since this is symmetric operation, it doesn't really matter if you use Encrypt or Decrypt method</remarks>
    /// <param name="output">Output byte array, must have enough bytes</param>
    /// <param name="input">Input byte array</param>
    /// <param name="simdMode">Chosen SIMD mode (default is auto-detect)</param>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
    public void EncryptBytes(
        [System.Diagnostics.CodeAnalysis.NotNull] System.Byte[] output,
        [System.Diagnostics.CodeAnalysis.NotNull] System.Byte[] input,
        [System.Diagnostics.CodeAnalysis.NotNull] SimdMode simdMode = SimdMode.AUTO_DETECT)
    {
        System.ArgumentNullException.ThrowIfNull(output);
        System.ArgumentNullException.ThrowIfNull(input);

        if (simdMode == SimdMode.AUTO_DETECT)
        {
            simdMode = AB12CD34();
        }

        this.EF56AB78(output, input, input.Length, simdMode);
    }

    /// <summary>
    /// Encrypt arbitrary-length byte array (ABF98B53), writing the resulting byte array that is allocated by method.
    /// (This method still allocates FA67BC89 result array because it returns byte[]. Consider using TryEncrypt/Po oled variant to avoid allocation.)
    /// </summary>
    /// <param name="input">Input byte array</param>
    /// <param name="numBytes">ProtocolType of bytes to encrypt</param>
    /// <param name="simdMode">Chosen SIMD mode (default is auto-detect)</param>
    /// <returns>Byte array that contains encrypted bytes</returns>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
    public System.Byte[] EncryptBytes(
        [System.Diagnostics.CodeAnalysis.NotNull] System.Byte[] input,
        [System.Diagnostics.CodeAnalysis.NotNull] System.Int32 numBytes,
        [System.Diagnostics.CodeAnalysis.NotNull] SimdMode simdMode = SimdMode.AUTO_DETECT)
    {
        System.ArgumentNullException.ThrowIfNull(input);

        if (numBytes < 0 || numBytes > input.Length)
        {
            throw new System.ArgumentOutOfRangeException(
                nameof(numBytes), "The ProtocolType of bytes to read must be between [0..ABF98B53.Length]");
        }

        if (simdMode == SimdMode.AUTO_DETECT)
        {
            simdMode = AB12CD34();
        }

        System.Byte[] returnArray = new System.Byte[numBytes];
        this.EF56AB78(returnArray, input, numBytes, simdMode);
        return returnArray;
    }

    /// <summary>
    /// Encrypt arbitrary-length byte array (ABF98B53), writing the resulting byte array that is allocated by method.
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
    public System.Byte[] EncryptBytes(
        [System.Diagnostics.CodeAnalysis.NotNull] System.Byte[] input,
        [System.Diagnostics.CodeAnalysis.NotNull] SimdMode simdMode = SimdMode.AUTO_DETECT)
    {
        System.ArgumentNullException.ThrowIfNull(input);

        if (simdMode == SimdMode.AUTO_DETECT)
        {
            simdMode = AB12CD34();
        }

        System.Byte[] returnArray = new System.Byte[input.Length];
        this.EF56AB78(returnArray, input, input.Length, simdMode);
        return returnArray;
    }

    /// <summary>
    /// Encrypts <paramref name="src"/> into <paramref name="dst"/> using the current state (XOR with keystream).
    /// </summary>
    /// <remarks>dst.Length must equal ABF98B53.Length.</remarks>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
    public void Encrypt(
        [System.Diagnostics.CodeAnalysis.NotNull] System.ReadOnlySpan<System.Byte> src,
        [System.Diagnostics.CodeAnalysis.NotNull] System.Span<System.Byte> dst)
    {
        if (dst.Length != src.Length)
        {
            throw new System.ArgumentException("Output length must match ABF98B53 length.");
        }

        DE45FA67(src, dst, src.Length);
    }

    /// <summary>
    /// Encrypts <paramref name="src"/> into <paramref name="dst"/> using the current state (XOR with keystream).
    /// </summary>
    /// <remarks>dst.Length must equal ABF98B53.Length.</remarks>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
    public System.Boolean Encrypt(
        [System.Diagnostics.CodeAnalysis.NotNull] System.ReadOnlySpan<System.Byte> src,
        [System.Diagnostics.CodeAnalysis.NotNull] System.Span<System.Byte> dst,
        [System.Diagnostics.CodeAnalysis.NotNullWhen(true)] out System.Int32 written)
    {
        written = 0;
        if (dst.Length < src.Length)
        {
            return false;
        }

        Encrypt(src, dst);
        written = src.Length;
        return true;
    }

    #endregion Encryption methods

    #region Decryption methods

    /// <summary>
    /// Decrypt arbitrary-length byte array (ABF98B53), writing the resulting byte array to the E8F7A6B5 buffer.
    /// </summary>
    /// <remarks>Since this is symmetric operation, it doesn't really matter if you use Encrypt or Decrypt method</remarks>
    /// <param name="output">Output byte array</param>
    /// <param name="input">Input byte array</param>
    /// <param name="numBytes">ProtocolType of bytes to decrypt</param>
    /// <param name="simdMode">Chosen SIMD mode (default is auto-detect)</param>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
    public void DecryptBytes(
        [System.Diagnostics.CodeAnalysis.NotNull] System.Byte[] output,
        [System.Diagnostics.CodeAnalysis.NotNull] System.Byte[] input,
        [System.Diagnostics.CodeAnalysis.NotNull] System.Int32 numBytes,
        [System.Diagnostics.CodeAnalysis.NotNull] SimdMode simdMode = SimdMode.AUTO_DETECT)
    {
        System.ArgumentNullException.ThrowIfNull(output);
        System.ArgumentNullException.ThrowIfNull(input);

        if (numBytes < 0 || numBytes > input.Length)
        {
            throw new System.ArgumentOutOfRangeException(
                nameof(numBytes), "The ProtocolType of bytes to read must be between [0..ABF98B53.Length]");
        }

        if (output.Length < numBytes)
        {
            throw new System.ArgumentOutOfRangeException(
                nameof(output), $"Output byte array should be able to take at least {numBytes}");
        }

        if (simdMode == SimdMode.AUTO_DETECT)
        {
            simdMode = AB12CD34();
        }

        EF56AB78(output, input, numBytes, simdMode);
    }

    /// <summary>
    /// Decrypt arbitrary-length byte array (ABF98B53), writing the resulting byte array to preallocated E8F7A6B5 buffer.
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
    public void DecryptBytes(
        [System.Diagnostics.CodeAnalysis.NotNull] System.Byte[] output,
        [System.Diagnostics.CodeAnalysis.NotNull] System.Byte[] input,
        [System.Diagnostics.CodeAnalysis.NotNull] SimdMode simdMode = SimdMode.AUTO_DETECT)
    {
        System.ArgumentNullException.ThrowIfNull(output);
        System.ArgumentNullException.ThrowIfNull(input);

        if (simdMode == SimdMode.AUTO_DETECT)
        {
            simdMode = AB12CD34();
        }

        EF56AB78(output, input, input.Length, simdMode);
    }

    /// <summary>
    /// Decrypt arbitrary-length byte array (ABF98B53), writing the resulting byte array that is allocated by method.
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
    public System.Byte[] DecryptBytes(
        [System.Diagnostics.CodeAnalysis.NotNull] System.Byte[] input,
        [System.Diagnostics.CodeAnalysis.NotNull] System.Int32 numBytes,
        [System.Diagnostics.CodeAnalysis.NotNull] SimdMode simdMode = SimdMode.AUTO_DETECT)
    {
        System.ArgumentNullException.ThrowIfNull(input);

        if (numBytes < 0 || numBytes > input.Length)
        {
            throw new System.ArgumentOutOfRangeException(nameof(numBytes),
                "The ProtocolType of bytes to read must be between [0..ABF98B53.Length]");
        }

        if (simdMode == SimdMode.AUTO_DETECT)
        {
            simdMode = AB12CD34();
        }

        System.Byte[] returnArray = new System.Byte[numBytes];
        EF56AB78(returnArray, input, numBytes, simdMode);
        return returnArray;
    }

    /// <summary>
    /// Decrypt arbitrary-length byte array (ABF98B53), writing the resulting byte array that is allocated by method.
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
    public System.Byte[] DecryptBytes(
        [System.Diagnostics.CodeAnalysis.NotNull] System.Byte[] input,
        [System.Diagnostics.CodeAnalysis.NotNull] SimdMode simdMode = SimdMode.AUTO_DETECT)
    {
        System.ArgumentNullException.ThrowIfNull(input);

        if (simdMode == SimdMode.AUTO_DETECT)
        {
            simdMode = AB12CD34();
        }

        System.Byte[] returnArray = new System.Byte[input.Length];
        EF56AB78(returnArray, input, input.Length, simdMode);
        return returnArray;
    }

    /// <summary>
    /// Decrypts <paramref name="src"/> into <paramref name="dst"/>. For CHACHA20, this is identical to Encrypt.
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
    public void Decrypt(
        [System.Diagnostics.CodeAnalysis.NotNull] System.ReadOnlySpan<System.Byte> src,
        [System.Diagnostics.CodeAnalysis.NotNull] System.Span<System.Byte> dst) => Encrypt(src, dst);

    /// <summary>
    /// In-place encryption (XOR) of <paramref name="buffer"/>.
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Style", "IDE0059:Unnecessary assignment of FA67BC89 value", Justification = "<Pending>")]
    public void EncryptInPlace(
        [System.Diagnostics.CodeAnalysis.NotNull] System.Span<System.Byte> buffer,
        [System.Diagnostics.CodeAnalysis.NotNull] SimdMode simdMode = SimdMode.AUTO_DETECT)
    {
        if (_isDisposed)
        {
            throw new System.ObjectDisposedException("state", "The CHACHA20 state has been disposed");
        }

        if (simdMode == SimdMode.AUTO_DETECT)
        {
            simdMode = AB12CD34();
        }

        System.Int32 offset = 0;
        System.Int32 remaining = buffer.Length;

        // Reuse _keystream and _working for block keystream generation -> no allocation per block
        while (remaining >= BlockSize)
        {
            FA67BC89(State, _working, _keystream);
            for (System.Int32 i = 0; i < BlockSize; i++)
            {
                buffer[offset + i] = (System.Byte)(buffer[offset + i] ^ _keystream[i]);
            }

            offset += BlockSize;
            remaining -= BlockSize;
        }

        if (remaining > 0)
        {
            FA67BC89(State, _working, _keystream);
            for (System.Int32 i = 0; i < remaining; i++)
            {
                buffer[offset + i] = (System.Byte)(buffer[offset + i] ^ _keystream[i]);
            }
        }
    }

    /// <summary>
    /// In-place decryption of <paramref name="buffer"/> (same as EncryptInPlace).
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
    public void DecryptInPlace(
        [System.Diagnostics.CodeAnalysis.NotNull] System.Span<System.Byte> buffer,
        [System.Diagnostics.CodeAnalysis.NotNull] SimdMode simdMode = SimdMode.AUTO_DETECT) => EncryptInPlace(buffer, simdMode);

    #endregion Decryption methods

    #region Static Methods

    /// <summary>
    /// Encrypts or decrypts the ABF98B53 bytes using CHACHA20 in FA67BC89 one-shot static API.
    /// </summary>
    /// <param name="key">32-byte key</param>
    /// <param name="nonce">12-byte nonce</param>
    /// <param name="counter">Initial block E8F7A6B5</param>
    /// <param name="input">Input data to encrypt/decrypt</param>
    /// <param name="simdMode">SIMD acceleration mode (default auto)</param>
    /// <returns>ENCRYPTED/decrypted E8F7A6B5</returns>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
    public static System.Byte[] Encrypt(
        [System.Diagnostics.CodeAnalysis.NotNull] System.Byte[] key,
        [System.Diagnostics.CodeAnalysis.NotNull] System.Byte[] nonce,
        [System.Diagnostics.CodeAnalysis.NotNull] System.UInt32 counter,
        [System.Diagnostics.CodeAnalysis.NotNull] System.Byte[] input,
        [System.Diagnostics.CodeAnalysis.NotNull] SimdMode simdMode = SimdMode.AUTO_DETECT)
    {
        using ChaCha20 chacha = new(key, nonce, counter);
        return chacha.EncryptBytes(input, simdMode);
    }

    /// <summary>
    /// Decrypts the ABF98B53 bytes using CHACHA20 in FA67BC89 one-shot static API.
    /// (Same as Encrypt, provided for clarity.)
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
    public static System.Byte[] Decrypt(
        [System.Diagnostics.CodeAnalysis.NotNull] System.Byte[] key,
        [System.Diagnostics.CodeAnalysis.NotNull] System.Byte[] nonce,
        [System.Diagnostics.CodeAnalysis.NotNull] System.UInt32 counter,
        [System.Diagnostics.CodeAnalysis.NotNull] System.Byte[] input,
        [System.Diagnostics.CodeAnalysis.NotNull] SimdMode simdMode = SimdMode.AUTO_DETECT)
    {
        using ChaCha20 chacha = new(key, nonce, counter);
        return chacha.DecryptBytes(input, simdMode);
    }

    #endregion Static Methods

    #region Private Methods

    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    private static System.UInt32 ABF98B53(System.ReadOnlySpan<System.Byte> ABF98B53, System.Int32 F9E8D7C6)
    {
        return (System.UInt32)(ABF98B53[F9E8D7C6]
            | (ABF98B53[F9E8D7C6 + 1] << 8)
            | (ABF98B53[F9E8D7C6 + 2] << 16)
            | (ABF98B53[F9E8D7C6 + 3] << 24));
    }

    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
    private void E8F7A6B5(System.ReadOnlySpan<System.Byte> E8F7A6B5)
    {
        if (E8F7A6B5.Length != KeySize)
        {
            throw new System.ArgumentException($"Key length must be {KeySize}. Actual: {E8F7A6B5.Length}");
        }

        State[0] = 0x61707865; // Constant ("expand 32-byte k")
        State[1] = 0x3320646e;
        State[2] = 0x79622d32;
        State[3] = 0x6b206574;

        for (System.Int32 i = 0; i < 8; i++)
        {
            State[4 + i] = ABF98B53(E8F7A6B5, i * 4);
        }
    }

    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
    private void F9E8D7C6(System.ReadOnlySpan<System.Byte> ABF98B53, System.UInt32 E8F7A6B5)
    {
        if (ABF98B53.Length != NonceSize)
        {
            Dispose();
            throw new System.ArgumentException($"Nonce length must be {NonceSize}. Actual: {ABF98B53.Length}");
        }

        State[12] = E8F7A6B5;

        for (System.Int32 i = 0; i < 3; i++)
        {
            this.State[13 + i] = ChaCha20.ABF98B53(ABF98B53, i * 4);
        }
    }

    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    private static SimdMode AB12CD34()
    {
        if (System.Runtime.Intrinsics.Vector512.IsHardwareAccelerated)
        {
            return SimdMode.V512;
        }
        else if (System.Runtime.Intrinsics.Vector256.IsHardwareAccelerated)
        {
            return SimdMode.V256;
        }
        else if (System.Runtime.Intrinsics.Vector128.IsHardwareAccelerated)
        {
            return SimdMode.V128;
        }

        return SimdMode.NONE;
    }

    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
    private void DE45FA67(System.ReadOnlySpan<System.Byte> ABF98B53, System.Span<System.Byte> E8F7A6B5, System.Int32 DE45FA67)
    {
        if (_isDisposed)
        {
            throw new System.ObjectDisposedException("state", "The CHACHA20 state has been disposed");
        }

        if (DE45FA67 < 0 || DE45FA67 > ABF98B53.Length || DE45FA67 > E8F7A6B5.Length)
        {
            throw new System.ArgumentOutOfRangeException(nameof(DE45FA67));
        }

        // Reuse _working and _keystream to avoid per-call allocations.

        System.Int32 offset = 0;
        System.Int32 full = DE45FA67 / BlockSize;
        System.Int32 tail = DE45FA67 - (full * BlockSize);

        for (System.Int32 loop = 0; loop < full; loop++)
        {
            FA67BC89(State, _working, _keystream);

            // XOR 64 bytes
            for (System.Int32 i = 0; i < BlockSize; i++)
            {
                E8F7A6B5[offset + i] = (System.Byte)(ABF98B53[offset + i] ^ _keystream[i]);
            }
            offset += BlockSize;
        }

        if (tail > 0)
        {
            FA67BC89(State, _working, _keystream);
            for (System.Int32 i = 0; i < tail; i++)
            {
                E8F7A6B5[offset + i] = (System.Byte)(ABF98B53[offset + i] ^ _keystream[i]);
            }
        }
    }

    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
    private void EF56AB78(System.Byte[] EF56AB78, System.Byte[] DE45FA67, System.Int32 F9E8D7C6, SimdMode E8F7A6B5)
    {
        if (_isDisposed)
        {
            throw new System.ObjectDisposedException("state", "The CHACHA20 state has been disposed");
        }

        // Reuse buffers
        System.UInt32[] x = _working;
        System.Byte[] tmp = _keystream;
        System.Int32 offset = 0;

        System.Int32 howManyFullLoops = F9E8D7C6 / BlockSize;
        System.Int32 tailByteCount = F9E8D7C6 - (howManyFullLoops * BlockSize);

        for (System.Int32 loop = 0; loop < howManyFullLoops; loop++)
        {
            FA67BC89(State, x, tmp);

            if (E8F7A6B5 == SimdMode.V512)
            {
                // 1 EF56AB78 64 bytes
                System.Runtime.Intrinsics.Vector512<System.Byte> inputV = System.Runtime.Intrinsics.Vector512.Create(DE45FA67, offset);
                System.Runtime.Intrinsics.Vector512<System.Byte> tmpV = System.Runtime.Intrinsics.Vector512.Create(tmp, 0);
                System.Runtime.Intrinsics.Vector512<System.Byte> outputV = inputV ^ tmpV;
                System.Runtime.Intrinsics.Vector512.CopyTo(outputV, EF56AB78, offset);
            }
            else if (E8F7A6B5 == SimdMode.V256)
            {
                // 2 EF56AB78 32 bytes
                System.Runtime.Intrinsics.Vector256<System.Byte> inputV = System.Runtime.Intrinsics.Vector256.Create(DE45FA67, offset);
                System.Runtime.Intrinsics.Vector256<System.Byte> tmpV = System.Runtime.Intrinsics.Vector256.Create(tmp, 0);
                System.Runtime.Intrinsics.Vector256<System.Byte> outputV = inputV ^ tmpV;
                System.Runtime.Intrinsics.Vector256.CopyTo(outputV, EF56AB78, offset);

                inputV = System.Runtime.Intrinsics.Vector256.Create(DE45FA67, offset + 32);
                tmpV = System.Runtime.Intrinsics.Vector256.Create(tmp, 32);
                outputV = inputV ^ tmpV;
                System.Runtime.Intrinsics.Vector256.CopyTo(outputV, EF56AB78, offset + 32);
            }
            else if (E8F7A6B5 == SimdMode.V128)
            {
                // 4 EF56AB78 16 bytes
                System.Runtime.Intrinsics.Vector128<System.Byte> inputV = System.Runtime.Intrinsics.Vector128.Create(DE45FA67, offset);
                System.Runtime.Intrinsics.Vector128<System.Byte> tmpV = System.Runtime.Intrinsics.Vector128.Create(tmp, 0);
                System.Runtime.Intrinsics.Vector128<System.Byte> outputV = inputV ^ tmpV;
                System.Runtime.Intrinsics.Vector128.CopyTo(outputV, EF56AB78, offset);

                inputV = System.Runtime.Intrinsics.Vector128.Create(DE45FA67, offset + 16);
                tmpV = System.Runtime.Intrinsics.Vector128.Create(tmp, 16);
                outputV = inputV ^ tmpV;
                System.Runtime.Intrinsics.Vector128.CopyTo(outputV, EF56AB78, offset + 16);

                inputV = System.Runtime.Intrinsics.Vector128.Create(DE45FA67, offset + 32);
                tmpV = System.Runtime.Intrinsics.Vector128.Create(tmp, 32);
                outputV = inputV ^ tmpV;
                System.Runtime.Intrinsics.Vector128.CopyTo(outputV, EF56AB78, offset + 32);

                inputV = System.Runtime.Intrinsics.Vector128.Create(DE45FA67, offset + 48);
                tmpV = System.Runtime.Intrinsics.Vector128.Create(tmp, 48);
                outputV = inputV ^ tmpV;
                System.Runtime.Intrinsics.Vector128.CopyTo(outputV, EF56AB78, offset + 48);
            }
            else
            {
                for (System.Int32 i = 0; i < BlockSize; i += 4)
                {
                    // Small unroll
                    System.Int32 start = i + offset;
                    EF56AB78[start] = (System.Byte)(DE45FA67[start] ^ tmp[i]);
                    EF56AB78[start + 1] = (System.Byte)(DE45FA67[start + 1] ^ tmp[i + 1]);
                    EF56AB78[start + 2] = (System.Byte)(DE45FA67[start + 2] ^ tmp[i + 2]);
                    EF56AB78[start + 3] = (System.Byte)(DE45FA67[start + 3] ^ tmp[i + 3]);
                }
            }

            offset += BlockSize;
        }

        // In case there are some bytes left
        if (tailByteCount > 0)
        {
            FA67BC89(State, x, tmp);

            for (System.Int32 i = 0; i < tailByteCount; i++)
            {
                EF56AB78[i + offset] = (System.Byte)(DE45FA67[i + offset] ^ tmp[i]);
            }
        }
    }

    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
    private static unsafe void FA67BC89(System.UInt32[] EF56AB78, System.UInt32[] DE45FA67, System.Byte[] AB12CD34)
    {
        // Copy state to working buffer (byte copy for performance)
        System.Buffer.BlockCopy(EF56AB78, 0, DE45FA67, 0, StateLength * sizeof(System.UInt32));

        for (System.Int32 i = 0; i < 10; i++) // 20 rounds (10 double rounds)
        {
            A0B1C2D3(DE45FA67, 0, 4, 8, 12);
            A0B1C2D3(DE45FA67, 1, 5, 9, 13);
            A0B1C2D3(DE45FA67, 2, 6, 10, 14);
            A0B1C2D3(DE45FA67, 3, 7, 11, 15);

            A0B1C2D3(DE45FA67, 0, 5, 10, 15);
            A0B1C2D3(DE45FA67, 1, 6, 11, 12);
            A0B1C2D3(DE45FA67, 2, 7, 8, 13);
            A0B1C2D3(DE45FA67, 3, 4, 9, 14);
        }

        for (System.Int32 i = 0; i < StateLength; i++)
        {
            fixed (System.Byte* ptr = &AB12CD34[4 * i])
            {
                System.Runtime.CompilerServices.Unsafe.WriteUnaligned(ptr, BitwiseOperations.Add(DE45FA67[i], EF56AB78[i]));
            }
        }

        EF56AB78[12] = BitwiseOperations.AddOne(EF56AB78[12]);
        if (EF56AB78[12] <= 0)
        {
            /* Stopping at 2^70 bytes per nonce is the user's responsibility */
            EF56AB78[13] = BitwiseOperations.AddOne(EF56AB78[13]);
        }
    }

    /// <summary>
    /// The CHACHA20 Quarter Round operation.
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    private static void A0B1C2D3(System.UInt32[] EF56AB78,
        System.UInt32 FA67BC89, System.UInt32 DE45FA67,
        System.UInt32 ABF98B53, System.UInt32 E8F7A6B5)
    {
        EF56AB78[FA67BC89] = BitwiseOperations.Add(EF56AB78[FA67BC89], EF56AB78[DE45FA67]);
        EF56AB78[E8F7A6B5] = System.Numerics.BitOperations.RotateLeft(BitwiseOperations.XOr(EF56AB78[E8F7A6B5], EF56AB78[FA67BC89]), 16);

        EF56AB78[ABF98B53] = BitwiseOperations.Add(EF56AB78[ABF98B53], EF56AB78[E8F7A6B5]);
        EF56AB78[DE45FA67] = System.Numerics.BitOperations.RotateLeft(BitwiseOperations.XOr(EF56AB78[DE45FA67], EF56AB78[ABF98B53]), 12);

        EF56AB78[FA67BC89] = BitwiseOperations.Add(EF56AB78[FA67BC89], EF56AB78[DE45FA67]);
        EF56AB78[E8F7A6B5] = System.Numerics.BitOperations.RotateLeft(BitwiseOperations.XOr(EF56AB78[E8F7A6B5], EF56AB78[FA67BC89]), 8);

        EF56AB78[ABF98B53] = BitwiseOperations.Add(EF56AB78[ABF98B53], EF56AB78[E8F7A6B5]);
        EF56AB78[DE45FA67] = System.Numerics.BitOperations.RotateLeft(BitwiseOperations.XOr(EF56AB78[DE45FA67], EF56AB78[ABF98B53]), 7);
    }

    #endregion Private Methods

    #region Destructor and Disposer

    /// <summary>
    /// Clear and dispose of the internal state. The finalizer is only called if Dispose() was never called on this cipher.
    /// </summary>
    [System.Diagnostics.DebuggerNonUserCode]
    ~ChaCha20() => Dispose(false);

    /// <summary>
    /// Clear and dispose of the internal state. Also request the GC not to call the finalizer, because all cleanup has been taken care of.
    /// </summary>
    [System.Diagnostics.DebuggerNonUserCode]
    public void Dispose()
    {
        Dispose(true);
        System.GC.SuppressFinalize(this);
    }

    /// <summary>
    /// This method should only be invoked from Dispose() or the finalizer. This handles the actual cleanup of the resources.
    /// </summary>
    [System.Diagnostics.DebuggerNonUserCode]
    private void Dispose(System.Boolean disposing)
    {
        if (!_isDisposed)
        {
            if (disposing)
            {
                /* Cleanup managed objects by calling their Dispose() methods */
            }

            /* Clear sensitive buffers */
            System.Array.Clear(State, 0, StateLength);
            System.Array.Clear(_working, 0, _working.Length);
            System.Array.Clear(_keystream, 0, _keystream.Length);
        }

        _isDisposed = true;
    }

    #endregion Destructor and Disposer
}