// Copyright (c) 2025 PPN Corporation. All rights reserved.

namespace Nalix.Cryptography.Symmetric.Block;


/// <summary>
/// Provides an implementation of the XTEA (eXtended Tiny Encryption Algorithm) block cipher.
/// </summary>
/// <remarks>
/// <para>
/// XTEA operates on 64-bit blocks with a 128-bit key and typically uses 64 Feistel rounds
/// (commonly referred to as 32 cycles). This implementation targets performance by using
/// unsafe pointer arithmetic and direct 32-bit word operations.
/// </para>
/// <para>
/// <strong>Padding:</strong> This API does not perform padding. Callers must ensure that
/// input lengths are exact multiples of <see cref="BlockSize"/> (8 bytes).
/// </para>
/// <para>
/// <strong>Endianness:</strong> The implementation treats two 32-bit words per block using the
/// platform's native endianness. It assumes little-endian architectures (e.g., x86/x64/ARM64 on Windows/Linux).
/// Interoperability with big-endian systems requires explicit byte-order handling by the caller.
/// </para>
/// <para>
/// <strong>Thread Safety:</strong> All methods are static and stateless; the type is thread-safe
/// as long as callers provide non-overlapping buffers for concurrent operations.
/// </para>
/// <para>
/// <strong>Security Note:</strong> XTEA is considered a legacy cipher and is generally not recommended for
/// new designs. Prefer modern, authenticated encryption such as
/// <c>AES-GCM</c> or <c>ChaCha20-Poly1305</c> where possible.
/// </para>
/// </remarks>
public static class Xtea
{
    #region Constants

    /// <summary>
    /// Gets the key size in bytes (128 bits).
    /// </summary>
    public const System.Byte KeySize = 16;

    /// <summary>
    /// Gets the block size in bytes (64 bits).
    /// </summary>
    public const System.Byte BlockSize = 8;

    /// <summary>
    /// Gets the default number of Feistel rounds for XTEA (commonly 64).
    /// </summary>
    /// <remarks>
    /// While XTEA is often described as 32 cycles, each cycle comprises two Feistel rounds.
    /// This constant reflects the total rounds (64).
    /// </remarks>
    public const System.Byte DefaultRounds = 64;

    /// <summary>
    /// The XTEA delta constant used to update the round sum.
    /// </summary>
    private const System.UInt32 Delta = 0x9E3779B9;

    #endregion Constants

    #region Public API Methods

    /// <summary>
    /// Encrypts data using the XTEA algorithm and returns a new array containing the ciphertext.
    /// </summary>
    /// <param name="plaintext">The input data to encrypt. Length must be a multiple of <see cref="BlockSize"/>.</param>
    /// <param name="key">The 128-bit encryption key (exactly <see cref="KeySize"/> bytes).</param>
    /// <param name="rounds">The number of Feistel rounds. Default is <see cref="DefaultRounds"/>.</param>
    /// <returns>A new array containing the ciphertext. Its length equals <paramref name="plaintext"/> length.</returns>
    /// <exception cref="System.ArgumentException">
    /// Thrown when <paramref name="plaintext"/> length is not a multiple of <see cref="BlockSize"/>,
    /// or when <paramref name="key"/> length is not exactly <see cref="KeySize"/>.
    /// </exception>
    /// <remarks>
    /// This overload internally rents a temporary buffer to perform the operation, then returns a right-sized array.
    /// </remarks>
    public static System.Byte[] Encrypt(
        System.ReadOnlySpan<System.Byte> plaintext,
        System.ReadOnlySpan<System.Byte> key,
        System.Byte rounds = DefaultRounds)
    {
        AssertInputSizes(plaintext, key);

        // rent a buffer (thuê bộ đệm) at least plaintext.Length
        System.Byte[] rented = System.Buffers.ArrayPool<System.Byte>.Shared.Rent(plaintext.Length);
        try
        {
            System.Int32 written = Encrypt(
                plaintext, key, System.MemoryExtensions.AsSpan(rented, 0, plaintext.Length), rounds);

            // copy out only the used portion
            System.Byte[] result = new System.Byte[written];
            System.Buffer.BlockCopy(rented, 0, result, 0, written);
            return result;
        }
        finally
        {
            System.Buffers.ArrayPool<System.Byte>.Shared.Return(rented);
        }
    }

    /// <summary>
    /// Decrypts data using the XTEA algorithm and returns a new array containing the plaintext.
    /// </summary>
    /// <param name="ciphertext">The data to decrypt. Length must be a multiple of <see cref="BlockSize"/>.</param>
    /// <param name="key">The 128-bit decryption key (exactly <see cref="KeySize"/> bytes).</param>
    /// <param name="rounds">The number of Feistel rounds. Default is <see cref="DefaultRounds"/>.</param>
    /// <returns>A new array containing the plaintext. Its length equals <paramref name="ciphertext"/> length.</returns>
    /// <exception cref="System.ArgumentException">
    /// Thrown when <paramref name="ciphertext"/> length is not a multiple of <see cref="BlockSize"/>,
    /// or when <paramref name="key"/> length is not exactly <see cref="KeySize"/>.
    /// </exception>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public static unsafe System.Byte[] Decrypt(
        System.ReadOnlySpan<System.Byte> ciphertext,
        System.ReadOnlySpan<System.Byte> key,
        System.Byte rounds = DefaultRounds)
    {
        AssertInputSizes(ciphertext, key);

        System.Byte[] plaintext = new System.Byte[ciphertext.Length];
        fixed (System.Byte* keyPtr = key)
        fixed (System.Byte* ciphertextPtr = ciphertext)
        fixed (System.Byte* plaintextPtr = plaintext)
        {
            System.UInt32* keyWords = (System.UInt32*)keyPtr;

            // Process each 8-byte block
            for (System.UInt32 i = 0; i < ciphertext.Length; i += BlockSize)
            {
                // Get pointers to current block
                System.UInt32* inputBlock = (System.UInt32*)(ciphertextPtr + i);
                System.UInt32* outputBlock = (System.UInt32*)(plaintextPtr + i);

                // Copy input values
                System.UInt32 v0 = inputBlock[0];
                System.UInt32 v1 = inputBlock[1];

                // Decrypt the block
                DecryptBlock(ref v0, ref v1, keyWords, rounds);

                // Store results
                outputBlock[0] = v0;
                outputBlock[1] = v1;
            }
        }

        return plaintext;
    }

    /// <summary>
    /// Encrypts data using the XTEA algorithm and writes the ciphertext into the provided output buffer.
    /// </summary>
    /// <param name="plaintext">The input data to encrypt. Length must be a multiple of <see cref="BlockSize"/>.</param>
    /// <param name="key">The 128-bit encryption key (exactly <see cref="KeySize"/> bytes).</param>
    /// <param name="output">The destination buffer for the ciphertext. Must be at least <c>plaintext.Length</c> bytes.</param>
    /// <param name="rounds">The number of Feistel rounds. Default is <see cref="DefaultRounds"/>.</param>
    /// <returns>The number of bytes written to <paramref name="output"/>; equals <c>plaintext.Length</c>.</returns>
    /// <exception cref="System.ArgumentException">
    /// Thrown when <paramref name="plaintext"/> length is not a multiple of <see cref="BlockSize"/>,
    /// when <paramref name="key"/> length is not exactly <see cref="KeySize"/>,
    /// or when <paramref name="output"/> is smaller than <c>plaintext.Length</c>.
    /// </exception>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public static unsafe System.Int32 Encrypt(
        System.ReadOnlySpan<System.Byte> plaintext,
        System.ReadOnlySpan<System.Byte> key,
        System.Span<System.Byte> output,
        System.Byte rounds = DefaultRounds)
    {
        AssertInputSizes(plaintext, key);

        if (output.Length < plaintext.Length)
        {
            throw new System.ArgumentException("Output buffer too small", nameof(output));
        }

        fixed (System.Byte* keyPtr = key)
        fixed (System.Byte* plaintextPtr = plaintext)
        fixed (System.Byte* outputPtr = output)
        {
            System.UInt32* keyWords = (System.UInt32*)keyPtr;

            // Process each 8-byte block
            for (System.UInt32 i = 0; i < plaintext.Length; i += BlockSize)
            {
                // Get pointers to current block
                System.UInt32* inputBlock = (System.UInt32*)(plaintextPtr + i);
                System.UInt32* outputBlock = (System.UInt32*)(outputPtr + i);

                // Copy input values
                System.UInt32 v0 = inputBlock[0];
                System.UInt32 v1 = inputBlock[1];

                // Encrypt the block
                EncryptBlock(ref v0, ref v1, keyWords, rounds);

                // Store results
                outputBlock[0] = v0;
                outputBlock[1] = v1;
            }
        }

        return plaintext.Length;
    }

    /// <summary>
    /// Decrypts data using the XTEA algorithm and writes the plaintext into the provided output buffer.
    /// </summary>
    /// <param name="ciphertext">The input data to decrypt. Length must be a multiple of <see cref="BlockSize"/>.</param>
    /// <param name="key">The 128-bit decryption key (exactly <see cref="KeySize"/> bytes).</param>
    /// <param name="output">The destination buffer for the plaintext. Must be at least <c>ciphertext.Length</c> bytes.</param>
    /// <param name="rounds">The number of Feistel rounds. Default is <see cref="DefaultRounds"/>.</param>
    /// <returns>The number of bytes written to <paramref name="output"/>; equals <c>ciphertext.Length</c>.</returns>
    /// <exception cref="System.ArgumentException">
    /// Thrown when <paramref name="ciphertext"/> length is not a multiple of <see cref="BlockSize"/>,
    /// when <paramref name="key"/> length is not exactly <see cref="KeySize"/>,
    /// or when <paramref name="output"/> is smaller than <c>ciphertext.Length</c>.
    /// </exception>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public static unsafe System.Int32 Decrypt(
        System.ReadOnlySpan<System.Byte> ciphertext,
        System.ReadOnlySpan<System.Byte> key,
        System.Span<System.Byte> output,
        System.Byte rounds = DefaultRounds)
    {
        AssertInputSizes(ciphertext, key);

        if (output.Length < ciphertext.Length)
        {
            throw new System.ArgumentException("Output buffer too small", nameof(output));
        }

        fixed (System.Byte* keyPtr = key)
        fixed (System.Byte* ciphertextPtr = ciphertext)
        fixed (System.Byte* outputPtr = output)
        {
            System.UInt32* keyWords = (System.UInt32*)keyPtr;

            // Process each 8-byte block
            for (System.Int32 i = 0; i < ciphertext.Length; i += BlockSize)
            {
                // Get pointers to current block
                System.UInt32* inputBlock = (System.UInt32*)(ciphertextPtr + i);
                System.UInt32* outputBlock = (System.UInt32*)(outputPtr + i);

                // Copy input values
                System.UInt32 v0 = inputBlock[0];
                System.UInt32 v1 = inputBlock[1];

                // Decrypt the block
                DecryptBlock(ref v0, ref v1, keyWords, rounds);

                // Store results
                outputBlock[0] = v0;
                outputBlock[1] = v1;
            }
        }

        return ciphertext.Length;
    }

    #endregion Public API Methods

    #region Core Encryption/Decryption Methods

    /// <summary>
    /// Encrypts a single 64-bit block of data using XTEA algorithm
    /// </summary>
    /// <param name="v0">First 32-bit word</param>
    /// <param name="v1">Second 32-bit word</param>
    /// <param name="key">Pointer to 128-bit key (as 4 uint values)</param>
    /// <param name="rounds">TransportProtocol of rounds (default is 32)</param>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    private static unsafe void EncryptBlock(
        ref System.UInt32 v0,
        ref System.UInt32 v1,
        System.UInt32* key,
        System.Byte rounds = DefaultRounds)
    {
        System.UInt32 sum = 0;

        for (System.Byte i = 0; i < rounds; i++)
        {
            v0 += (((v1 << 4) ^ (v1 >> 5)) + v1) ^ (sum + key[sum & 3]);
            sum += Delta;
            v1 += (((v0 << 4) ^ (v0 >> 5)) + v0) ^ (sum + key[(sum >> 11) & 3]);
        }
    }

    /// <summary>
    /// Decrypts a single 64-bit block of data using XTEA algorithm
    /// </summary>
    /// <param name="v0">First 32-bit word</param>
    /// <param name="v1">Second 32-bit word</param>
    /// <param name="key">Pointer to 128-bit key (as 4 uint values)</param>
    /// <param name="rounds">TransportProtocol of rounds (default is 32)</param>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    private static unsafe void DecryptBlock(
        ref System.UInt32 v0,
        ref System.UInt32 v1,
        System.UInt32* key,
        System.Byte rounds = DefaultRounds)
    {
        System.UInt32 sum = rounds * Delta;

        for (System.Byte i = 0; i < rounds; i++)
        {
            v1 -= (((v0 << 4) ^ (v0 >> 5)) + v0) ^ (sum + key[(sum >> 11) & 3]);
            sum -= Delta;
            v0 -= (((v1 << 4) ^ (v1 >> 5)) + v1) ^ (sum + key[sum & 3]);
        }
    }

    #endregion Core Encryption/Decryption Methods

    #region Helper Methods

    /// <summary>
    /// Validates input parameters
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    private static void AssertInputSizes(
        System.ReadOnlySpan<System.Byte> data,
        System.ReadOnlySpan<System.Byte> key)
    {
        if (data.Length % BlockSize != 0)
        {
            throw new System.ArgumentException(
                $"Data length must be a multiple of {BlockSize} bytes", nameof(data));
        }

        if (key.Length != KeySize)
        {
            throw new System.ArgumentException(
                $"Key must be exactly {KeySize} bytes", nameof(key));
        }
    }

    #endregion Helper Methods
}