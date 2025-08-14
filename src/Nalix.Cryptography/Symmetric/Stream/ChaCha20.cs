// Copyright (c) 2025 PPN Corporation. All rights reserved.

using Nalix.Cryptography.Enums;
using Nalix.Cryptography.Internal;
using System;
using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics;
using System.Threading.Tasks;

namespace Nalix.Cryptography.Symmetric.Stream;

/// <summary>
/// Class for ChaCha20 encryption / decryption
/// </summary>
public sealed class ChaCha20 : IDisposable
{
    #region Constants

    /// <summary>
    /// Only allowed key length in bytes.
    /// </summary>
    public const Int32 KeySize = 32;

    /// <summary>
    /// The size of a nonce in bytes.
    /// </summary>
    public const Int32 NonceSize = 12;

    /// <summary>
    /// The size of a block in bytes.
    /// </summary>
    public const Int32 BlockSize = 64;

    /// <summary>
    /// The length of the key in bytes.
    /// </summary>
    public const Int32 StateLength = 16;

    /// <summary>
    /// 2^30 bytes per nonce
    /// </summary>
    private const Int32 MaxBytesPerNonce = 1 << 30;

    #endregion Constants

    #region Fields

    /// <summary>
    /// Determines if the objects in this class have been disposed of. Set to true by the Dispose() method.
    /// </summary>
    private Boolean _isDisposed;

    /// <summary>
    /// The ChaCha20 state (aka "context"). Read-Only.
    /// </summary>
    private UInt32[] State { get; } = new UInt32[StateLength];

    // These are the same constants defined in the reference implementation.
    // http://cr.yp.to/streamciphers/timings/estreambench/submissions/salsa20/chacha8/ref/chacha.c
    private static readonly Byte[] Sigma = "expand 32-byte k"u8.ToArray();

    private static readonly Byte[] Tau = "expand 16-byte k"u8.ToArray();

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
    public ChaCha20(Byte[] key, Byte[] nonce, UInt32 counter)
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
    public ChaCha20(ReadOnlySpan<Byte> key, ReadOnlySpan<Byte> nonce, UInt32 counter)
    {
        this.KeySetup(key.ToArray());
        this.IvSetup(nonce.ToArray(), counter);
    }

    #endregion Constructors

    #region Encryption methods

    /// <summary>
    /// Encrypt arbitrary-length byte array (input), writing the resulting byte array to preallocated output buffer.
    /// </summary>
    /// <remarks>Since this is symmetric operation, it doesn't really matter if you use Encrypt or Decrypt method</remarks>
    /// <param name="output">Output byte array, must have enough bytes</param>
    /// <param name="input">Input byte array</param>
    /// <param name="numBytes">TransportProtocol of bytes to encrypt</param>
    /// <param name="simdMode">Chosen SIMD mode (default is auto-detect)</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void EncryptBytes(Byte[] output, Byte[] input, Int32 numBytes, SimdMode simdMode = SimdMode.AutoDetect)
    {
        ArgumentNullException.ThrowIfNull(output);
        ArgumentNullException.ThrowIfNull(input);

        if (numBytes < 0 || numBytes > input.Length)
        {
            throw new ArgumentOutOfRangeException(nameof(numBytes), "The TransportProtocol of bytes to read must be between [0..input.Length]");
        }

        if (output.Length < numBytes)
        {
            throw new ArgumentOutOfRangeException(nameof(output), $"Output byte array should be able to take at least {numBytes}");
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
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void EncryptStream(
        System.IO.Stream output, System.IO.Stream input,
        Int32 howManyBytesToProcessAtTime = 1024,
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
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public async Task EncryptStreamAsync(
        System.IO.Stream output,
        System.IO.Stream input,
        Int32 howManyBytesToProcessAtTime = 1024,
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
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void EncryptBytes(Byte[] output, Byte[] input, SimdMode simdMode = SimdMode.AutoDetect)
    {
        ArgumentNullException.ThrowIfNull(output);
        ArgumentNullException.ThrowIfNull(input);

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
    /// <param name="numBytes">TransportProtocol of bytes to encrypt</param>
    /// <param name="simdMode">Chosen SIMD mode (default is auto-detect)</param>
    /// <returns>Byte array that contains encrypted bytes</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Byte[] EncryptBytes(Byte[] input, Int32 numBytes, SimdMode simdMode = SimdMode.AutoDetect)
    {
        ArgumentNullException.ThrowIfNull(input);

        if (numBytes < 0 || numBytes > input.Length)
        {
            throw new ArgumentOutOfRangeException(
                nameof(numBytes), "The TransportProtocol of bytes to read must be between [0..input.Length]");
        }

        if (simdMode == SimdMode.AutoDetect)
        {
            simdMode = DetectSimdMode();
        }

        Byte[] returnArray = new Byte[numBytes];
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
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Byte[] EncryptBytes(Byte[] input, SimdMode simdMode = SimdMode.AutoDetect)
    {
        ArgumentNullException.ThrowIfNull(input);

        if (simdMode == SimdMode.AutoDetect)
        {
            simdMode = DetectSimdMode();
        }

        Byte[] returnArray = new Byte[input.Length];
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
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Byte[] EncryptString(String input, SimdMode simdMode = SimdMode.AutoDetect)
    {
        ArgumentNullException.ThrowIfNull(input);

        if (simdMode == SimdMode.AutoDetect)
        {
            simdMode = DetectSimdMode();
        }

        Byte[] utf8Bytes = System.Text.Encoding.UTF8.GetBytes(input);
        Byte[] returnArray = new Byte[utf8Bytes.Length];

        this.WorkBytes(returnArray, utf8Bytes, utf8Bytes.Length, simdMode);
        return returnArray;
    }

    #endregion Encryption methods

    #region Decryption methods

    /// <summary>
    /// Decrypt arbitrary-length byte array (input), writing the resulting byte array to the output buffer.
    /// </summary>
    /// <remarks>Since this is symmetric operation, it doesn't really matter if you use Encrypt or Decrypt method</remarks>
    /// <param name="output">Output byte array</param>
    /// <param name="input">Input byte array</param>
    /// <param name="numBytes">TransportProtocol of bytes to decrypt</param>
    /// <param name="simdMode">Chosen SIMD mode (default is auto-detect)</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void DecryptBytes(Byte[] output, Byte[] input, Int32 numBytes, SimdMode simdMode = SimdMode.AutoDetect)
    {
        ArgumentNullException.ThrowIfNull(output);
        ArgumentNullException.ThrowIfNull(input);

        if (numBytes < 0 || numBytes > input.Length)
        {
            throw new ArgumentOutOfRangeException(nameof(numBytes), "The TransportProtocol of bytes to read must be between [0..input.Length]");
        }

        if (output.Length < numBytes)
        {
            throw new ArgumentOutOfRangeException(nameof(output), $"Output byte array should be able to take at least {numBytes}");
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
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void DecryptStream(
        System.IO.Stream output,
        System.IO.Stream input,
        Int32 howManyBytesToProcessAtTime = 1024,
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
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public async Task DecryptStreamAsync(
        System.IO.Stream output,
        System.IO.Stream input,
        Int32 howManyBytesToProcessAtTime = 1024,
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
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void DecryptBytes(Byte[] output, Byte[] input, SimdMode simdMode = SimdMode.AutoDetect)
    {
        ArgumentNullException.ThrowIfNull(output);
        ArgumentNullException.ThrowIfNull(input);

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
    /// <param name="numBytes">TransportProtocol of bytes to encrypt</param>
    /// <param name="simdMode">Chosen SIMD mode (default is auto-detect)</param>
    /// <returns>Byte array that contains decrypted bytes</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Byte[] DecryptBytes(Byte[] input, Int32 numBytes, SimdMode simdMode = SimdMode.AutoDetect)
    {
        ArgumentNullException.ThrowIfNull(input);

        if (numBytes < 0 || numBytes > input.Length)
        {
            throw new ArgumentOutOfRangeException(nameof(numBytes),
                "The TransportProtocol of bytes to read must be between [0..input.Length]");
        }

        if (simdMode == SimdMode.AutoDetect)
        {
            simdMode = DetectSimdMode();
        }

        Byte[] returnArray = new Byte[numBytes];
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
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Byte[] DecryptBytes(Byte[] input, SimdMode simdMode = SimdMode.AutoDetect)
    {
        ArgumentNullException.ThrowIfNull(input);

        if (simdMode == SimdMode.AutoDetect)
        {
            simdMode = DetectSimdMode();
        }

        Byte[] returnArray = new Byte[input.Length];
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
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public String DecryptUtf8ByteArray(Byte[] input, SimdMode simdMode = SimdMode.AutoDetect)
    {
        ArgumentNullException.ThrowIfNull(input);

        if (simdMode == SimdMode.AutoDetect)
        {
            simdMode = DetectSimdMode();
        }

        Byte[] tempArray = new Byte[input.Length];

        WorkBytes(tempArray, input, input.Length, simdMode);
        return System.Text.Encoding.UTF8.GetString(tempArray);
    }

    #endregion Decryption methods

    #region Private Methods

    /// <summary>
    /// Set up the ChaCha state with the given key. A 32-byte key is required and enforced.
    /// </summary>
    /// <param name="key">
    /// A 32-byte (256-bit) key, treated as a concatenation of eight 32-bit little-endian integers
    /// </param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void KeySetup(Byte[] key)
    {
        if (key.Length != KeySize)
        {
            throw new ArgumentException($"Key length must be {KeySize}. Actual: {key.Length}");
        }

        State[0] = 0x61707865; // Constant ("expand 32-byte k")
        State[1] = 0x3320646e;
        State[2] = 0x79622d32;
        State[3] = 0x6b206574;

        for (Int32 i = 0; i < 8; i++)
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
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void IvSetup(Byte[] nonce, UInt32 counter)
    {
        if (nonce.Length != NonceSize)
        {
            Dispose();
            throw new ArgumentException($"Nonce length must be {NonceSize}. Actual: {nonce.Length}");
        }

        State[12] = counter;

        for (Int32 i = 0; i < 3; i++)
        {
            State[13 + i] = BitwiseUtils.U8To32Little(nonce, i * 4);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static SimdMode DetectSimdMode()
    {
        if (Vector512.IsHardwareAccelerated)
        {
            return SimdMode.V512;
        }
        else if (Vector256.IsHardwareAccelerated)
        {
            return SimdMode.V256;
        }
        else if (Vector128.IsHardwareAccelerated)
        {
            return SimdMode.V128;
        }

        return SimdMode.None;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void WorkStreams(
        System.IO.Stream output, System.IO.Stream input,
        SimdMode simdMode, Int32 howManyBytesToProcessAtTime = 1024)
    {
        Int32 readBytes;

        Byte[] inputBuffer = new Byte[howManyBytesToProcessAtTime];
        Byte[] outputBuffer = new Byte[howManyBytesToProcessAtTime];

        while ((readBytes = input.Read(inputBuffer, 0, howManyBytesToProcessAtTime)) > 0)
        {
            // Encrypt or decrypt
            WorkBytes(output: outputBuffer, input: inputBuffer, numBytes: readBytes, simdMode);

            // WriteInt16 buffer
            output.Write(outputBuffer, 0, readBytes);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private async Task WorkStreamsAsync(
        System.IO.Stream output, System.IO.Stream input,
        SimdMode simdMode, Int32 howManyBytesToProcessAtTime = 1024)
    {
        Byte[] readBytesBuffer = new Byte[howManyBytesToProcessAtTime];
        Byte[] writeBytesBuffer = new Byte[howManyBytesToProcessAtTime];
        Int32 howManyBytesWereRead = await input.ReadAsync(readBytesBuffer.AsMemory(0, howManyBytesToProcessAtTime));

        while (howManyBytesWereRead > 0)
        {
            // Encrypt or decrypt
            WorkBytes(output: writeBytesBuffer, input: readBytesBuffer, numBytes: howManyBytesWereRead, simdMode);

            // WriteInt16
            await output.WriteAsync(writeBytesBuffer.AsMemory(0, howManyBytesWereRead));

            // Read more
            howManyBytesWereRead = await input.ReadAsync(readBytesBuffer.AsMemory(0, howManyBytesToProcessAtTime));
        }
    }

    /// <summary>
    /// Encrypt or decrypt an arbitrary-length byte array (input), writing the resulting byte array to the output buffer. The TransportProtocol of bytes to read from the input buffer is determined by numBytes.
    /// </summary>
    /// <param name="output">Output byte array</param>
    /// <param name="input">Input byte array</param>
    /// <param name="numBytes">How many bytes to process</param>
    /// <param name="simdMode">Chosen SIMD mode (default is auto-detect)</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void WorkBytes(Byte[] output, Byte[] input, Int32 numBytes, SimdMode simdMode)
    {
        if (_isDisposed)
        {
            throw new ObjectDisposedException("state", "The ChaCha state has been disposed");
        }

        UInt32[] x = new UInt32[StateLength];    // Working buffer
        Byte[] tmp = new Byte[BlockSize];  // Temporary buffer
        Int32 offset = 0;

        Int32 howManyFullLoops = numBytes / BlockSize;
        Int32 tailByteCount = numBytes - (howManyFullLoops * BlockSize);

        for (Int32 loop = 0; loop < howManyFullLoops; loop++)
        {
            UpdateStateAndGenerateTemporaryBuffer(State, x, tmp);

            if (simdMode == SimdMode.V512)
            {
                // 1 x 64 bytes
                Vector512<Byte> inputV = Vector512.Create(input, offset);
                Vector512<Byte> tmpV = Vector512.Create(tmp, 0);
                Vector512<Byte> outputV = inputV ^ tmpV;
                outputV.CopyTo(output, offset);
            }
            else if (simdMode == SimdMode.V256)
            {
                // 2 x 32 bytes
                Vector256<Byte> inputV = Vector256.Create(input, offset);
                Vector256<Byte> tmpV = Vector256.Create(tmp, 0);
                Vector256<Byte> outputV = inputV ^ tmpV;
                outputV.CopyTo(output, offset);

                inputV = Vector256.Create(input, offset + 32);
                tmpV = Vector256.Create(tmp, 32);
                outputV = inputV ^ tmpV;
                outputV.CopyTo(output, offset + 32);
            }
            else if (simdMode == SimdMode.V128)
            {
                // 4 x 16 bytes
                Vector128<Byte> inputV = Vector128.Create(input, offset);
                Vector128<Byte> tmpV = Vector128.Create(tmp, 0);
                Vector128<Byte> outputV = inputV ^ tmpV;
                outputV.CopyTo(output, offset);

                inputV = Vector128.Create(input, offset + 16);
                tmpV = Vector128.Create(tmp, 16);
                outputV = inputV ^ tmpV;
                outputV.CopyTo(output, offset + 16);

                inputV = Vector128.Create(input, offset + 32);
                tmpV = Vector128.Create(tmp, 32);
                outputV = inputV ^ tmpV;
                outputV.CopyTo(output, offset + 32);

                inputV = Vector128.Create(input, offset + 48);
                tmpV = Vector128.Create(tmp, 48);
                outputV = inputV ^ tmpV;
                outputV.CopyTo(output, offset + 48);
            }
            else
            {
                for (Int32 i = 0; i < BlockSize; i += 4)
                {
                    // Small unroll
                    Int32 start = i + offset;
                    output[start] = (Byte)(input[start] ^ tmp[i]);
                    output[start + 1] = (Byte)(input[start + 1] ^ tmp[i + 1]);
                    output[start + 2] = (Byte)(input[start + 2] ^ tmp[i + 2]);
                    output[start + 3] = (Byte)(input[start + 3] ^ tmp[i + 3]);
                }
            }

            offset += BlockSize;
        }

        // In case there are some bytes left
        if (tailByteCount > 0)
        {
            UpdateStateAndGenerateTemporaryBuffer(State, x, tmp);

            for (Int32 i = 0; i < tailByteCount; i++)
            {
                output[i + offset] = (Byte)(input[i + offset] ^ tmp[i]);
            }
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void UpdateStateAndGenerateTemporaryBuffer(
        UInt32[] stateToModify, UInt32[] workingBuffer, Byte[] temporaryBuffer)
    {
        // Copy state to working buffer
        Buffer.BlockCopy(stateToModify, 0, workingBuffer, 0, StateLength * sizeof(UInt32));

        for (Int32 i = 0; i < 10; i++) // 20 rounds (10 double rounds)
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

        for (Int32 i = 0; i < StateLength; i++)
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
    /// <param name="a">Index of the first TransportProtocol</param>
    /// <param name="b">Index of the second TransportProtocol</param>
    /// <param name="c">Index of the third TransportProtocol</param>
    /// <param name="d">Index of the fourth TransportProtocol</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void QuarterRound(UInt32[] x, UInt32 a, UInt32 b, UInt32 c, UInt32 d)
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
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    ~ChaCha20() => Dispose(false);

    /// <summary>
    /// Clear and dispose of the internal state. Also request the GC not to call the finalizer, because all cleanup has been taken care of.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Dispose()
    {
        Dispose(true);
        /*
			* The Garbage Collector does not need to invoke the finalizer because Dispose(bool) has already done all the cleanup needed.
			*/
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// This method should only be invoked from Dispose() or the finalizer. This handles the actual cleanup of the resources.
    /// </summary>
    /// <param name="disposing">
    /// Should be true if called by Dispose(); false if called by the finalizer
    /// </param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void Dispose(Boolean disposing)
    {
        if (!_isDisposed)
        {
            if (disposing)
            {
                /* Cleanup managed objects by calling their Dispose() methods */
            }

            /* Cleanup any unmanaged objects here */
            Array.Clear(State, 0, StateLength);
        }

        _isDisposed = true;
    }

    #endregion Destructor and Disposer
}