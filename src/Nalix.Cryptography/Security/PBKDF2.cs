// Copyright (c) 2025 PPN Corporation. All rights reserved.

using Nalix.Common.Security.Cryptography.Enums;
using Nalix.Cryptography.Hashing;

namespace Nalix.Cryptography.Security;

/// <summary>
/// High-performance implementation of the PBKDF2 (Password-Based Key Derivation Function 2) algorithm.
/// Supports HMAC-SHA1 and HMAC-SHA256 for key derivation.
/// </summary>
public sealed class PBKDF2 : System.IDisposable
{
    #region Fields

    private readonly System.Byte[] _salt;
    private readonly System.Int32 _keyLength;
    private readonly System.Int32 _iterations;
    private readonly HashAlgorithmType _hashType;
    private System.Boolean _disposed;

    #endregion Fields

    #region Constructors

    /// <summary>
    /// Initializes a new instance of the <see cref="PBKDF2"/> class.
    /// </summary>
    /// <param name="salt">The salt value to use in the key derivation process. Must not be null or empty.</param>
    /// <param name="iterations">The TransportProtocol of iterations to perform. Must be greater than 0.</param>
    /// <param name="keyLength">The desired length of the derived key in bytes. Must be greater than 0.</param>
    /// <param name="hashType">The hash algorithm to use (SHA1 or SHA256). Environment to SHA1.</param>
    /// <exception cref="System.ArgumentException">Thrown if <paramref name="salt"/> is null or empty.</exception>
    /// <exception cref="System.ArgumentOutOfRangeException">Thrown if <paramref name="iterations"/> or <paramref name="keyLength"/> is less than or equal to 0.</exception>
    public PBKDF2(
        System.Byte[] salt, System.Int32 iterations, System.Int32 keyLength,
        HashAlgorithmType hashType = HashAlgorithmType.Sha1)
    {
        if (salt == null || salt.Length == 0)
        {
            throw new System.ArgumentException("Salt cannot be empty.", nameof(salt));
        }

        if (iterations <= 0)
        {
            throw new System.ArgumentOutOfRangeException(nameof(iterations), "Must be > 0.");
        }

        if (keyLength <= 0)
        {
            throw new System.ArgumentOutOfRangeException(nameof(keyLength), "Must be > 0.");
        }

        _salt = (System.Byte[])salt.Clone();
        _keyLength = keyLength;
        _iterations = iterations;
        _hashType = hashType;
    }

    #endregion Constructors

    #region Public Methods

    /// <summary>
    /// Derives a key from the specified password using PBKDF2 with UTF-8 encoding.
    /// </summary>
    /// <param name="password">The password to derive the key from.</param>
    /// <returns>A byte array containing the derived key.</returns>
    /// <exception cref="System.ArgumentException">Thrown if <paramref name="password"/> is null or empty.</exception>
    /// <exception cref="System.ObjectDisposedException">Thrown if the instance has been disposed.</exception>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public System.Byte[] DeriveKey(System.String password)
    {
        System.ObjectDisposedException.ThrowIf(_disposed, nameof(PBKDF2));
        if (System.String.IsNullOrEmpty(password))
        {
            throw new System.ArgumentException("Password cannot be empty.", nameof(password));
        }

        System.ReadOnlySpan<System.Byte> passwordBytes = System.Text.Encoding.UTF8.GetBytes(password);

        return _hashType switch
        {
            HashAlgorithmType.Sha1 => DeriveKeyUsingHmacSha1(passwordBytes),
            HashAlgorithmType.Sha224 => DeriveKeyUsingHmacSha224(passwordBytes),
            HashAlgorithmType.Sha256 => DeriveKeyUsingHmacSha256(passwordBytes),
            _ => throw new System.NotSupportedException($"Hash algorithm {_hashType} is not supported.")
        };
    }

    /// <summary>
    /// Derives a key from the specified password bytes using PBKDF2.
    /// </summary>
    /// <param name="passwordBytes">The password bytes to derive the key from.</param>
    /// <returns>A byte array containing the derived key.</returns>
    /// <exception cref="System.ArgumentException">Thrown if <paramref name="passwordBytes"/> is empty.</exception>
    /// <exception cref="System.ObjectDisposedException">Thrown if the instance has been disposed.</exception>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public System.Byte[] DeriveKey(System.ReadOnlySpan<System.Byte> passwordBytes)
    {
        System.ObjectDisposedException.ThrowIf(_disposed, nameof(PBKDF2));
        return passwordBytes.IsEmpty
            ? throw new System.ArgumentException("Password bytes cannot be empty.", nameof(passwordBytes))
            : _hashType switch
            {
                HashAlgorithmType.Sha1 => DeriveKeyUsingHmacSha1(passwordBytes),
                HashAlgorithmType.Sha224 => DeriveKeyUsingHmacSha224(passwordBytes),
                HashAlgorithmType.Sha256 => DeriveKeyUsingHmacSha256(passwordBytes),
                _ => throw new System.NotSupportedException($"Hash algorithm {_hashType} is not supported.")
            };
    }

    /// <summary>
    /// Releases all resources used by the <see cref="PBKDF2"/> instance and clears sensitive data.
    /// </summary>
    [System.Diagnostics.DebuggerNonUserCode]
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;

        System.Array.Clear(_salt, 0, _salt.Length);
    }

    #endregion Public Methods

    #region Private Methods

    /// <summary>
    /// Derives a key using HMAC-SHA1.
    /// </summary>
    /// <param name="password">The password bytes to derive the key from.</param>
    /// <returns>A byte array containing the derived key.</returns>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    private System.Byte[] DeriveKeyUsingHmacSha1(System.ReadOnlySpan<System.Byte> password)
        => DeriveKeyUsingHmac(password, _salt, _iterations, _keyLength, 20, ComputeHmacSha1);

    /// <summary>
    /// Derives a key using HMAC-SHA224.
    /// </summary>
    /// <param name="password">The password bytes to derive the key from.</param>
    /// <returns>A byte array containing the derived key.</returns>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    private System.Byte[] DeriveKeyUsingHmacSha224(System.ReadOnlySpan<System.Byte> password)
        => DeriveKeyUsingHmac(password, _salt, _iterations, _keyLength, 28, ComputeHmacSha224);

    /// <summary>
    /// Derives a key using HMAC-SHA256.
    /// </summary>
    /// <param name="password">The password bytes to derive the key from.</param>
    /// <returns>A byte array containing the derived key.</returns>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    private System.Byte[] DeriveKeyUsingHmacSha256(System.ReadOnlySpan<System.Byte> password)
        => DeriveKeyUsingHmac(password, _salt, _iterations, _keyLength, 32, ComputeHmacSha256);

    /// <summary>
    /// Core implementation of PBKDF2 key derivation using the specified HMAC function.
    /// </summary>
    /// <param name="password">The password bytes to derive the key from.</param>
    /// <param name="salt">The salt bytes to use.</param>
    /// <param name="iterations">The TransportProtocol of iterations to perform.</param>
    /// <param name="keyLength">The desired key length in bytes.</param>
    /// <param name="hashLength">The length of the hash output (20 for SHA1, 32 for SHA256).</param>
    /// <param name="computeHmac">The HMAC computation function to use.</param>
    /// <returns>A byte array containing the derived key.</returns>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    private static System.Byte[] DeriveKeyUsingHmac(
         System.ReadOnlySpan<System.Byte> password, System.ReadOnlySpan<System.Byte> salt,
        System.Int32 iterations, System.Int32 keyLength, System.Int32 hashLength,
         System.Action<System.ReadOnlySpan<System.Byte>, System.ReadOnlySpan<System.Byte>, System.Span<System.Byte>> computeHmac)
    {
        System.Int32 blockCount = (keyLength + hashLength - 1) / hashLength;
        System.Byte[] derivedKey = new System.Byte[keyLength];

        System.Span<System.Byte> buffer = stackalloc System.Byte[salt.Length + 4];
        salt.CopyTo(buffer);

        System.Int32 offset = 0;
        for (System.Int32 i = 1; i <= blockCount; i++)
        {
            System.Buffers.Binary.BinaryPrimitives.WriteInt32BigEndian(buffer[salt.Length..], i);

            System.Int32 bytesToCopy = System.Math.Min(hashLength, keyLength - offset);

            ComputeBlock(password, buffer, iterations,
                System.MemoryExtensions.AsSpan(derivedKey, offset, bytesToCopy), hashLength, computeHmac);

            offset += hashLength;
        }

        return derivedKey;
    }

    /// <summary>
    /// Computes a single block of the PBKDF2 key derivation process.
    /// </summary>
    /// <param name="password">The password bytes to derive the key from.</param>
    /// <param name="saltWithIndex">The salt concatenated with the block index.</param>
    /// <param name="iterations">The TransportProtocol of iterations to perform.</param>
    /// <param name="outputBlock">The span to store the computed block.</param>
    /// <param name="hashLength">The length of the hash output.</param>
    /// <param name="computeHmac">The HMAC computation function to use.</param>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    private static void ComputeBlock(
        System.ReadOnlySpan<System.Byte> password, System.ReadOnlySpan<System.Byte> saltWithIndex,
        System.Int32 iterations, System.Span<System.Byte> outputBlock, System.Int32 hashLength,
        System.Action<System.ReadOnlySpan<System.Byte>, System.ReadOnlySpan<System.Byte>, System.Span<System.Byte>> computeHmac)
    {
        System.Span<System.Byte> u = stackalloc System.Byte[hashLength];
        System.Span<System.Byte> temp = stackalloc System.Byte[hashLength];

        computeHmac(password, saltWithIndex, u);
        u.CopyTo(outputBlock);

        for (System.Int32 i = 1; i < iterations; i++)
        {
            computeHmac(password, u, temp);
            temp.CopyTo(u);

            if (System.Numerics.Vector.IsHardwareAccelerated && hashLength >= System.Numerics.Vector<System.Byte>.Count)
            {
                System.Int32 j = 0;
                for (; j + System.Numerics.Vector<System.Byte>.Count <= outputBlock.Length; j += System.Numerics.Vector<System.Byte>.Count)
                {
                    System.Numerics.Vector<System.Byte> vU = new(u[j..]);
                    System.Numerics.Vector<System.Byte> vOut = new(outputBlock[j..]);
                    (vOut ^ vU).CopyTo(outputBlock[j..]);
                }
                for (; j < outputBlock.Length; j++)
                {
                    outputBlock[j] ^= u[j];
                }
            }
            else
            {
                for (System.Int32 j = 0; j < outputBlock.Length; j++)
                {
                    outputBlock[j] ^= u[j];
                }
            }
        }
    }

    /// <summary>
    /// Computes an HMAC-SHA1 hash.
    /// </summary>
    /// <param name="key">The key for the HMAC computation.</param>
    /// <param name="message">The message to hash.</param>
    /// <param name="output">The span to store the hash output (20 bytes).</param>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    private static void ComputeHmacSha1(
        System.ReadOnlySpan<System.Byte> key,
        System.ReadOnlySpan<System.Byte> message, System.Span<System.Byte> output)
    {
        const System.Int32 BlockSize = 64; // SHA-1 block size in bytes
        System.Span<System.Byte> keyBlock = stackalloc System.Byte[BlockSize];
        keyBlock.Clear();

        // Step 1: Process Key
        if (key.Length > BlockSize)
        {
            SHA1 sha1 = new();
            System.MemoryExtensions.CopyTo(sha1.ComputeHash(key), keyBlock);
        }
        else
        {
            key.CopyTo(keyBlock);
        }

        // Step 2: Generate ipad and opad
        System.Span<System.Byte> ipad = stackalloc System.Byte[BlockSize];
        System.Span<System.Byte> opad = stackalloc System.Byte[BlockSize];

        for (System.Int32 i = 0; i < BlockSize; i++)
        {
            ipad[i] = (System.Byte)(keyBlock[i] ^ 0x36);
            opad[i] = (System.Byte)(keyBlock[i] ^ 0x5C);
        }

        // Step 3: Compute inner hash (H(K ⊕ ipad || message))
        SHA1 sha1Inner = new();
        sha1Inner.Update(ipad);
        sha1Inner.Update(message);
        System.Span<System.Byte> innerHash = stackalloc System.Byte[20]; // SHA-1 output size
        System.MemoryExtensions.CopyTo(sha1Inner.FinalizeHash(), innerHash);

        // Step 4: Compute outer hash (H(K ⊕ opad || innerHash))
        SHA1 sha1Outer = new();
        sha1Outer.Update(opad);
        sha1Outer.Update(innerHash);
        System.MemoryExtensions.CopyTo(sha1Outer.FinalizeHash(), output);
    }

    /// <summary>
    /// Computes an HMAC-SHA1 hash.
    /// </summary>
    /// <param name="key">The key for the HMAC computation.</param>
    /// <param name="message">The message to hash.</param>
    /// <param name="output">The span to store the hash output (28 bytes).</param>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    private static void ComputeHmacSha224(
        System.ReadOnlySpan<System.Byte> key,
        System.ReadOnlySpan<System.Byte> message, System.Span<System.Byte> output)
    {
        const System.Int32 BlockSize = 64; // SHA-1 block size in bytes
        System.Span<System.Byte> keyBlock = stackalloc System.Byte[BlockSize];
        keyBlock.Clear();

        // Step 1: Process Key
        if (key.Length > BlockSize)
        {
            using SHA224 sha224 = new();
            System.MemoryExtensions.CopyTo(sha224.ComputeHash(key), keyBlock);
        }
        else
        {
            key.CopyTo(keyBlock);
        }

        // Step 2: Generate ipad and opad
        System.Span<System.Byte> ipad = stackalloc System.Byte[BlockSize];
        System.Span<System.Byte> opad = stackalloc System.Byte[BlockSize];

        for (System.Int32 i = 0; i < BlockSize; i++)
        {
            ipad[i] = (System.Byte)(keyBlock[i] ^ 0x36);
            opad[i] = (System.Byte)(keyBlock[i] ^ 0x5C);
        }

        // Step 3: Compute inner hash (H(K ⊕ ipad || message))
        using SHA224 sha224Inner = new();
        sha224Inner.Update(ipad);
        sha224Inner.Update(message);
        System.Span<System.Byte> innerHash = stackalloc System.Byte[20]; // SHA-1 output size
        System.MemoryExtensions.CopyTo(sha224Inner.FinalizeHash(), innerHash);

        // Step 4: Compute outer hash (H(K ⊕ opad || innerHash))
        using SHA224 sha224Outer = new();
        sha224Outer.Update(opad);
        sha224Outer.Update(innerHash);
        System.MemoryExtensions.CopyTo(sha224Outer.FinalizeHash(), output);
    }

    /// <summary>
    /// Computes an HMAC-SHA256 hash.
    /// </summary>
    /// <param name="key">The key for the HMAC computation.</param>
    /// <param name="message">The message to hash.</param>
    /// <param name="output">The span to store the hash output (32 bytes).</param>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    private static void ComputeHmacSha256(
        System.ReadOnlySpan<System.Byte> key,
        System.ReadOnlySpan<System.Byte> message, System.Span<System.Byte> output)
    {
        const System.Int32 BlockSize = 64; // SHA-256 block size in bytes
        System.Span<System.Byte> keyPadded = stackalloc System.Byte[BlockSize];
        System.Span<System.Byte> ipad = stackalloc System.Byte[BlockSize];
        System.Span<System.Byte> opad = stackalloc System.Byte[BlockSize];

        // Step 1: Process Key
        if (key.Length > BlockSize)
        {
            System.MemoryExtensions.CopyTo(SHA256.HashData(key), keyPadded);
        }
        else
        {
            key.CopyTo(keyPadded);
        }

        // Step 2: Generate ipad and opad
        for (System.Int32 i = 0; i < BlockSize; i++)
        {
            ipad[i] = (System.Byte)(keyPadded[i] ^ 0x36);
            opad[i] = (System.Byte)(keyPadded[i] ^ 0x5C);
        }

        // Step 3: Hash (ipad || message)
        System.Span<System.Byte> innerHash = stackalloc System.Byte[32];
        using (SHA256 sha256 = new())
        {
            sha256.Update(ipad);
            sha256.Update(message);
            innerHash = sha256.FinalizeHash();
        }

        // Step 4: Hash (opad || innerHash)
        using (SHA256 sha256 = new())
        {
            sha256.Update(opad);
            sha256.Update(innerHash);
            System.MemoryExtensions.CopyTo(sha256.FinalizeHash(), output);
        }
    }

    #endregion Private Methods
}