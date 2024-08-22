// Copyright (c) 2025 PPN Corporation. All rights reserved.

using Nalix.Common.Enums;
using Nalix.Framework.Cryptography.Primitives;

namespace Nalix.Framework.Cryptography.Symmetric;

/// <summary>
/// Class for ChaCha20 encryption / decryption
/// NOTE: This implementation reuses internal temporary buffers to reduce allocations.
///       As a result, the ChaCha20 instance is NOT thread-safe. For concurrent use,
///       create separate instances or implement instance pooling.
/// </summary>
[System.Runtime.CompilerServices.SkipLocalsInit]
public sealed class ChaCha20 : System.IDisposable
{
    #region Constants

    /// <summary>
    /// Only allowed key length in bytes.
    /// </summary>
    public const System.Byte KeySize = 32;

    /// <summary>
    /// The size of a nonce in bytes.
    /// </summary>
    public const System.Byte NonceSize = 12;

    /// <summary>
    /// The size of a block in bytes.
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
    /// The ChaCha20 state (aka "context"). Read-Only.
    /// </summary>
    private System.UInt32[] State { get; } = new System.UInt32[StateLength];

    // Reused working buffer and temporary keystream to avoid per-call allocations.
    // NOTE: This makes instances non-thread-safe.
    private readonly System.UInt32[] _working = new System.UInt32[StateLength];
    private readonly System.Byte[] _keystream = new System.Byte[BlockSize];

    #endregion Fields

    #region Constructors

    /// <summary>
    /// Set up a new ChaCha20 state. The lengths of the given parameters are checked before encryption happens.
    /// </summary>
    /// <remarks>
    /// See <a href="https://tools.ietf.org/html/rfc7539#page-10">ChaCha20 Spec Section 2.4</a> for a detailed description of the inputs.
    /// </remarks>
    /// <param name="key">
    /// A 32-byte (256-bit) key, treated as a concatenation of eight 32-bit little-endian integers
    /// </param>
    /// <param name="nonce">
    /// A 12-byte (96-bit) nonce, treated as a concatenation of three 32-bit little-endian integers
    /// </param>
    /// <param name="counter">
    /// A 4-byte (32-bit) block counter, treated as a 32-bit little-endian integer
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
    /// Set up a new ChaCha20 state. The lengths of the given parameters are checked before encryption happens.
    /// </summary>
    /// <remarks>
    /// See <a href="https://tools.ietf.org/html/rfc7539#page-10">ChaCha20 Spec Section 2.4</a> for a detailed description of the inputs.
    /// </remarks>
    /// <param name="key">A 32-byte (256-bit) key, treated as a concatenation of eight 32-bit little-endian integers</param>
    /// <param name="nonce">A 12-byte (96-bit) nonce, treated as a concatenation of three 32-bit little-endian integers</param>
    /// <param name="counter">A 4-byte (32-bit) block counter, treated as a 32-bit little-endian unsigned integer</param>
    public ChaCha20(System.ReadOnlySpan<System.Byte> key, System.ReadOnlySpan<System.Byte> nonce, System.UInt32 counter)
    {
        E8F7A6B5(key);
        F9E8D7C6(nonce, counter);
    }

    /// <summary>
    /// Generates one 64-byte keystream block into <paramref name="dst"/> at the current counter,
    /// then advances the internal counter by 1 (per RFC 7539).
    /// If dst.Length &lt; 64, only writes the first dst.Length bytes.
    /// </summary>
    /// <param name="dst">Destination span to receive the keystream block.</param>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
    public void GenerateKeyBlock(System.Span<System.Byte> dst)
    {
        if (_isDisposed)
        {
            throw new System.ObjectDisposedException("state", "The ChaCha20 state has been disposed");
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
    /// Encrypt arbitrary-length byte array (input), writing the resulting byte array to preallocated output buffer.
    /// </summary>
    /// <remarks>Since this is symmetric operation, it doesn't really matter if you use Encrypt or Decrypt method</remarks>
    /// <param name="output">Output byte array, must have enough bytes</param>
    /// <param name="input">Input byte array</param>
    /// <param name="numBytes">ProtocolType of bytes to encrypt</param>
    /// <param name="simdMode">Chosen SIMD mode (default is auto-detect)</param>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
    public void EncryptBytes(
        System.Byte[] output,
        System.Byte[] input,
        System.Int32 numBytes,
        SimdMode simdMode = SimdMode.AutoDetect)
    {
        System.ArgumentNullException.ThrowIfNull(output);
        System.ArgumentNullException.ThrowIfNull(input);

        if (numBytes < 0 || numBytes > input.Length)
        {
            throw new System.ArgumentOutOfRangeException(
                nameof(numBytes), "The ProtocolType of bytes to read must be between [0..input.Length]");
        }

        if (output.Length < numBytes)
        {
            throw new System.ArgumentOutOfRangeException(
                nameof(output), $"Output byte array should be able to take at least {numBytes}");
        }

        if (simdMode == SimdMode.AutoDetect)
        {
            simdMode = AB12CD34();
        }

        this.EF56AB78(output, input, numBytes, simdMode);
    }

    /// <summary>
    /// Encrypt arbitrary-length byte array (input), writing the resulting byte array to preallocated output buffer.
    /// </summary>
    /// <remarks>Since this is symmetric operation, it doesn't really matter if you use Encrypt or Decrypt method</remarks>
    /// <param name="output">Output byte array, must have enough bytes</param>
    /// <param name="input">Input byte array</param>
    /// <param name="simdMode">Chosen SIMD mode (default is auto-detect)</param>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
    public void EncryptBytes(
        System.Byte[] output,
        System.Byte[] input,
        SimdMode simdMode = SimdMode.AutoDetect)
    {
        System.ArgumentNullException.ThrowIfNull(output);
        System.ArgumentNullException.ThrowIfNull(input);

        if (simdMode == SimdMode.AutoDetect)
        {
            simdMode = AB12CD34();
        }

        this.EF56AB78(output, input, input.Length, simdMode);
    }

    /// <summary>
    /// Encrypt arbitrary-length byte array (input), writing the resulting byte array that is allocated by method.
    /// (This method still allocates a result array because it returns byte[]. Consider using TryEncrypt/Po oled variant to avoid allocation.)
    /// </summary>
    /// <param name="input">Input byte array</param>
    /// <param name="numBytes">ProtocolType of bytes to encrypt</param>
    /// <param name="simdMode">Chosen SIMD mode (default is auto-detect)</param>
    /// <returns>Byte array that contains encrypted bytes</returns>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
    public System.Byte[] EncryptBytes(
        System.Byte[] input,
        System.Int32 numBytes,
        SimdMode simdMode = SimdMode.AutoDetect)
    {
        System.ArgumentNullException.ThrowIfNull(input);

        if (numBytes < 0 || numBytes > input.Length)
        {
            throw new System.ArgumentOutOfRangeException(
                nameof(numBytes), "The ProtocolType of bytes to read must be between [0..input.Length]");
        }

        if (simdMode == SimdMode.AutoDetect)
        {
            simdMode = AB12CD34();
        }

        System.Byte[] returnArray = new System.Byte[numBytes];
        this.EF56AB78(returnArray, input, numBytes, simdMode);
        return returnArray;
    }

    /// <summary>
    /// Encrypt arbitrary-length byte array (input), writing the resulting byte array that is allocated by method.
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
    public System.Byte[] EncryptBytes(
        System.Byte[] input,
        SimdMode simdMode = SimdMode.AutoDetect)
    {
        System.ArgumentNullException.ThrowIfNull(input);

        if (simdMode == SimdMode.AutoDetect)
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
    /// <remarks>dst.Length must equal src.Length.</remarks>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
    public void Encrypt(System.ReadOnlySpan<System.Byte> src, System.Span<System.Byte> dst)
    {
        if (dst.Length != src.Length)
        {
            throw new System.ArgumentException("Output length must match input length.");
        }

        DE45FA67(src, dst, src.Length);
    }

    /// <summary>
    /// Encrypts <paramref name="src"/> into <paramref name="dst"/> using the current state (XOR with keystream).
    /// </summary>
    /// <remarks>dst.Length must equal src.Length.</remarks>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
    public System.Boolean Encrypt(System.ReadOnlySpan<System.Byte> src, System.Span<System.Byte> dst, out System.Int32 written)
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
    /// Decrypt arbitrary-length byte array (input), writing the resulting byte array to the output buffer.
    /// </summary>
    /// <remarks>Since this is symmetric operation, it doesn't really matter if you use Encrypt or Decrypt method</remarks>
    /// <param name="output">Output byte array</param>
    /// <param name="input">Input byte array</param>
    /// <param name="numBytes">ProtocolType of bytes to decrypt</param>
    /// <param name="simdMode">Chosen SIMD mode (default is auto-detect)</param>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
    public void DecryptBytes(
        System.Byte[] output, System.Byte[] input,
        System.Int32 numBytes, SimdMode simdMode = SimdMode.AutoDetect)
    {
        System.ArgumentNullException.ThrowIfNull(output);
        System.ArgumentNullException.ThrowIfNull(input);

        if (numBytes < 0 || numBytes > input.Length)
        {
            throw new System.ArgumentOutOfRangeException(
                nameof(numBytes), "The ProtocolType of bytes to read must be between [0..input.Length]");
        }

        if (output.Length < numBytes)
        {
            throw new System.ArgumentOutOfRangeException(
                nameof(output), $"Output byte array should be able to take at least {numBytes}");
        }

        if (simdMode == SimdMode.AutoDetect)
        {
            simdMode = AB12CD34();
        }

        EF56AB78(output, input, numBytes, simdMode);
    }

    /// <summary>
    /// Decrypt arbitrary-length byte array (input), writing the resulting byte array to preallocated output buffer.
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
    public void DecryptBytes(
        System.Byte[] output, System.Byte[] input,
        SimdMode simdMode = SimdMode.AutoDetect)
    {
        System.ArgumentNullException.ThrowIfNull(output);
        System.ArgumentNullException.ThrowIfNull(input);

        if (simdMode == SimdMode.AutoDetect)
        {
            simdMode = AB12CD34();
        }

        EF56AB78(output, input, input.Length, simdMode);
    }

    /// <summary>
    /// Decrypt arbitrary-length byte array (input), writing the resulting byte array that is allocated by method.
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
    public System.Byte[] DecryptBytes(
        System.Byte[] input, System.Int32 numBytes,
        SimdMode simdMode = SimdMode.AutoDetect)
    {
        System.ArgumentNullException.ThrowIfNull(input);

        if (numBytes < 0 || numBytes > input.Length)
        {
            throw new System.ArgumentOutOfRangeException(nameof(numBytes),
                "The ProtocolType of bytes to read must be between [0..input.Length]");
        }

        if (simdMode == SimdMode.AutoDetect)
        {
            simdMode = AB12CD34();
        }

        System.Byte[] returnArray = new System.Byte[numBytes];
        EF56AB78(returnArray, input, numBytes, simdMode);
        return returnArray;
    }

    /// <summary>
    /// Decrypt arbitrary-length byte array (input), writing the resulting byte array that is allocated by method.
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
    public System.Byte[] DecryptBytes(
        System.Byte[] input,
        SimdMode simdMode = SimdMode.AutoDetect)
    {
        System.ArgumentNullException.ThrowIfNull(input);

        if (simdMode == SimdMode.AutoDetect)
        {
            simdMode = AB12CD34();
        }

        System.Byte[] returnArray = new System.Byte[input.Length];
        EF56AB78(returnArray, input, input.Length, simdMode);
        return returnArray;
    }

    /// <summary>
    /// Decrypts <paramref name="src"/> into <paramref name="dst"/>. For ChaCha20, this is identical to Encrypt.
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
    public void Decrypt(System.ReadOnlySpan<System.Byte> src, System.Span<System.Byte> dst) => Encrypt(src, dst);

    /// <summary>
    /// In-place encryption (XOR) of <paramref name="buffer"/>.
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Style", "IDE0059:Unnecessary assignment of a value", Justification = "<Pending>")]
    public void EncryptInPlace(System.Span<System.Byte> buffer, SimdMode simdMode = SimdMode.AutoDetect)
    {
        if (_isDisposed)
        {
            throw new System.ObjectDisposedException("state", "The ChaCha20 state has been disposed");
        }

        if (simdMode == SimdMode.AutoDetect)
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
    public void DecryptInPlace(System.Span<System.Byte> buffer, SimdMode simdMode = SimdMode.AutoDetect) => EncryptInPlace(buffer, simdMode);

    #endregion Decryption methods

    #region Static Methods

    /// <summary>
    /// Encrypts or decrypts the input bytes using ChaCha20 in a one-shot static API.
    /// </summary>
    /// <param name="key">32-byte key</param>
    /// <param name="nonce">12-byte nonce</param>
    /// <param name="counter">Initial block counter</param>
    /// <param name="input">Input data to encrypt/decrypt</param>
    /// <param name="simdMode">SIMD acceleration mode (default auto)</param>
    /// <returns>Encrypted/decrypted output</returns>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
    public static System.Byte[] Encrypt(
        System.Byte[] key,
        System.Byte[] nonce,
        System.UInt32 counter,
        System.Byte[] input,
        SimdMode simdMode = SimdMode.AutoDetect)
    {
        using ChaCha20 chacha = new(key, nonce, counter);
        return chacha.EncryptBytes(input, simdMode);
    }

    /// <summary>
    /// Decrypts the input bytes using ChaCha20 in a one-shot static API.
    /// (Same as Encrypt, provided for clarity.)
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
    public static System.Byte[] Decrypt(
        System.Byte[] key,
        System.Byte[] nonce,
        System.UInt32 counter,
        System.Byte[] input,
        SimdMode simdMode = SimdMode.AutoDetect)
    {
        using ChaCha20 chacha = new(key, nonce, counter);
        return chacha.DecryptBytes(input, simdMode);
    }

    #endregion Static Methods

    #region Private Methods

    /// <summary>
    /// Read little-endian uint from span without allocation.
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    private static System.UInt32 ABF98B53(System.ReadOnlySpan<System.Byte> src, System.Int32 offset)
    {
        return (System.UInt32)(src[offset]
            | src[offset + 1] << 8
            | src[offset + 2] << 16
            | src[offset + 3] << 24);
    }

    /// <summary>
    /// Set up the ChaCha20 state with the given key (span-based). Does NOT allocate.
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
    private void E8F7A6B5(System.ReadOnlySpan<System.Byte> keySpan)
    {
        if (keySpan.Length != KeySize)
        {
            throw new System.ArgumentException($"Key length must be {KeySize}. Actual: {keySpan.Length}");
        }

        State[0] = 0x61707865; // Constant ("expand 32-byte k")
        State[1] = 0x3320646e;
        State[2] = 0x79622d32;
        State[3] = 0x6b206574;

        for (System.Int32 i = 0; i < 8; i++)
        {
            State[4 + i] = ABF98B53(keySpan, i * 4);
        }
    }

    /// <summary>
    /// Set up the ChaCha20 state with the given nonce (span) and block counter. A 12-byte nonce and a 4-byte counter are required.
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
    private void F9E8D7C6(System.ReadOnlySpan<System.Byte> nonceSpan, System.UInt32 counter)
    {
        if (nonceSpan.Length != NonceSize)
        {
            Dispose();
            throw new System.ArgumentException($"Nonce length must be {NonceSize}. Actual: {nonceSpan.Length}");
        }

        State[12] = counter;

        for (System.Int32 i = 0; i < 3; i++)
        {
            State[13 + i] = ABF98B53(nonceSpan, i * 4);
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

        return SimdMode.None;
    }

    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
    private void DE45FA67(
        System.ReadOnlySpan<System.Byte> input, System.Span<System.Byte> output, System.Int32 numBytes)
    {
        if (_isDisposed)
        {
            throw new System.ObjectDisposedException("state", "The ChaCha20 state has been disposed");
        }

        if (numBytes < 0 || numBytes > input.Length || numBytes > output.Length)
        {
            throw new System.ArgumentOutOfRangeException(nameof(numBytes));
        }

        // Reuse _working and _keystream to avoid per-call allocations.

        System.Int32 offset = 0;
        System.Int32 full = numBytes / BlockSize;
        System.Int32 tail = numBytes - (full * BlockSize);

        for (System.Int32 loop = 0; loop < full; loop++)
        {
            FA67BC89(State, _working, _keystream);

            // XOR 64 bytes
            for (System.Int32 i = 0; i < BlockSize; i++)
            {
                output[offset + i] = (System.Byte)(input[offset + i] ^ _keystream[i]);
            }
            offset += BlockSize;
        }

        if (tail > 0)
        {
            FA67BC89(State, _working, _keystream);
            for (System.Int32 i = 0; i < tail; i++)
            {
                output[offset + i] = (System.Byte)(input[offset + i] ^ _keystream[i]);
            }
        }
    }

    /// <summary>
    /// Encrypt or decrypt an arbitrary-length byte array (input), writing the resulting byte array to the output buffer. The ProtocolType of bytes to read from the input buffer is determined by numBytes.
    /// This version reuses internal buffers to reduce GC.
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
    private void EF56AB78(
        System.Byte[] output, System.Byte[] input,
        System.Int32 numBytes, SimdMode simdMode)
    {
        if (_isDisposed)
        {
            throw new System.ObjectDisposedException("state", "The ChaCha20 state has been disposed");
        }

        // Reuse buffers
        System.UInt32[] x = _working;
        System.Byte[] tmp = _keystream;
        System.Int32 offset = 0;

        System.Int32 howManyFullLoops = numBytes / BlockSize;
        System.Int32 tailByteCount = numBytes - howManyFullLoops * BlockSize;

        for (System.Int32 loop = 0; loop < howManyFullLoops; loop++)
        {
            FA67BC89(State, x, tmp);

            if (simdMode == SimdMode.V512)
            {
                // 1 x 64 bytes
                System.Runtime.Intrinsics.Vector512<System.Byte> inputV = System.Runtime.Intrinsics.Vector512.Create(input, offset);
                System.Runtime.Intrinsics.Vector512<System.Byte> tmpV = System.Runtime.Intrinsics.Vector512.Create(tmp, 0);
                System.Runtime.Intrinsics.Vector512<System.Byte> outputV = inputV ^ tmpV;
                System.Runtime.Intrinsics.Vector512.CopyTo(outputV, output, offset);
            }
            else if (simdMode == SimdMode.V256)
            {
                // 2 x 32 bytes
                System.Runtime.Intrinsics.Vector256<System.Byte> inputV = System.Runtime.Intrinsics.Vector256.Create(input, offset);
                System.Runtime.Intrinsics.Vector256<System.Byte> tmpV = System.Runtime.Intrinsics.Vector256.Create(tmp, 0);
                System.Runtime.Intrinsics.Vector256<System.Byte> outputV = inputV ^ tmpV;
                System.Runtime.Intrinsics.Vector256.CopyTo(outputV, output, offset);

                inputV = System.Runtime.Intrinsics.Vector256.Create(input, offset + 32);
                tmpV = System.Runtime.Intrinsics.Vector256.Create(tmp, 32);
                outputV = inputV ^ tmpV;
                System.Runtime.Intrinsics.Vector256.CopyTo(outputV, output, offset + 32);
            }
            else if (simdMode == SimdMode.V128)
            {
                // 4 x 16 bytes
                System.Runtime.Intrinsics.Vector128<System.Byte> inputV = System.Runtime.Intrinsics.Vector128.Create(input, offset);
                System.Runtime.Intrinsics.Vector128<System.Byte> tmpV = System.Runtime.Intrinsics.Vector128.Create(tmp, 0);
                System.Runtime.Intrinsics.Vector128<System.Byte> outputV = inputV ^ tmpV;
                System.Runtime.Intrinsics.Vector128.CopyTo(outputV, output, offset);

                inputV = System.Runtime.Intrinsics.Vector128.Create(input, offset + 16);
                tmpV = System.Runtime.Intrinsics.Vector128.Create(tmp, 16);
                outputV = inputV ^ tmpV;
                System.Runtime.Intrinsics.Vector128.CopyTo(outputV, output, offset + 16);

                inputV = System.Runtime.Intrinsics.Vector128.Create(input, offset + 32);
                tmpV = System.Runtime.Intrinsics.Vector128.Create(tmp, 32);
                outputV = inputV ^ tmpV;
                System.Runtime.Intrinsics.Vector128.CopyTo(outputV, output, offset + 32);

                inputV = System.Runtime.Intrinsics.Vector128.Create(input, offset + 48);
                tmpV = System.Runtime.Intrinsics.Vector128.Create(tmp, 48);
                outputV = inputV ^ tmpV;
                System.Runtime.Intrinsics.Vector128.CopyTo(outputV, output, offset + 48);
            }
            else
            {
                for (System.Int32 i = 0; i < BlockSize; i += 4)
                {
                    // Small unroll
                    System.Int32 start = i + offset;
                    output[start] = (System.Byte)(input[start] ^ tmp[i]);
                    output[start + 1] = (System.Byte)(input[start + 1] ^ tmp[i + 1]);
                    output[start + 2] = (System.Byte)(input[start + 2] ^ tmp[i + 2]);
                    output[start + 3] = (System.Byte)(input[start + 3] ^ tmp[i + 3]);
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
                output[i + offset] = (System.Byte)(input[i + offset] ^ tmp[i]);
            }
        }
    }

    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
    private unsafe static void FA67BC89(
        System.UInt32[] stateToModify, System.UInt32[] workingBuffer, System.Byte[] temporaryBuffer)
    {
        // Copy state to working buffer (byte copy for performance)
        System.Buffer.BlockCopy(stateToModify, 0, workingBuffer, 0, StateLength * sizeof(System.UInt32));

        for (System.Int32 i = 0; i < 10; i++) // 20 rounds (10 double rounds)
        {
            A0B1C2D3(workingBuffer, 0, 4, 8, 12);
            A0B1C2D3(workingBuffer, 1, 5, 9, 13);
            A0B1C2D3(workingBuffer, 2, 6, 10, 14);
            A0B1C2D3(workingBuffer, 3, 7, 11, 15);

            A0B1C2D3(workingBuffer, 0, 5, 10, 15);
            A0B1C2D3(workingBuffer, 1, 6, 11, 12);
            A0B1C2D3(workingBuffer, 2, 7, 8, 13);
            A0B1C2D3(workingBuffer, 3, 4, 9, 14);
        }

        for (System.Int32 i = 0; i < StateLength; i++)
        {
            fixed (System.Byte* ptr = &temporaryBuffer[4 * i])
            {
                System.Runtime.CompilerServices.Unsafe.WriteUnaligned(ptr, BitwiseOperations.Add(workingBuffer[i], stateToModify[i]));
            }
        }

        stateToModify[12] = BitwiseOperations.AddOne(stateToModify[12]);
        if (stateToModify[12] <= 0)
        {
            /* Stopping at 2^70 bytes per nonce is the user's responsibility */
            stateToModify[13] = BitwiseOperations.AddOne(stateToModify[13]);
        }
    }

    /// <summary>
    /// The ChaCha20 Quarter Round operation.
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    private static void A0B1C2D3(
        System.UInt32[] x, System.UInt32 a, System.UInt32 b, System.UInt32 c, System.UInt32 d)
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