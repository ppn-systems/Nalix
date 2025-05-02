using Nalix.Common.Cryptography.Hashing;
using Nalix.Cryptography.Hashing;
using Nalix.Cryptography.Utils;
using System;
using System.Runtime.CompilerServices;

namespace Nalix.Cryptography.Mac;

/// <summary>
/// Provides implementations of Hash-based Message Authentication Codes (HMAC).
/// </summary>
/// <remarks>
/// HMAC is a specific type of message authentication code (MAC) involving a cryptographic
/// hash function and a secret cryptographic key. It provides a way to verify both the data
/// integrity and the authentication of a message.
/// </remarks>
public sealed class Hmac : IDisposable
{
    #region Constants

    private const int Sha1BlockSize = 64;
    private const int Sha1HashSize = 20;
    private const int Sha256BlockSize = 64;
    private const int Sha256HashSize = 32;

    private const byte OuterPadValue = 0x5C;
    private const byte InnerPadValue = 0x36;

    #endregion Constants

    #region Fields

    private readonly byte[] _key;
    private readonly int _hashSize;
    private readonly int _blockSize;
    private readonly HashAlgorithm _algorithm;

    private bool _disposed;

    #endregion Fields

    #region Constructors

    /// <summary>
    /// Initializes a new instance of the <see cref="Hmac"/> class with the specified key and algorithm.
    /// </summary>
    /// <param name="key">The secret key for HMAC generation.</param>
    /// <param name="algorithm">The hash algorithm to use.</param>
    /// <exception cref="ArgumentNullException">Thrown when the key is null.</exception>
    /// <exception cref="ArgumentException">Thrown when the key is empty.</exception>
    public Hmac(ReadOnlySpan<byte> key, HashAlgorithm algorithm = HashAlgorithm.Sha256)
    {
        if (key.IsEmpty)
            throw new ArgumentException("HMAC key cannot be empty", nameof(key));

        _algorithm = algorithm;

        // Set block size and hash size based on algorithm
        (_blockSize, _hashSize) = algorithm switch
        {
            HashAlgorithm.Sha1 => (Sha1BlockSize, Sha1HashSize),
            HashAlgorithm.Sha256 => (Sha256BlockSize, Sha256HashSize),
            _ => throw new ArgumentException("Unsupported hash algorithm", nameof(algorithm))
        };

        // Process the key
        _key = PrepareKey(key);
        _disposed = false;
    }

    #endregion Constructors

    #region Public Methods

    /// <summary>
    /// Creates a one-time use HMAC and computes the result in a single operation.
    /// </summary>
    /// <param name="key">The secret key for HMAC generation.</param>
    /// <param name="data">The message to authenticate.</param>
    /// <param name="algorithm">The hash algorithm to use.</param>
    /// <returns>A byte array containing the computed HMAC.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static byte[] ComputeHash(
        ReadOnlySpan<byte> key, ReadOnlySpan<byte> data,
        HashAlgorithm algorithm = HashAlgorithm.Sha256)
    {
        using Hmac hmac = new(key, algorithm);
        return hmac.ComputeHash(data);
    }

    /// <summary>
    /// Computes the HMAC for the specified input data.
    /// </summary>
    /// <param name="data">The message to authenticate.</param>
    /// <returns>A byte array containing the computed HMAC.</returns>
    /// <exception cref="ObjectDisposedException">Thrown if the instance has been disposed.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public byte[] ComputeHash(ReadOnlySpan<byte> data)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        // Create inner and outer keys with appropriate padding
        byte[] innerKeyPad = new byte[_blockSize];
        byte[] outerKeyPad = new byte[_blockSize];

        for (int i = 0; i < _blockSize; i++)
        {
            innerKeyPad[i] = (byte)(_key[i] ^ InnerPadValue);
            outerKeyPad[i] = (byte)(_key[i] ^ OuterPadValue);
        }

        // Compute inner hash (H(Ka ⊕ ipad || message))
        byte[] innerHash = ComputeInnerHash(innerKeyPad, data);

        // Compute outer hash (H(Ka ⊕ opad || inner_hash))
        return ComputeOuterHash(outerKeyPad, innerHash);
    }

    /// <summary>
    /// Verifies if the provided HMAC matches the computed HMAC for the message.
    /// </summary>
    /// <param name="data">The message that was authenticated.</param>
    /// <param name="expectedHmac">The expected HMAC value to compare against.</param>
    /// <returns>True if the computed HMAC matches the expected HMAC; otherwise, false.</returns>
    /// <exception cref="ObjectDisposedException">Thrown if the instance has been disposed.</exception>
    /// <remarks>
    /// This method uses time-constant comparison to prevent timing attacks (tấn công thời gian).
    /// </remarks>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool VerifyHash(ReadOnlySpan<byte> data, ReadOnlySpan<byte> expectedHmac)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (expectedHmac.Length != _hashSize)
            return false;

        byte[] computedHmac = ComputeHash(data);

        // Use constant time comparison to prevent timing attacks
        return BitwiseUtils.FixedTimeEquals(computedHmac, expectedHmac);
    }

    /// <summary>
    /// Static method to verify an HMAC.
    /// </summary>
    /// <param name="key">The secret key used for HMAC generation.</param>
    /// <param name="data">The message that was authenticated.</param>
    /// <param name="expectedHmac">The expected HMAC value to compare against.</param>
    /// <param name="algorithm">The hash algorithm to use.</param>
    /// <returns>True if the computed HMAC matches the expected HMAC; otherwise, false.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool Verify(
        ReadOnlySpan<byte> key,
        ReadOnlySpan<byte> data,
        ReadOnlySpan<byte> expectedHmac,
        HashAlgorithm algorithm = HashAlgorithm.Sha256)
    {
        using Hmac hmac = new(key, algorithm);
        return hmac.VerifyHash(data, expectedHmac);
    }

    #endregion Public Methods

    #region Private Methods

    /// <summary>
    /// Prepares the key for use in HMAC by ensuring it's exactly blockSize bytes.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private byte[] PrepareKey(ReadOnlySpan<byte> key)
    {
        byte[] normalizedKey = new byte[_blockSize];

        // If key is longer than block size, hash it
        if (key.Length > _blockSize)
        {
            byte[] hashedKey;

            if (_algorithm == HashAlgorithm.Sha1)
            {
                hashedKey = SHA1.HashData(key);
            }
            else
            {
                hashedKey = SHA256.HashData(key);
            }

            Array.Copy(hashedKey, normalizedKey, Math.Min(hashedKey.Length, _blockSize));
        }
        // If key is shorter than or equal to block size, use it as is with zero padding
        else
        {
            key.CopyTo(normalizedKey.AsSpan(0, key.Length));
        }

        return normalizedKey;
    }

    /// <summary>
    /// Computes the inner hash of the HMAC function.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private byte[] ComputeInnerHash(byte[] innerKeyPad, ReadOnlySpan<byte> data)
    {
        if (_algorithm == HashAlgorithm.Sha1)
        {
            // For SHA1, we'll use the static implementation
            using var ms = new System.IO.MemoryStream();
            using var sha1 = new SHA1();

            sha1.Update(innerKeyPad);

            // Copy the data to the memory stream
            if (data.Length > 0)
            {
                byte[] dataArray = data.ToArray();
                sha1.Update(dataArray);
            }

            return sha1.FinalizeHash();
        }
        else
        {
            // For SHA256, we'll use the instance-based implementation
            using var sha256 = new SHA256();
            sha256.Update(innerKeyPad);
            sha256.Update(data);
            return sha256.FinalizeHash();
        }
    }

    /// <summary>
    /// Computes the outer hash of the HMAC function.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private byte[] ComputeOuterHash(byte[] outerKeyPad, byte[] innerHash)
    {
        if (_algorithm == HashAlgorithm.Sha1)
        {
            // For SHA1, we'll use the static implementation
            using var ms = new System.IO.MemoryStream();
            using var sha1 = new SHA1();
            sha1.Update(outerKeyPad);
            sha1.Update(innerHash);
            return sha1.FinalizeHash();
        }
        else
        {
            // For SHA256, we'll use the instance-based implementation
            using var sha256 = new SHA256();
            sha256.Update(outerKeyPad);
            sha256.Update(innerHash);
            return sha256.FinalizeHash();
        }
    }

    #endregion Private Methods

    #region IDisposable

    /// <summary>
    /// Disposes resources used by the HMAC instance.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Dispose()
    {
        if (_disposed) return;

        // Clear sensitive data
        if (_key != null)
        {
            Array.Clear(_key, 0, _key.Length);
        }

        _disposed = true;
    }

    #endregion IDisposable
}
