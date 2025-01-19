using Notio.Common.Exceptions;
using Notio.Cryptography.Mode;
using System;
using System.Buffers;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Security.Cryptography;

namespace Notio.Cryptography;

/// <summary>
/// Provides AES-256 encryption and decryption utilities with CTR and CFB modes.
/// </summary>
public static class Aes256
{
    /// <summary>
    /// Represents a memory buffer that manages encrypted or decrypted data.
    /// </summary>
    /// <param name="owner">The memory owner providing the buffer.</param>
    /// <param name="length">The length of the buffer.</param>
    public sealed class MemoryBuffer(IMemoryOwner<byte> owner, int length) : IDisposable
    {
        private readonly IMemoryOwner<byte> _owner = owner;
        private bool _disposed;

        /// <summary>
        /// Gets the memory associated with this buffer.
        /// </summary>
        /// <exception cref="ObjectDisposedException">Thrown if the buffer is accessed after disposal.</exception>
        public Memory<byte> Memory => _disposed
            ? throw new ObjectDisposedException(nameof(MemoryBuffer))
            : _owner.Memory;

        /// <summary>
        /// Gets or sets the length of the buffer.
        /// </summary>
        public int Length = length;

        /// <summary>
        /// Releases the resources used by this buffer.
        /// </summary>
        /// <param name="disposing">Indicates whether the method is called from Dispose or a finalizer.</param>
        public void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                if (disposing)
                {
                    _owner.Memory.Span.Clear();
                    _owner.Dispose();
                }
                _disposed = true;
            }
        }

        /// <summary>
        /// Releases all resources used by this buffer.
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }
    }

    internal const int MinParallelSize = 1024 * 64; // 64KB threshold cho xử lý song song
    internal const int BufferSize = 81920; // 80KB buffer for better performance
    internal const int BlockSize = 16;  // AES block size in bytes
    internal const int KeySize = 32;    // AES-256 key size in bytes
    internal static readonly ArrayPool<byte> Pool = ArrayPool<byte>.Shared;

    /// <summary>
    /// Provides AES encryption and decryption in CTR mode.
    /// </summary>
    public static class CtrMode
    {
        /// <summary>
        /// Encrypts data using AES-256 in CTR mode.
        /// </summary>
        /// <param name="key">The encryption key.</param>
        /// <param name="plaintext">The plaintext data to encrypt.</param>
        /// <returns>A memory buffer containing the ciphertext.</returns>
        public static MemoryBuffer Encrypt(ReadOnlySpan<byte> key, ReadOnlySpan<byte> plaintext)
            => AesCtrCipher.Encrypt(key, plaintext);

        /// <summary>
        /// Decrypts data using AES-256 in CTR mode.
        /// </summary>
        /// <param name="key">The decryption key.</param>
        /// <param name="ciphertext">The ciphertext data to decrypt.</param>
        /// <returns>A memory buffer containing the plaintext.</returns>
        public static MemoryBuffer Decrypt(ReadOnlySpan<byte> key, ReadOnlySpan<byte> ciphertext)
            => AesCtrCipher.Decrypt(key, ciphertext);
    }

    /// <summary>
    /// Provides AES encryption and decryption in CFB mode.
    /// </summary>
    public static class CfbMode
    {
        /// <summary>
        /// Encrypts data using AES-256 in CFB mode.
        /// </summary>
        /// <param name="key">The encryption key.</param>
        /// <param name="plaintext">The plaintext data to encrypt.</param>
        /// <returns>A memory buffer containing the ciphertext.</returns>
        public static MemoryBuffer Encrypt(ReadOnlySpan<byte> key, ReadOnlySpan<byte> plaintext)
            => AesCfbCipher.Encrypt(key, plaintext);

        /// <summary>
        /// Decrypts data using AES-256 in CFB mode.
        /// </summary>
        /// <param name="key">The decryption key.</param>
        /// <param name="ciphertext">The ciphertext data to decrypt.</param>
        /// <returns>A memory buffer containing the plaintext.</returns>
        public static MemoryBuffer Decrypt(ReadOnlySpan<byte> key, ReadOnlySpan<byte> ciphertext)
            => AesCfbCipher.Decrypt(key, ciphertext);
    }

    /// <summary>
    /// Generates a new AES-256 encryption key.
    /// </summary>
    /// <returns>A 256-bit key.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static byte[] GenerateKey()
    {
        try
        {
            using var aes = Aes.Create();
            aes.KeySize = Aes256.KeySize * 8; // Convert bytes to bits
            aes.GenerateKey();
            return aes.Key;
        }
        catch (Exception ex)
        {
            throw new CryptoOperationException("Failed to generate encryption key", ex);
        }
    }

    /// <summary>
    /// Validates the provided encryption key.
    /// </summary>
    /// <param name="key">The key to validate.</param>
    /// <exception cref="ArgumentNullException">Thrown if the key is null or empty.</exception>
    /// <exception cref="ArgumentException">Thrown if the key length is invalid.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void ValidateKey(ReadOnlySpan<byte> key)
    {
        if (key.IsEmpty)
            throw new ArgumentNullException(nameof(key), "Encryption key cannot be null or empty");
        if (key.Length != KeySize)
            throw new ArgumentException($"Key must be {KeySize} bytes for AES-256", nameof(key));
    }

    /// <summary>
    /// Validates input data for encryption or decryption.
    /// </summary>
    /// <param name="data">The data to validate.</param>
    /// <param name="paramName">The parameter name for error messages.</param>
    /// <exception cref="ArgumentNullException">Thrown if the data is null or empty.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void ValidateInput(ReadOnlySpan<byte> data, string paramName)
    {
        if (data.IsEmpty)
            throw new ArgumentNullException(paramName, "Input data cannot be null or empty");
    }

    /// <summary>
    /// Generates a secure initialization vector (IV).
    /// </summary>
    /// <param name="iv">The span to store the generated IV.</param>
    /// <exception cref="ArgumentException">Thrown if the IV length is invalid.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void GenerateSecureIVInternal(Span<byte> iv)
    {
        if (iv.Length != BlockSize)
            throw new ArgumentException($"IV must be {BlockSize} bytes", nameof(iv));

        try
        {
            using RandomNumberGenerator rng = RandomNumberGenerator.Create();
            rng.GetBytes(iv);
        }
        catch
        {
            for (int i = 0; i < iv.Length; i++)
                iv[i] = (byte)(DateTime.UtcNow.Ticks >> (i % 8) * 8);
        }
    }

    /// <summary>
    /// Generates a secure initialization vector (IV) as a byte array.
    /// </summary>
    /// <returns>The generated IV.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static byte[] GenerateSecureIV()
    {
        byte[] iv = new byte[BlockSize];
        GenerateSecureIVInternal(iv);
        return iv;
    }

    /// <summary>
    /// Generates a secure initialization vector (IV).
    /// </summary>
    /// <param name="iv">The span to store the generated IV.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void GenerateSecureIV(Span<byte> iv)
        => GenerateSecureIVInternal(iv);

    /// <summary>
    /// Increments a counter value used in CTR mode encryption.
    /// </summary>
    /// <param name="counter">The counter to increment.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void IncrementCounter(Span<byte> counter)
    {
        for (int i = counter.Length - 1; i >= 0; i--)
        {
            if (++counter[i] != 0) break;
        }
    }

    /// <summary>
    /// XORs a data block with a counter value.
    /// </summary>
    /// <param name="data">The data block to modify.</param>
    /// <param name="counter">The counter value to XOR with the data.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void XorBlock(Span<byte> data, ReadOnlySpan<byte> counter)
    {
        ref byte dataRef = ref MemoryMarshal.GetReference(data);
        ref byte counterRef = ref MemoryMarshal.GetReference(counter);

        if (Vector.IsHardwareAccelerated && data.Length >= Vector<byte>.Count)
        {
            int vectorSize = Vector<byte>.Count;

            for (int i = 0; i <= data.Length - vectorSize; i += vectorSize)
            {
                Span<byte> dataSlice = data.Slice(i, vectorSize);
                ReadOnlySpan<byte> counterSlice = counter.Slice(i, vectorSize);

                Vector<byte> dataVec = new(dataSlice);
                Vector<byte> counterVec = new(counterSlice);
                (dataVec ^ counterVec).CopyTo(dataSlice);
            }
        }
        else
        {
            for (int i = 0; i < data.Length; i++)
            {
                Unsafe.Add(ref dataRef, i) ^= Unsafe.Add(ref counterRef, i);
            }
        }
    }
}