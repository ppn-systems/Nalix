using Nalix.Common.Security.Cryptography.Hashing;
using Nalix.Cryptography.Hashing;
using Nalix.Cryptography.Internal;
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
public sealed class Hmac : System.IDisposable
{
    #region Constants

    private const System.Byte Sha1BlockSize = 64;
    private const System.Byte Sha1HashSize = 20;

    private const System.Byte Sha224BlockSize = 64;
    private const System.Byte Sha224HashSize = 28;

    private const System.Byte Sha256BlockSize = 64;
    private const System.Byte Sha256HashSize = 32;

    private const System.Byte Sha384BlockSize = 128;
    private const System.Byte Sha384HashSize = 48;

    private const System.Byte OuterPadValue = 0x5C;
    private const System.Byte InnerPadValue = 0x36;

    #endregion Constants

    #region Fields

    private readonly System.Byte[] _key;
    private readonly System.Int32 _hashSize;
    private readonly System.Int32 _blockSize;
    private readonly HashAlgorithmType _algorithm;

    private System.Boolean _disposed;

    #endregion Fields

    #region Constructors

    /// <summary>
    /// Initializes a new instance of the <see cref="Hmac"/> class with the specified key and algorithm.
    /// </summary>
    /// <param name="key">The secret key for HMAC generation.</param>
    /// <param name="algorithm">The hash algorithm to use.</param>
    /// <exception cref="System.ArgumentNullException">Thrown when the key is null.</exception>
    /// <exception cref="System.ArgumentException">Thrown when the key is empty.</exception>
    public Hmac(
        System.ReadOnlySpan<System.Byte> key,
        HashAlgorithmType algorithm = HashAlgorithmType.Sha256)
    {
        if (key.IsEmpty)
        {
            throw new System.ArgumentException("HMAC key cannot be empty", nameof(key));
        }

        _algorithm = algorithm;

        // Set block size and hash size based on algorithm
        (_blockSize, _hashSize) = algorithm switch
        {
            HashAlgorithmType.Sha1 => (Sha1BlockSize, Sha1HashSize),
            HashAlgorithmType.Sha224 => (Sha224BlockSize, Sha224HashSize),
            HashAlgorithmType.Sha256 => (Sha256BlockSize, Sha256HashSize),
            HashAlgorithmType.Sha384 => (Sha384BlockSize, Sha384HashSize),
            _ => throw new System.ArgumentException("Unsupported hash algorithm", nameof(algorithm))
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
    public static System.Byte[] ComputeHash(
        System.ReadOnlySpan<System.Byte> key,
        System.ReadOnlySpan<System.Byte> data,
        HashAlgorithmType algorithm = HashAlgorithmType.Sha256)
    {
        using Hmac hmac = new(key, algorithm);
        return hmac.ComputeHash(data);
    }

    /// <summary>
    /// Computes the HMAC for the specified input data.
    /// </summary>
    /// <param name="data">The message to authenticate.</param>
    /// <returns>A byte array containing the computed HMAC.</returns>
    /// <exception cref="System.ObjectDisposedException">Thrown if the instance has been disposed.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public System.Byte[] ComputeHash(System.ReadOnlySpan<System.Byte> data)
    {
        System.ObjectDisposedException.ThrowIf(_disposed, this);

        // Create inner and outer keys with appropriate padding
        System.Byte[] innerKeyPad = new System.Byte[_blockSize];
        System.Byte[] outerKeyPad = new System.Byte[_blockSize];

        for (System.Int32 i = 0; i < _blockSize; i++)
        {
            innerKeyPad[i] = (System.Byte)(_key[i] ^ InnerPadValue);
            outerKeyPad[i] = (System.Byte)(_key[i] ^ OuterPadValue);
        }

        // Compute inner hash (H(Ka ⊕ ipad || message))
        System.Byte[] innerHash = ComputeInnerHash(innerKeyPad, data);

        // Compute outer hash (H(Ka ⊕ opad || inner_hash))
        return ComputeOuterHash(outerKeyPad, innerHash);
    }

    /// <summary>
    /// Verifies if the provided HMAC matches the computed HMAC for the message.
    /// </summary>
    /// <param name="data">The message that was authenticated.</param>
    /// <param name="expectedHmac">The expected HMAC value to compare against.</param>
    /// <returns>True if the computed HMAC matches the expected HMAC; otherwise, false.</returns>
    /// <exception cref="System.ObjectDisposedException">Thrown if the instance has been disposed.</exception>
    /// <remarks>
    /// This method uses time-constant comparison to prevent timing attacks (tấn công thời gian).
    /// </remarks>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public System.Boolean VerifyHash(
        System.ReadOnlySpan<System.Byte> data,
        System.ReadOnlySpan<System.Byte> expectedHmac)
    {
        System.ObjectDisposedException.ThrowIf(_disposed, this);

        if (expectedHmac.Length != _hashSize)
        {
            return false;
        }

        System.Byte[] computedHmac = ComputeHash(data);

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
    public static System.Boolean Verify(
        System.ReadOnlySpan<System.Byte> key,
        System.ReadOnlySpan<System.Byte> data,
        System.ReadOnlySpan<System.Byte> expectedHmac,
        HashAlgorithmType algorithm = HashAlgorithmType.Sha256)
    {
        using Hmac hmac = new(key, algorithm);
        return hmac.VerifyHash(data, expectedHmac);
    }

    /// <summary>
    /// Prepares the key for use in HMAC by ensuring it's exactly blockSize bytes.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private System.Byte[] PrepareKey(System.ReadOnlySpan<System.Byte> key)
    {
        System.Byte[] normalizedKey = new System.Byte[_blockSize];

        // If key is longer than block size, hash it
        if (key.Length > _blockSize)
        {
            System.Byte[] hashedKey = _algorithm switch
            {
                HashAlgorithmType.Sha1 => SHA1.HashData(key),
                HashAlgorithmType.Sha224 => SHA224.HashData(key),
                HashAlgorithmType.Sha256 => SHA256.HashData(key),
                HashAlgorithmType.Sha384 => SHA384.HashData(key),
                _ => throw new System.ArgumentException("Unsupported hash algorithm", nameof(key))
            };

            System.Array.Copy(hashedKey, normalizedKey, System.Math.Min(hashedKey.Length, _blockSize));
        }
        // If key is shorter than or equal to block size, use it as is with zero padding
        else
        {
            key.CopyTo(System.MemoryExtensions.AsSpan(normalizedKey, 0, key.Length));
        }

        return normalizedKey;
    }

    /// <summary>
    /// Computes the inner hash of the HMAC function.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [System.Diagnostics.CodeAnalysis.SuppressMessage(
        "Usage", "CA2208:Instantiate argument exceptions correctly", Justification = "<Pending>")]
    [System.Diagnostics.CodeAnalysis.SuppressMessage(
        "CodeQuality", "IDE0079:Remove unnecessary suppression", Justification = "<Pending>")]
    private System.Byte[] ComputeInnerHash(
        System.Byte[] innerKeyPad,
        System.ReadOnlySpan<System.Byte> data)
    {
        switch (_algorithm)
        {
            case HashAlgorithmType.Sha1:
                {
                    using SHA1 sha1 = new();
                    sha1.Update(innerKeyPad);
                    sha1.Update(data);
                    return sha1.FinalizeHash();
                }

            case HashAlgorithmType.Sha224:
                {
                    using SHA224 sha224 = new();
                    sha224.Update(innerKeyPad);
                    sha224.Update(data);
                    return sha224.FinalizeHash();
                }

            case HashAlgorithmType.Sha256:
                {
                    using SHA256 sha256 = new();
                    sha256.Update(innerKeyPad);
                    sha256.Update(data);
                    return sha256.FinalizeHash();
                }

            case HashAlgorithmType.Sha384:
                {
                    using SHA384 sha384 = new();
                    sha384.Update(innerKeyPad);
                    sha384.Update(data);
                    return sha384.FinalizeHash();
                }

            default:
                throw new System.ArgumentException("Unsupported hash algorithm", nameof(_algorithm));
        }
    }

    /// <summary>
    /// Computes the outer hash of the HMAC function.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [System.Diagnostics.CodeAnalysis.SuppressMessage(
        "Usage", "CA2208:Instantiate argument exceptions correctly", Justification = "<Pending>")]
    [System.Diagnostics.CodeAnalysis.SuppressMessage(
        "CodeQuality", "IDE0079:Remove unnecessary suppression", Justification = "<Pending>")]
    private System.Byte[] ComputeOuterHash(
        System.Byte[] outerKeyPad,
        System.Byte[] innerHash)
    {
        switch (_algorithm)
        {
            case HashAlgorithmType.Sha1:
                {
                    using SHA1 sha1 = new();
                    sha1.Update(outerKeyPad);
                    sha1.Update(innerHash);
                    return sha1.FinalizeHash();
                }

            case HashAlgorithmType.Sha224:
                {
                    using SHA224 sha224 = new();
                    sha224.Update(outerKeyPad);
                    sha224.Update(innerHash);
                    return sha224.FinalizeHash();
                }

            case HashAlgorithmType.Sha256:
                {
                    using SHA256 sha256 = new();
                    sha256.Update(outerKeyPad);
                    sha256.Update(innerHash);
                    return sha256.FinalizeHash();
                }

            case HashAlgorithmType.Sha384:
                {
                    using SHA384 sha384 = new();
                    sha384.Update(outerKeyPad);
                    sha384.Update(innerHash);
                    return sha384.FinalizeHash();
                }

            default:
                throw new System.ArgumentException("Unsupported hash algorithm", nameof(_algorithm));
        }
    }

    #endregion Public Methods

    #region IDisposable

    /// <summary>
    /// Disposes resources used by the HMAC instance.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        // Clear sensitive data
        if (_key != null)
        {
            System.Array.Clear(_key, 0, _key.Length);
        }

        _disposed = true;
    }

    #endregion IDisposable
}
