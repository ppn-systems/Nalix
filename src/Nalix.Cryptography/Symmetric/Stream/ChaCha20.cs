// Copyright (c) 2025 PPN Corporation. All rights reserved.

using Nalix.Common.Security.Enums;
using Nalix.Cryptography.Primitives;

namespace Nalix.Cryptography.Symmetric.Stream;

/// <summary>
/// Class for ChaCha20 encryption / decryption
/// </summary>
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

    /// <summary>
    /// 2^30 bytes per nonce
    /// </summary>
    private const System.Int32 MaxBytesPerNonce = 1 << 30;

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

    // These are the same constants defined in the reference implementation.
    // http://cr.yp.to/streamciphers/timings/estreambench/submissions/salsa20/chacha8/ref/chacha.c
    private static readonly System.Byte[] Sigma = "expand 32-byte k"u8.ToArray();

    private static readonly System.Byte[] Tau = "expand 16-byte k"u8.ToArray();

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
        this.KeySetup(key);
        this.IvSetup(nonce, counter);
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
        this.KeySetup(key.ToArray());
        this.IvSetup(nonce.ToArray(), counter);
    }

    /// <summary>
    /// Generates one 64-byte keystream block into <paramref name="dst"/> at the current counter,
    /// then advances the internal counter by 1 (per RFC 7539).
    /// If dst.Length &lt; 64, only writes the first dst.Length bytes.
    /// </summary>
    /// <param name="dst">Destination span to receive the keystream block.</param>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public void GenerateKeyBlock(System.Span<System.Byte> dst)
    {
        if (_isDisposed)
        {
            throw new System.ObjectDisposedException("state", "The ChaCha state has been disposed");
        }

        // Reuse existing state update logic to produce a 64-byte block.
        var x = new System.UInt32[StateLength];
        var tmp = new System.Byte[BlockSize];

        UpdateStateAndGenerateTemporaryBuffer(State, x, tmp);

        System.Int32 n = dst.Length < BlockSize ? dst.Length : BlockSize;
        System.Buffer.BlockCopy(tmp, 0, dst.ToArray(), 0, n); // copy via temp array view

        // Note:
        // BlockCopy needs arrays; to avoid extra alloc, copy manually when dst.Length < 64.
        // So we do an explicit fast path:
        if (n > 0)
        {
            for (System.Int32 i = 0; i < n; i++)
            {
                dst[i] = tmp[i];
            }
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
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public void EncryptBytes(
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
            simdMode = DetectSimdMode();
        }

        this.WorkBytes(output, input, numBytes, simdMode);
    }

    /// <summary>
    /// Encrypt arbitrary-length byte stream (input), writing the resulting bytes to another stream (output)
    /// </summary>
    /// <param name="output">Output stream</param>
    /// <param name="input">Input stream</param>
    /// <param name="howManyBytesToProcessAtTime">How many bytes to read and write at time, default is 1024</param>
    /// <param name="simdMode">Chosen SIMD mode (default is auto-detect)</param>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public void EncryptStream(
        System.IO.Stream output, System.IO.Stream input,
        System.Int32 howManyBytesToProcessAtTime = 1024,
        SimdMode simdMode = SimdMode.AutoDetect)
    {
        if (simdMode == SimdMode.AutoDetect)
        {
            simdMode = DetectSimdMode();
        }

        this.WorkStreams(output, input, simdMode, howManyBytesToProcessAtTime);
    }

    /// <summary>
    /// Async encrypt arbitrary-length byte stream (input), writing the resulting bytes to another stream (output)
    /// </summary>
    /// <param name="output">Output stream</param>
    /// <param name="input">Input stream</param>
    /// <param name="howManyBytesToProcessAtTime">How many bytes to read and write at time, default is 1024</param>
    /// <param name="simdMode">Chosen SIMD mode (default is auto-detect)</param>
    public async System.Threading.Tasks.Task EncryptStreamAsync(
        System.IO.Stream output,
        System.IO.Stream input,
        System.Int32 howManyBytesToProcessAtTime = 1024,
        SimdMode simdMode = SimdMode.AutoDetect)
    {
        if (simdMode == SimdMode.AutoDetect)
        {
            simdMode = DetectSimdMode();
        }

        await WorkStreamsAsync(output, input, simdMode, howManyBytesToProcessAtTime);
    }

    /// <summary>
    /// Encrypt arbitrary-length byte array (input), writing the resulting byte array to preallocated output buffer.
    /// </summary>
    /// <remarks>Since this is symmetric operation, it doesn't really matter if you use Encrypt or Decrypt method</remarks>
    /// <param name="output">Output byte array, must have enough bytes</param>
    /// <param name="input">Input byte array</param>
    /// <param name="simdMode">Chosen SIMD mode (default is auto-detect)</param>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public void EncryptBytes(System.Byte[] output, System.Byte[] input, SimdMode simdMode = SimdMode.AutoDetect)
    {
        System.ArgumentNullException.ThrowIfNull(output);
        System.ArgumentNullException.ThrowIfNull(input);

        if (simdMode == SimdMode.AutoDetect)
        {
            simdMode = DetectSimdMode();
        }

        this.WorkBytes(output, input, input.Length, simdMode);
    }

    /// <summary>
    /// Encrypt arbitrary-length byte array (input), writing the resulting byte array that is allocated by method.
    /// </summary>
    /// <remarks>Since this is symmetric operation, it doesn't really matter if you use Encrypt or Decrypt method</remarks>
    /// <param name="input">Input byte array</param>
    /// <param name="numBytes">ProtocolType of bytes to encrypt</param>
    /// <param name="simdMode">Chosen SIMD mode (default is auto-detect)</param>
    /// <returns>Byte array that contains encrypted bytes</returns>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public System.Byte[] EncryptBytes(
        System.Byte[] input, System.Int32 numBytes,
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
            simdMode = DetectSimdMode();
        }

        System.Byte[] returnArray = new System.Byte[numBytes];
        this.WorkBytes(returnArray, input, numBytes, simdMode);
        return returnArray;
    }

    /// <summary>
    /// Encrypt arbitrary-length byte array (input), writing the resulting byte array that is allocated by method.
    /// </summary>
    /// <remarks>Since this is symmetric operation, it doesn't really matter if you use Encrypt or Decrypt method</remarks>
    /// <param name="input">Input byte array</param>
    /// <param name="simdMode">Chosen SIMD mode (default is auto-detect)</param>
    /// <returns>Byte array that contains encrypted bytes</returns>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public System.Byte[] EncryptBytes(
        System.Byte[] input,
        SimdMode simdMode = SimdMode.AutoDetect)
    {
        System.ArgumentNullException.ThrowIfNull(input);

        if (simdMode == SimdMode.AutoDetect)
        {
            simdMode = DetectSimdMode();
        }

        System.Byte[] returnArray = new System.Byte[input.Length];
        this.WorkBytes(returnArray, input, input.Length, simdMode);
        return returnArray;
    }

    /// <summary>
    /// Encrypt string as UTF8 byte array, returns byte array that is allocated by method.
    /// </summary>
    /// <remarks>Here you can NOT swap encrypt and decrypt methods, because of bytes-string transform</remarks>
    /// <param name="input">Input string</param>
    /// <param name="simdMode">Chosen SIMD mode (default is auto-detect)</param>
    /// <returns>Byte array that contains encrypted bytes</returns>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public System.Byte[] EncryptString(System.String input, SimdMode simdMode = SimdMode.AutoDetect)
    {
        System.ArgumentNullException.ThrowIfNull(input);

        if (simdMode == SimdMode.AutoDetect)
        {
            simdMode = DetectSimdMode();
        }

        System.Byte[] utf8Bytes = System.Text.Encoding.UTF8.GetBytes(input);
        System.Byte[] returnArray = new System.Byte[utf8Bytes.Length];

        this.WorkBytes(returnArray, utf8Bytes, utf8Bytes.Length, simdMode);
        return returnArray;
    }

    /// <summary>
    /// Encrypts <paramref name="src"/> into <paramref name="dst"/> using the current state (XOR with keystream).
    /// </summary>
    /// <remarks>dst.Length must equal src.Length.</remarks>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public void Encrypt(System.ReadOnlySpan<System.Byte> src, System.Span<System.Byte> dst)
    {
        if (dst.Length != src.Length)
        {
            throw new System.ArgumentException("Output length must match input length.");
        }

        WorkBytes(src, dst, src.Length);
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
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
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
            simdMode = DetectSimdMode();
        }

        WorkBytes(output, input, numBytes, simdMode);
    }

    /// <summary>
    /// Decrypt arbitrary-length byte stream (input), writing the resulting bytes to another stream (output)
    /// </summary>
    /// <param name="output">Output stream</param>
    /// <param name="input">Input stream</param>
    /// <param name="howManyBytesToProcessAtTime">How many bytes to read and write at time, default is 1024</param>
    /// <param name="simdMode">Chosen SIMD mode (default is auto-detect)</param>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public void DecryptStream(
        System.IO.Stream output,
        System.IO.Stream input,
        System.Int32 howManyBytesToProcessAtTime = 1024,
        SimdMode simdMode = SimdMode.AutoDetect)
    {
        if (simdMode == SimdMode.AutoDetect)
        {
            simdMode = DetectSimdMode();
        }

        WorkStreams(output, input, simdMode, howManyBytesToProcessAtTime);
    }

    /// <summary>
    /// Async decrypt arbitrary-length byte stream (input), writing the resulting bytes to another stream (output)
    /// </summary>
    /// <param name="output">Output stream</param>
    /// <param name="input">Input stream</param>
    /// <param name="howManyBytesToProcessAtTime">How many bytes to read and write at time, default is 1024</param>
    /// <param name="simdMode">Chosen SIMD mode (default is auto-detect)</param>
    public async System.Threading.Tasks.Task DecryptStreamAsync(
        System.IO.Stream output,
        System.IO.Stream input,
        System.Int32 howManyBytesToProcessAtTime = 1024,
        SimdMode simdMode = SimdMode.AutoDetect)
    {
        if (simdMode == SimdMode.AutoDetect)
        {
            simdMode = DetectSimdMode();
        }

        await WorkStreamsAsync(output, input, simdMode, howManyBytesToProcessAtTime);
    }

    /// <summary>
    /// Decrypt arbitrary-length byte array (input), writing the resulting byte array to preallocated output buffer.
    /// </summary>
    /// <remarks>Since this is symmetric operation, it doesn't really matter if you use Encrypt or Decrypt method</remarks>
    /// <param name="output">Output byte array, must have enough bytes</param>
    /// <param name="input">Input byte array</param>
    /// <param name="simdMode">Chosen SIMD mode (default is auto-detect)</param>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public void DecryptBytes(
        System.Byte[] output, System.Byte[] input,
        SimdMode simdMode = SimdMode.AutoDetect)
    {
        System.ArgumentNullException.ThrowIfNull(output);
        System.ArgumentNullException.ThrowIfNull(input);

        if (simdMode == SimdMode.AutoDetect)
        {
            simdMode = DetectSimdMode();
        }

        WorkBytes(output, input, input.Length, simdMode);
    }

    /// <summary>
    /// Decrypt arbitrary-length byte array (input), writing the resulting byte array that is allocated by method.
    /// </summary>
    /// <remarks>Since this is symmetric operation, it doesn't really matter if you use Encrypt or Decrypt method</remarks>
    /// <param name="input">Input byte array</param>
    /// <param name="numBytes">ProtocolType of bytes to encrypt</param>
    /// <param name="simdMode">Chosen SIMD mode (default is auto-detect)</param>
    /// <returns>Byte array that contains decrypted bytes</returns>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
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
            simdMode = DetectSimdMode();
        }

        System.Byte[] returnArray = new System.Byte[numBytes];
        WorkBytes(returnArray, input, numBytes, simdMode);
        return returnArray;
    }

    /// <summary>
    /// Decrypt arbitrary-length byte array (input), writing the resulting byte array that is allocated by method.
    /// </summary>
    /// <remarks>Since this is symmetric operation, it doesn't really matter if you use Encrypt or Decrypt method</remarks>
    /// <param name="input">Input byte array</param>
    /// <param name="simdMode">Chosen SIMD mode (default is auto-detect)</param>
    /// <returns>Byte array that contains decrypted bytes</returns>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public System.Byte[] DecryptBytes(
        System.Byte[] input,
        SimdMode simdMode = SimdMode.AutoDetect)
    {
        System.ArgumentNullException.ThrowIfNull(input);

        if (simdMode == SimdMode.AutoDetect)
        {
            simdMode = DetectSimdMode();
        }

        System.Byte[] returnArray = new System.Byte[input.Length];
        WorkBytes(returnArray, input, input.Length, simdMode);
        return returnArray;
    }

    /// <summary>
    /// Decrypt UTF8 byte array to string.
    /// </summary>
    /// <remarks>Here you can NOT swap encrypt and decrypt methods, because of bytes-string transform</remarks>
    /// <param name="input">Byte array</param>
    /// <param name="simdMode">Chosen SIMD mode (default is auto-detect)</param>
    /// <returns>Byte array that contains encrypted bytes</returns>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public System.String DecryptUtf8ByteArray(
        System.Byte[] input,
        SimdMode simdMode = SimdMode.AutoDetect)
    {
        System.ArgumentNullException.ThrowIfNull(input);

        if (simdMode == SimdMode.AutoDetect)
        {
            simdMode = DetectSimdMode();
        }

        System.Byte[] tempArray = new System.Byte[input.Length];

        WorkBytes(tempArray, input, input.Length, simdMode);
        return System.Text.Encoding.UTF8.GetString(tempArray);
    }

    /// <summary>
    /// Decrypts <paramref name="src"/> into <paramref name="dst"/>. For ChaCha20, this is identical to Encrypt.
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public void Decrypt(System.ReadOnlySpan<System.Byte> src, System.Span<System.Byte> dst)
        => Encrypt(src, dst);

    /// <summary>
    /// In-place encryption (XOR) of <paramref name="buffer"/>.
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public void EncryptInPlace(System.Span<System.Byte> buffer, SimdMode simdMode = SimdMode.AutoDetect)
    {
        // Use a small stack buffer to avoid allocating a second array the same size as 'buffer'.
        // We process in 64-byte blocks.
        if (simdMode == SimdMode.AutoDetect)
        {
            _ = DetectSimdMode();
        }

        var tmpKeystream = new System.Byte[BlockSize];
        var x = new System.UInt32[StateLength];

        System.Int32 offset = 0;
        System.Int32 remaining = buffer.Length;

        while (remaining >= BlockSize)
        {
            UpdateStateAndGenerateTemporaryBuffer(State, x, tmpKeystream);
            for (System.Int32 i = 0; i < BlockSize; i++)
            {
                buffer[offset + i] = (System.Byte)(buffer[offset + i] ^ tmpKeystream[i]);
            }

            offset += BlockSize;
            remaining -= BlockSize;
        }

        if (remaining > 0)
        {
            UpdateStateAndGenerateTemporaryBuffer(State, x, tmpKeystream);
            for (System.Int32 i = 0; i < remaining; i++)
            {
                buffer[offset + i] = (System.Byte)(buffer[offset + i] ^ tmpKeystream[i]);
            }
        }
    }

    /// <summary>
    /// In-place decryption of <paramref name="buffer"/> (same as EncryptInPlace).
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public void DecryptInPlace(System.Span<System.Byte> buffer, SimdMode simdMode = SimdMode.AutoDetect)
        => EncryptInPlace(buffer, simdMode);

    #endregion Decryption methods

    #region Private Methods

    /// <summary>
    /// Set up the ChaCha state with the given key. A 32-byte key is required and enforced.
    /// </summary>
    /// <param name="key">
    /// A 32-byte (256-bit) key, treated as a concatenation of eight 32-bit little-endian integers
    /// </param>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    private void KeySetup(System.Byte[] key)
    {
        if (key.Length != KeySize)
        {
            throw new System.ArgumentException($"Key length must be {KeySize}. Actual: {key.Length}");
        }

        State[0] = 0x61707865; // Constant ("expand 32-byte k")
        State[1] = 0x3320646e;
        State[2] = 0x79622d32;
        State[3] = 0x6b206574;

        for (System.Int32 i = 0; i < 8; i++)
        {
            State[4 + i] = BitwiseUtils.U8To32Little(key, i * 4);
        }
    }

    /// <summary>
    /// Set up the ChaCha state with the given nonce (aka Initialization Vector or IV) and block counter. A 12-byte nonce and a 4-byte counter are required.
    /// </summary>
    /// <param name="nonce">
    /// A 12-byte (96-bit) nonce, treated as a concatenation of three 32-bit little-endian integers
    /// </param>
    /// <param name="counter">
    /// A 4-byte (32-bit) block counter, treated as a 32-bit little-endian integer
    /// </param>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    private void IvSetup(System.Byte[] nonce, System.UInt32 counter)
    {
        if (nonce.Length != NonceSize)
        {
            Dispose();
            throw new System.ArgumentException($"Nonce length must be {NonceSize}. Actual: {nonce.Length}");
        }

        State[12] = counter;

        for (System.Int32 i = 0; i < 3; i++)
        {
            State[13 + i] = BitwiseUtils.U8To32Little(nonce, i * 4);
        }
    }

    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    private static SimdMode DetectSimdMode()
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
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    private void WorkStreams(
        System.IO.Stream output, System.IO.Stream input,
        SimdMode simdMode, System.Int32 howManyBytesToProcessAtTime = 1024)
    {
        System.Int32 readBytes;

        System.Byte[] inputBuffer = new System.Byte[howManyBytesToProcessAtTime];
        System.Byte[] outputBuffer = new System.Byte[howManyBytesToProcessAtTime];

        while ((readBytes = input.Read(inputBuffer, 0, howManyBytesToProcessAtTime)) > 0)
        {
            // Encrypt or decrypt
            WorkBytes(output: outputBuffer, input: inputBuffer, numBytes: readBytes, simdMode);

            // WriteInt16 buffer
            output.Write(outputBuffer, 0, readBytes);
        }
    }

    private async System.Threading.Tasks.Task WorkStreamsAsync(
        System.IO.Stream output, System.IO.Stream input,
        SimdMode simdMode, System.Int32 howManyBytesToProcessAtTime = 1024)
    {
        System.Byte[] readBytesBuffer = new System.Byte[howManyBytesToProcessAtTime];
        System.Byte[] writeBytesBuffer = new System.Byte[howManyBytesToProcessAtTime];
        System.Int32 howManyBytesWereRead = await input.ReadAsync(System.MemoryExtensions
                                                       .AsMemory(readBytesBuffer, 0, howManyBytesToProcessAtTime));

        while (howManyBytesWereRead > 0)
        {
            // Encrypt or decrypt
            WorkBytes(output: writeBytesBuffer, input: readBytesBuffer, numBytes: howManyBytesWereRead, simdMode);

            // WriteInt16
            await output.WriteAsync(System.MemoryExtensions
                        .AsMemory(writeBytesBuffer, 0, howManyBytesWereRead));

            // Read more
            howManyBytesWereRead = await input.ReadAsync(System.MemoryExtensions
                                              .AsMemory(readBytesBuffer, 0, howManyBytesToProcessAtTime));
        }
    }

    [System.Runtime.CompilerServices.MethodImpl(
    System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    private void WorkBytes(
        System.ReadOnlySpan<System.Byte> input, System.Span<System.Byte> output, System.Int32 numBytes)
    {
        if (_isDisposed)
        {
            throw new System.ObjectDisposedException("state", "The ChaCha state has been disposed");
        }

        if (numBytes < 0 || numBytes > input.Length || numBytes > output.Length)
        {
            throw new System.ArgumentOutOfRangeException(nameof(numBytes));
        }

        // We keep this simple and robust: scalar XOR with generated blocks.
        // (Your array-based overload already has SIMD paths.)
        var x = new System.UInt32[StateLength];
        var tmp = new System.Byte[BlockSize];

        System.Int32 offset = 0;
        System.Int32 full = numBytes / BlockSize;
        System.Int32 tail = numBytes - (full * BlockSize);

        for (System.Int32 loop = 0; loop < full; loop++)
        {
            UpdateStateAndGenerateTemporaryBuffer(State, x, tmp);

            // XOR 64 bytes
            for (System.Int32 i = 0; i < BlockSize; i++)
            {
                output[offset + i] = (System.Byte)(input[offset + i] ^ tmp[i]);
            }
            offset += BlockSize;
        }

        if (tail > 0)
        {
            UpdateStateAndGenerateTemporaryBuffer(State, x, tmp);
            for (System.Int32 i = 0; i < tail; i++)
            {
                output[offset + i] = (System.Byte)(input[offset + i] ^ tmp[i]);
            }
        }
    }

    /// <summary>
    /// Encrypt or decrypt an arbitrary-length byte array (input), writing the resulting byte array to the output buffer. The ProtocolType of bytes to read from the input buffer is determined by numBytes.
    /// </summary>
    /// <param name="output">Output byte array</param>
    /// <param name="input">Input byte array</param>
    /// <param name="numBytes">How many bytes to process</param>
    /// <param name="simdMode">Chosen SIMD mode (default is auto-detect)</param>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    private void WorkBytes(
        System.Byte[] output, System.Byte[] input,
        System.Int32 numBytes, SimdMode simdMode)
    {
        if (_isDisposed)
        {
            throw new System.ObjectDisposedException("state", "The ChaCha state has been disposed");
        }

        System.UInt32[] x = new System.UInt32[StateLength];    // Working buffer
        System.Byte[] tmp = new System.Byte[BlockSize];  // Temporary buffer
        System.Int32 offset = 0;

        System.Int32 howManyFullLoops = numBytes / BlockSize;
        System.Int32 tailByteCount = numBytes - (howManyFullLoops * BlockSize);

        for (System.Int32 loop = 0; loop < howManyFullLoops; loop++)
        {
            UpdateStateAndGenerateTemporaryBuffer(State, x, tmp);

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
            UpdateStateAndGenerateTemporaryBuffer(State, x, tmp);

            for (System.Int32 i = 0; i < tailByteCount; i++)
            {
                output[i + offset] = (System.Byte)(input[i + offset] ^ tmp[i]);
            }
        }
    }

    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    private static void UpdateStateAndGenerateTemporaryBuffer(
        System.UInt32[] stateToModify, System.UInt32[] workingBuffer, System.Byte[] temporaryBuffer)
    {
        // Copy state to working buffer
        System.Buffer.BlockCopy(stateToModify, 0, workingBuffer, 0, StateLength * sizeof(System.UInt32));

        for (System.Int32 i = 0; i < 10; i++) // 20 rounds (10 double rounds)
        {
            QuarterRound(workingBuffer, 0, 4, 8, 12);
            QuarterRound(workingBuffer, 1, 5, 9, 13);
            QuarterRound(workingBuffer, 2, 6, 10, 14);
            QuarterRound(workingBuffer, 3, 7, 11, 15);

            QuarterRound(workingBuffer, 0, 5, 10, 15);
            QuarterRound(workingBuffer, 1, 6, 11, 12);
            QuarterRound(workingBuffer, 2, 7, 8, 13);
            QuarterRound(workingBuffer, 3, 4, 9, 14);
        }

        for (System.Int32 i = 0; i < StateLength; i++)
        {
            BitwiseUtils.ToBytes(temporaryBuffer, BitwiseUtils.Add(workingBuffer[i], stateToModify[i]), 4 * i);
        }

        stateToModify[12] = BitwiseUtils.AddOne(stateToModify[12]);
        if (stateToModify[12] <= 0)
        {
            /* Stopping at 2^70 bytes per nonce is the user's responsibility */
            stateToModify[13] = BitwiseUtils.AddOne(stateToModify[13]);
        }
    }

    /// <summary>
    /// The ChaCha Quarter Round operation. It operates on four 32-bit unsigned integers within the given buffer at indices a, b, c, and d.
    /// </summary>
    /// <remarks>
    /// The ChaCha state does not have four integer numbers: it has 16. So the quarter-round operation works on only four of them -- hence the name. Each quarter round operates on four predetermined numbers in the ChaCha state.
    /// See <a href="https://tools.ietf.org/html/rfc7539#page-4">ChaCha20 Spec Sections 2.1 - 2.2</a>.
    /// </remarks>
    /// <param name="x">A ChaCha state (vector). Must contain 16 elements.</param>
    /// <param name="a">Index of the first ProtocolType</param>
    /// <param name="b">Index of the second ProtocolType</param>
    /// <param name="c">Index of the third ProtocolType</param>
    /// <param name="d">Index of the fourth ProtocolType</param>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    private static void QuarterRound(
        System.UInt32[] x, System.UInt32 a, System.UInt32 b, System.UInt32 c, System.UInt32 d)
    {
        x[a] = BitwiseUtils.Add(x[a], x[b]);
        x[d] = BitwiseUtils.RotateLeft(BitwiseUtils.XOr(x[d], x[a]), 16);

        x[c] = BitwiseUtils.Add(x[c], x[d]);
        x[b] = BitwiseUtils.RotateLeft(BitwiseUtils.XOr(x[b], x[c]), 12);

        x[a] = BitwiseUtils.Add(x[a], x[b]);
        x[d] = BitwiseUtils.RotateLeft(BitwiseUtils.XOr(x[d], x[a]), 8);

        x[c] = BitwiseUtils.Add(x[c], x[d]);
        x[b] = BitwiseUtils.RotateLeft(BitwiseUtils.XOr(x[b], x[c]), 7);
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
        /*
		* The Garbage Collector does not need to invoke the finalizer because Dispose(bool) has already done all the cleanup needed.
		*/
        System.GC.SuppressFinalize(this);
    }

    /// <summary>
    /// This method should only be invoked from Dispose() or the finalizer. This handles the actual cleanup of the resources.
    /// </summary>
    /// <param name="disposing">
    /// Should be true if called by Dispose(); false if called by the finalizer
    /// </param>
    [System.Diagnostics.DebuggerNonUserCode]
    private void Dispose(System.Boolean disposing)
    {
        if (!_isDisposed)
        {
            if (disposing)
            {
                /* Cleanup managed objects by calling their Dispose() methods */
            }

            /* Cleanup any unmanaged objects here */
            System.Array.Clear(State, 0, StateLength);
        }

        _isDisposed = true;
    }

    #endregion Destructor and Disposer
}