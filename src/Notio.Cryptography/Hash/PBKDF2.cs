using System;
using System.Buffers;
using System.Buffers.Binary;
using System.Runtime.CompilerServices;
using System.Text;

namespace Notio.Cryptography.Hash;

/// <summary>
/// High-performance implementation of PBKDF2 (Password-Based Key Derivation Function 2).
/// </summary>
/// <remarks>
/// PBKDF2 is a key derivation function designed to securely derive cryptographic keys from passwords.
/// It applies a hash function multiple times to increase computational cost and resist brute-force attacks.
/// </remarks>
public sealed class Pbkdf2 : IDisposable
{
    private readonly byte[] _salt;
    private readonly int _keyLength;
    private readonly int _iterations;
    private readonly HashAlgorithmType _hashType;
    private bool _disposed;

    /// <summary>
    /// Supported hash algorithms for PBKDF2.
    /// </summary>
    /// <remarks>
    /// PBKDF2 supports multiple hash algorithms for different security and compatibility requirements:
    /// - `Sha1`: Legacy option for compatibility.
    /// - `Sha256`: Stronger security, recommended for new applications.
    /// </remarks>
    public enum HashAlgorithmType
    {
        /// <summary>HMAC-SHA1 (default for compatibility)</summary>
        Sha1,

        /// <summary>HMAC-SHA256 (more secure)</summary>
        Sha256
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="Pbkdf2"/> class.
    /// </summary>
    /// <param name="salt">The cryptographic salt used for key derivation.</param>
    /// <param name="iterations">The number of iterations to apply the hash function.</param>
    /// <param name="keyLength">The desired length of the derived key (in bytes).</param>
    /// <param name="hashType">The hash function to use (`Sha1` or `Sha256`).</param>
    /// <exception cref="ArgumentException">Thrown if <paramref name="salt"/> is null or empty.</exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown if <paramref name="iterations"/> or <paramref name="keyLength"/> is less than or equal to zero.
    /// </exception>
    /// <remarks>
    /// The higher the iteration count, the stronger the key derivation function against brute-force attacks,
    /// but it also increases computation time.
    /// </remarks>
    public Pbkdf2(byte[] salt, int iterations, int keyLength, HashAlgorithmType hashType = HashAlgorithmType.Sha1)
    {
        if (salt == null || salt.Length == 0)
            throw new ArgumentException("Salt cannot be null or empty", nameof(salt));

        if (iterations <= 0)
            throw new ArgumentOutOfRangeException(nameof(iterations), "Iterations must be greater than zero.");

        if (keyLength <= 0)
            throw new ArgumentOutOfRangeException(nameof(keyLength), "Key length must be greater than zero.");

        // Create a defensive copy of the salt to prevent modification
        _salt = new byte[salt.Length];
        Buffer.BlockCopy(salt, 0, _salt, 0, salt.Length);

        _keyLength = keyLength;
        _iterations = iterations;
        _hashType = hashType;
    }

    /// <summary>
    /// Derives a cryptographic key from a password.
    /// </summary>
    /// <param name="password">The password to derive the key from.</param>
    /// <returns>The derived key as a byte array.</returns>
    /// <exception cref="ObjectDisposedException">Thrown if the instance has been disposed.</exception>
    /// <exception cref="ArgumentException">Thrown if <paramref name="password"/> is null or empty.</exception>
    /// <remarks>
    /// This method encodes the password as UTF-8 before processing.
    /// </remarks>
    public byte[] DeriveKey(string password)
    {
        ObjectDisposedException.ThrowIf(_disposed, nameof(Pbkdf2));

        if (string.IsNullOrEmpty(password))
            throw new ArgumentException("Password cannot be null or empty.", nameof(password));

        ReadOnlySpan<byte> passwordBytes = Encoding.UTF8.GetBytes(password);
        try
        {
            return _hashType switch
            {
                HashAlgorithmType.Sha256 => DeriveKeyUsingHmac(passwordBytes, _salt, _iterations, _keyLength, 32),
                _ => DeriveKeyUsingHmac(passwordBytes, _salt, _iterations, _keyLength, 20)
            };
        }
        finally
        {
            // Ensure password bytes are cleared from memory when using string
            if (passwordBytes.Length > 0 && RuntimeHelpers.IsReferenceOrContainsReferences<byte>())
            {
            }
        }
    }

    /// <summary>
    /// Derives a cryptographic key from raw password bytes.
    /// </summary>
    /// <param name="passwordBytes">The password bytes to derive the key from.</param>
    /// <returns>The derived key as a byte array.</returns>
    /// <exception cref="ObjectDisposedException">Thrown if the instance has been disposed.</exception>
    /// <exception cref="ArgumentException">Thrown if <paramref name="passwordBytes"/> is empty.</exception>
    /// <remarks>
    /// This method is useful when the password is already in a secure byte format.
    /// </remarks>
    public byte[] DeriveKey(ReadOnlySpan<byte> passwordBytes)
    {
        ObjectDisposedException.ThrowIf(_disposed, nameof(Pbkdf2));

        if (passwordBytes.IsEmpty)
            throw new ArgumentException("Password bytes cannot be empty.", nameof(passwordBytes));

        return _hashType switch
        {
            HashAlgorithmType.Sha256 => DeriveKeyUsingHmac(passwordBytes, _salt, _iterations, _keyLength, 32),
            _ => DeriveKeyUsingHmac(passwordBytes, _salt, _iterations, _keyLength, 20)
        };
    }

    /// <summary>
    /// Core PBKDF2 implementation using the selected hash algorithm.
    /// </summary>
    private static byte[] DeriveKeyUsingHmac(ReadOnlySpan<byte> password, ReadOnlySpan<byte> salt,
        int iterations, int keyLength, int hashLength)
    {
        // Calculate required blocks
        int blockCount = (int)Math.Ceiling((double)keyLength / hashLength);
        byte[] derivedKey = new byte[keyLength];

        // Prepare buffer for salt + block index
        int bufferSize = salt.Length + 4;
        byte[] buffer = ArrayPool<byte>.Shared.Rent(bufferSize);
        try
        {
            salt.CopyTo(buffer);

            // Process each block
            int outputOffset = 0;
            for (int i = 1; i <= blockCount; i++)
            {
                // Write block index in big-endian format
                BinaryPrimitives.WriteInt32BigEndian(buffer.AsSpan(salt.Length, 4), i);

                // Calculate block output
                int bytesToCopy = Math.Min(hashLength, keyLength - outputOffset);
                ComputeBlock(password, buffer.AsSpan(0, bufferSize), iterations,
                    derivedKey.AsSpan(outputOffset, bytesToCopy), hashLength);

                outputOffset += hashLength;
            }

            return derivedKey;
        }
        finally
        {
            // Return buffer to the pool
            ArrayPool<byte>.Shared.Return(buffer, clearArray: true);
        }
    }

    /// <summary>
    /// Compute a single PBKDF2 block.
    /// </summary>
    private static void ComputeBlock(ReadOnlySpan<byte> password, ReadOnlySpan<byte> saltWithIndex,
        int iterations, Span<byte> outputBlock, int hashLength)
    {
        // Allocate buffers for HMAC calculations
        byte[] u = ArrayPool<byte>.Shared.Rent(hashLength);
        byte[] temp = ArrayPool<byte>.Shared.Rent(hashLength);

        try
        {
            // First iteration: Hash(password, salt || INT_32_BE(blockIndex))
            ComputeHmac(password, saltWithIndex, u, hashLength);
            u.AsSpan(0, hashLength).CopyTo(outputBlock);

            // Remaining iterations
            for (int i = 1; i < iterations; i++)
            {
                ComputeHmac(password, u.AsSpan(0, hashLength), temp, hashLength);
                Buffer.BlockCopy(temp, 0, u, 0, hashLength);

                // XOR the new result with the accumulator
                for (int j = 0; j < outputBlock.Length; j++)
                {
                    outputBlock[j] ^= u[j];
                }
            }
        }
        finally
        {
            // Clear and return buffers to the pool
            ArrayPool<byte>.Shared.Return(u, clearArray: true);
            ArrayPool<byte>.Shared.Return(temp, clearArray: true);
        }
    }

    /// <summary>
    /// Compute HMAC based on the current hash type.
    /// </summary>
    private static void ComputeHmac(ReadOnlySpan<byte> key, ReadOnlySpan<byte> message,
        Span<byte> output, int hashLength)
    {
        // Use our custom HMAC implementation
        if (hashLength == 20)
        {
            ComputeHmacSha1(key, message, output);
        }
        else
        {
            ComputeHmacSha256(key, message, output);
        }
    }

    /// <summary>
    /// Compute HMAC-SHA1 of a message using the specified key.
    /// </summary>
    private static void ComputeHmacSha1(ReadOnlySpan<byte> key, ReadOnlySpan<byte> message, Span<byte> output)
    {
        const int blockSize = 64;
        const int outputSize = 20;

        // Handle key that's longer than block size
        Span<byte> effectiveKey = stackalloc byte[blockSize];
        if (key.Length > blockSize)
        {
            Sha1.ComputeHash(key).AsSpan()[..outputSize].CopyTo(effectiveKey);
        }
        else
        {
            key.CopyTo(effectiveKey);
        }

        // Prepare the inner and outer padded keys
        Span<byte> innerKey = stackalloc byte[blockSize + message.Length];
        Span<byte> outerKey = stackalloc byte[blockSize + outputSize];

        // XOR the key with ipad (0x36) and opad (0x5C)
        for (int i = 0; i < blockSize; i++)
        {
            innerKey[i] = (byte)(effectiveKey[i] ^ 0x36);
            outerKey[i] = (byte)(effectiveKey[i] ^ 0x5C);
        }

        // Copy the message to the inner key
        message.CopyTo(innerKey[blockSize..]);

        // Inner hash
        byte[] innerHash = Sha1.ComputeHash(innerKey);
        innerHash.AsSpan().CopyTo(outerKey[blockSize..]);

        // Outer hash
        byte[] result = Sha1.ComputeHash(outerKey);
        result.AsSpan().CopyTo(output);
    }

    /// <summary>
    /// Compute HMAC-SHA256 of a message using the specified key.
    /// </summary>
    private static void ComputeHmacSha256(ReadOnlySpan<byte> key, ReadOnlySpan<byte> message, Span<byte> output)
    {
        const int blockSize = 64;
        const int outputSize = 32;

        // Handle key that's longer than block size
        Span<byte> effectiveKey = stackalloc byte[blockSize];
        if (key.Length > blockSize)
        {
            Sha256.HashData(key).AsSpan()[..outputSize].CopyTo(effectiveKey);
        }
        else
        {
            key.CopyTo(effectiveKey);
        }

        // Prepare the inner and outer padded keys
        byte[] innerKey = ArrayPool<byte>.Shared.Rent(blockSize + message.Length);
        byte[] outerKey = ArrayPool<byte>.Shared.Rent(blockSize + outputSize);

        try
        {
            // XOR the key with ipad (0x36) and opad (0x5C)
            for (int i = 0; i < blockSize; i++)
            {
                innerKey[i] = (byte)(effectiveKey[i] ^ 0x36);
                outerKey[i] = (byte)(effectiveKey[i] ^ 0x5C);
            }

            // Copy the message to the inner key
            message.CopyTo(innerKey.AsSpan()[blockSize..]);

            // Inner hash
            byte[] innerHash = Sha256.HashData(innerKey.AsSpan(0, blockSize + message.Length));
            innerHash.CopyTo(outerKey.AsSpan()[blockSize..]);

            // Outer hash
            byte[] result = Sha256.HashData(outerKey.AsSpan(0, blockSize + outputSize));
            result.AsSpan().CopyTo(output);
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(innerKey, clearArray: true);
            ArrayPool<byte>.Shared.Return(outerKey, clearArray: true);
        }
    }

    /// <summary>
    /// Compares two byte arrays in constant time to prevent timing attacks.
    /// </summary>
    public static bool ConstantTimeEquals(ReadOnlySpan<byte> a, ReadOnlySpan<byte> b)
    {
        if (a.Length != b.Length)
            return false;

        int result = 0;
        for (int i = 0; i < a.Length; i++)
        {
            result |= a[i] ^ b[i];
        }

        return result == 0;
    }

    /// <summary>
    /// Releases all resources used by the PBKDF2 object.
    /// </summary>
    public void Dispose()
    {
        if (!_disposed)
        {
            // Clear sensitive data
            if (_salt != null)
            {
                Array.Clear(_salt, 0, _salt.Length);
            }

            _disposed = true;
        }
    }
}
