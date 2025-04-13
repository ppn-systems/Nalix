using Notio.Cryptography.Enums;
using Notio.Cryptography.Hashing;
using System;
using System.Buffers.Binary;
using System.Numerics;
using System.Text;

namespace Notio.Cryptography.Security;

/// <summary>
/// High-performance implementation of the PBKDF2 (Password-Based Key Derivation Function 2) algorithm.
/// Supports HMAC-SHA1 and HMAC-SHA256 for key derivation.
/// </summary>
public sealed class Pbkdf2 : IDisposable
{
    #region Fields

    private readonly byte[] _salt;
    private readonly int _keyLength;
    private readonly int _iterations;
    private readonly HashAlgorithm _hashType;
    private bool _disposed;

    #endregion

    /// <summary>
    /// Initializes a new instance of the <see cref="Pbkdf2"/> class.
    /// </summary>
    /// <param name="salt">The salt value to use in the key derivation process. Must not be null or empty.</param>
    /// <param name="iterations">The Number of iterations to perform. Must be greater than 0.</param>
    /// <param name="keyLength">The desired length of the derived key in bytes. Must be greater than 0.</param>
    /// <param name="hashType">The hash algorithm to use (SHA1 or SHA256). Defaults to SHA1.</param>
    /// <exception cref="ArgumentException">Thrown if <paramref name="salt"/> is null or empty.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown if <paramref name="iterations"/> or <paramref name="keyLength"/> is less than or equal to 0.</exception>
    public Pbkdf2(byte[] salt, int iterations, int keyLength, HashAlgorithm hashType = HashAlgorithm.Sha1)
    {
        if (salt == null || salt.Length == 0)
            throw new ArgumentException("Salt cannot be empty.", nameof(salt));

        if (iterations <= 0)
            throw new ArgumentOutOfRangeException(nameof(iterations), "Must be > 0.");

        if (keyLength <= 0)
            throw new ArgumentOutOfRangeException(nameof(keyLength), "Must be > 0.");

        _salt = (byte[])salt.Clone();
        _keyLength = keyLength;
        _iterations = iterations;
        _hashType = hashType;
    }

    /// <summary>
    /// Derives a key from the specified password using PBKDF2 with UTF-8 encoding.
    /// </summary>
    /// <param name="password">The password to derive the key from.</param>
    /// <returns>A byte array containing the derived key.</returns>
    /// <exception cref="ArgumentException">Thrown if <paramref name="password"/> is null or empty.</exception>
    /// <exception cref="ObjectDisposedException">Thrown if the instance has been disposed.</exception>
    public byte[] DeriveKey(string password)
    {
        ObjectDisposedException.ThrowIf(_disposed, nameof(Pbkdf2));
        if (string.IsNullOrEmpty(password)) throw new ArgumentException("Password cannot be empty.", nameof(password));

        ReadOnlySpan<byte> passwordBytes = Encoding.UTF8.GetBytes(password);
        return _hashType == HashAlgorithm.Sha256
            ? DeriveKeyUsingHmacSha256(passwordBytes)
            : DeriveKeyUsingHmacSha1(passwordBytes);
    }

    /// <summary>
    /// Derives a key from the specified password bytes using PBKDF2.
    /// </summary>
    /// <param name="passwordBytes">The password bytes to derive the key from.</param>
    /// <returns>A byte array containing the derived key.</returns>
    /// <exception cref="ArgumentException">Thrown if <paramref name="passwordBytes"/> is empty.</exception>
    /// <exception cref="ObjectDisposedException">Thrown if the instance has been disposed.</exception>
    public byte[] DeriveKey(ReadOnlySpan<byte> passwordBytes)
    {
        ObjectDisposedException.ThrowIf(_disposed, nameof(Pbkdf2));
        if (passwordBytes.IsEmpty) throw new ArgumentException("Password bytes cannot be empty.", nameof(passwordBytes));

        return _hashType == HashAlgorithm.Sha256
            ? DeriveKeyUsingHmacSha256(passwordBytes)
            : DeriveKeyUsingHmacSha1(passwordBytes);
    }

    /// <summary>
    /// Compares two byte spans in constant time to prevent timing attacks.
    /// </summary>
    /// <param name="a">The first byte span to compare.</param>
    /// <param name="b">The second byte span to compare.</param>
    /// <returns><c>true</c> if the spans are equal; otherwise, <c>false</c>.</returns>
    public static bool ConstantTimeEquals(ReadOnlySpan<byte> a, ReadOnlySpan<byte> b)
    {
        if (a.Length != b.Length) return false;

        int result = 0;
        for (int i = 0; i < a.Length; i++) result |= a[i] ^ b[i];

        return result == 0;
    }

    /// <summary>
    /// Releases all resources used by the <see cref="Pbkdf2"/> instance and clears sensitive data.
    /// </summary>
    public void Dispose()
    {
        if (_disposed) return;
        Array.Clear(_salt, 0, _salt.Length);
        _disposed = true;
    }

    /// <summary>
    /// Derives a key using HMAC-SHA1.
    /// </summary>
    /// <param name="password">The password bytes to derive the key from.</param>
    /// <returns>A byte array containing the derived key.</returns>
    private byte[] DeriveKeyUsingHmacSha1(ReadOnlySpan<byte> password)
        => DeriveKeyUsingHmac(password, _salt, _iterations, _keyLength, 20, ComputeHmacSha1);

    /// <summary>
    /// Derives a key using HMAC-SHA256.
    /// </summary>
    /// <param name="password">The password bytes to derive the key from.</param>
    /// <returns>A byte array containing the derived key.</returns>
    private byte[] DeriveKeyUsingHmacSha256(ReadOnlySpan<byte> password)
        => DeriveKeyUsingHmac(password, _salt, _iterations, _keyLength, 32, ComputeHmacSha256);

    /// <summary>
    /// Core implementation of PBKDF2 key derivation using the specified HMAC function.
    /// </summary>
    /// <param name="password">The password bytes to derive the key from.</param>
    /// <param name="salt">The salt bytes to use.</param>
    /// <param name="iterations">The Number of iterations to perform.</param>
    /// <param name="keyLength">The desired key length in bytes.</param>
    /// <param name="hashLength">The length of the hash output (20 for SHA1, 32 for SHA256).</param>
    /// <param name="computeHmac">The HMAC computation function to use.</param>
    /// <returns>A byte array containing the derived key.</returns>
    private static byte[] DeriveKeyUsingHmac(
        ReadOnlySpan<byte> password, ReadOnlySpan<byte> salt,
        int iterations, int keyLength, int hashLength,
        Action<ReadOnlySpan<byte>, ReadOnlySpan<byte>, Span<byte>> computeHmac)
    {
        int blockCount = (keyLength + hashLength - 1) / hashLength;
        byte[] derivedKey = new byte[keyLength];

        Span<byte> buffer = stackalloc byte[salt.Length + 4];
        salt.CopyTo(buffer);

        int offset = 0;
        for (int i = 1; i <= blockCount; i++)
        {
            BinaryPrimitives.WriteInt32BigEndian(buffer[salt.Length..], i);

            int bytesToCopy = Math.Min(hashLength, keyLength - offset);
            ComputeBlock(password, buffer, iterations, derivedKey.AsSpan(offset, bytesToCopy), hashLength, computeHmac);

            offset += hashLength;
        }

        return derivedKey;
    }

    /// <summary>
    /// Computes a single block of the PBKDF2 key derivation process.
    /// </summary>
    /// <param name="password">The password bytes to derive the key from.</param>
    /// <param name="saltWithIndex">The salt concatenated with the block index.</param>
    /// <param name="iterations">The Number of iterations to perform.</param>
    /// <param name="outputBlock">The span to store the computed block.</param>
    /// <param name="hashLength">The length of the hash output.</param>
    /// <param name="computeHmac">The HMAC computation function to use.</param>
    private static void ComputeBlock(
        ReadOnlySpan<byte> password, ReadOnlySpan<byte> saltWithIndex,
        int iterations, Span<byte> outputBlock, int hashLength,
        Action<ReadOnlySpan<byte>, ReadOnlySpan<byte>, Span<byte>> computeHmac)
    {
        Span<byte> u = stackalloc byte[hashLength];
        Span<byte> temp = stackalloc byte[hashLength];

        computeHmac(password, saltWithIndex, u);
        u.CopyTo(outputBlock);

        for (int i = 1; i < iterations; i++)
        {
            computeHmac(password, u, temp);
            temp.CopyTo(u);

            if (Vector.IsHardwareAccelerated && hashLength >= Vector<byte>.Count)
            {
                int j = 0;
                for (; j + Vector<byte>.Count <= outputBlock.Length; j += Vector<byte>.Count)
                {
                    var vU = new Vector<byte>(u[j..]);
                    var vOut = new Vector<byte>(outputBlock[j..]);
                    (vOut ^ vU).CopyTo(outputBlock[j..]);
                }
                for (; j < outputBlock.Length; j++) outputBlock[j] ^= u[j];
            }
            else
            {
                for (int j = 0; j < outputBlock.Length; j++) outputBlock[j] ^= u[j];
            }
        }
    }

    /// <summary>
    /// Computes an HMAC-SHA1 hash.
    /// </summary>
    /// <param name="key">The key for the HMAC computation.</param>
    /// <param name="message">The message to hash.</param>
    /// <param name="output">The span to store the hash output (20 bytes).</param>
    private static void ComputeHmacSha1(ReadOnlySpan<byte> key, ReadOnlySpan<byte> message, Span<byte> output)
    {
        const int BlockSize = 64; // SHA-1 block size in bytes
        Span<byte> keyBlock = stackalloc byte[BlockSize];
        keyBlock.Clear();

        // Step 1: Process Key
        if (key.Length > BlockSize)
        {
            Sha1 sha1 = new();
            sha1.ComputeHash(key).CopyTo(keyBlock);
        }
        else
        {
            key.CopyTo(keyBlock);
        }

        // Step 2: Generate ipad and opad
        Span<byte> ipad = stackalloc byte[BlockSize];
        Span<byte> opad = stackalloc byte[BlockSize];

        for (int i = 0; i < BlockSize; i++)
        {
            ipad[i] = (byte)(keyBlock[i] ^ 0x36);
            opad[i] = (byte)(keyBlock[i] ^ 0x5C);
        }

        // Step 3: Compute inner hash (H(K ⊕ ipad || message))
        Sha1 sha1Inner = new();
        sha1Inner.Update(ipad);
        sha1Inner.Update(message);
        Span<byte> innerHash = stackalloc byte[20]; // SHA-1 output size
        sha1Inner.FinalizeHash().CopyTo(innerHash);

        // Step 4: Compute outer hash (H(K ⊕ opad || innerHash))
        Sha1 sha1Outer = new();
        sha1Outer.Update(opad);
        sha1Outer.Update(innerHash);
        sha1Outer.FinalizeHash().CopyTo(output);
    }

    /// <summary>
    /// Computes an HMAC-SHA256 hash.
    /// </summary>
    /// <param name="key">The key for the HMAC computation.</param>
    /// <param name="message">The message to hash.</param>
    /// <param name="output">The span to store the hash output (32 bytes).</param>
    private static void ComputeHmacSha256(ReadOnlySpan<byte> key, ReadOnlySpan<byte> message, Span<byte> output)
    {
        const int BlockSize = 64; // SHA-256 block size in bytes
        Span<byte> keyPadded = stackalloc byte[BlockSize];
        Span<byte> ipad = stackalloc byte[BlockSize];
        Span<byte> opad = stackalloc byte[BlockSize];

        // Step 1: Process Key
        if (key.Length > BlockSize)
        {
            Sha256.HashData(key).CopyTo(keyPadded);
        }
        else
        {
            key.CopyTo(keyPadded);
        }

        // Step 2: Generate ipad and opad
        for (int i = 0; i < BlockSize; i++)
        {
            ipad[i] = (byte)(keyPadded[i] ^ 0x36);
            opad[i] = (byte)(keyPadded[i] ^ 0x5C);
        }

        // Step 3: Hash (ipad || message)
        Span<byte> innerHash = stackalloc byte[32];
        using (Sha256 sha256 = new())
        {
            sha256.Update(ipad);
            sha256.Update(message);
            innerHash = sha256.FinalizeHash();
        }

        // Step 4: Hash (opad || innerHash)
        using (Sha256 sha256 = new())
        {
            sha256.Update(opad);
            sha256.Update(innerHash);
            sha256.FinalizeHash().CopyTo(output);
        }
    }
}
