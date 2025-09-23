// Copyright (c) 2025 PPN Corporation. All rights reserved.

using Nalix.Common.Enums;

namespace Nalix.Framework.Cryptography.Security;

/// <summary>
/// PBKDF2 (RFC 8018) key derivation with HMAC-SHA256/512, Span-based core, and secure disposal.
/// </summary>
public sealed class PBKDF2 : System.IDisposable
{
    private readonly System.Byte[] _salt;           // a copy of the provided salt
    private readonly System.Int32 _iterations;
    private readonly System.Int32 _keyLength;
    private readonly HashType _hashAlg;

    private System.Boolean _disposed;

    /// <summary>
    /// Initializes a new PBKDF2 instance with the given parameters.
    /// </summary>
    /// <param name="salt">Cryptographic salt (will be copied internally).</param>
    /// <param name="iterations">Iteration count (>= 1; recommended: 100k+).</param>
    /// <param name="keyLength">Desired key length in bytes (>= 1).</param>
    /// <param name="hashAlgorithm">PRF selection: Sha256 or Sha512.</param>
    /// <exception cref="System.ArgumentException">If parameters are invalid.</exception>
    public PBKDF2(System.Byte[] salt, System.Int32 iterations, System.Int32 keyLength, HashType hashAlgorithm)
    {
        if (salt is null || salt.Length < 8)
        {
            throw new System.ArgumentException("Salt must be at least 8 bytes.", nameof(salt));
        }

        System.ArgumentOutOfRangeException.ThrowIfNegativeOrZero(iterations);

        System.ArgumentOutOfRangeException.ThrowIfNegativeOrZero(keyLength);

        if (hashAlgorithm is not HashType.Sha256 and not HashType.Sha512)
        {
            throw new System.ArgumentOutOfRangeException(nameof(hashAlgorithm), "Only Sha256 and Sha512 are supported.");
        }

        _salt = new System.Byte[salt.Length];
        System.Buffer.BlockCopy(salt, 0, _salt, 0, salt.Length);
        _iterations = iterations;
        _keyLength = keyLength;
        _hashAlg = hashAlgorithm;
    }

    /// <summary>
    /// Derives a key from a UTF-8 password string and returns a new byte array.
    /// </summary>
    /// <param name="password">Password string (will be encoded as UTF-8).</param>
    public System.Byte[] DeriveKey(System.String password)
    {
        System.ArgumentNullException.ThrowIfNull(password);
        // Encoding allocates; safe and simple. For zero-alloc, add a char->utf8 span encoder later.
        System.Byte[] pw = System.Text.Encoding.UTF8.GetBytes(password);
        try
        {
            return DeriveKey(pw);
        }
        finally
        {
            System.Array.Clear(pw, 0, pw.Length);
        }
    }

    /// <summary>
    /// Derives a key from password bytes and returns a new byte array.
    /// </summary>
    /// <param name="password">Password bytes (e.g., UTF-8 of the password).</param>
    public System.Byte[] DeriveKey(System.ReadOnlySpan<System.Byte> password)
    {
        System.Byte[] dk = System.GC.AllocateUninitializedArray<System.Byte>(_keyLength);
        DeriveKey(password, dk);
        return dk;
    }

    /// <summary>
    /// Derives a key from password bytes into <paramref name="output"/>.
    /// </summary>
    /// <param name="password">Password bytes.</param>
    /// <param name="output">Destination buffer with length >= configured key length.</param>
    public void DeriveKey(System.ReadOnlySpan<System.Byte> password, System.Span<System.Byte> output)
    {
        System.ObjectDisposedException.ThrowIf(_disposed, this);
        if (output.Length < _keyLength)
        {
            throw new System.ArgumentException("Output buffer is too small.", nameof(output));
        }

        System.Int32 hLen = _hashAlg == HashType.Sha256 ? 32 : 64;
        System.Int32 l = (System.Int32)System.Math.Ceiling(_keyLength / (System.Double)hLen);
        System.Int32 r = _keyLength - ((l - 1) * hLen);

        System.Byte[] block = System.Buffers.ArrayPool<System.Byte>.Shared.Rent(hLen);
        try
        {
            System.Int32 offset = 0;
            for (System.Int32 i = 1; i <= l; i++)
            {
                F(password, _salt, _iterations, i, _hashAlg, System.MemoryExtensions.AsSpan(block, 0, hLen));
                System.Int32 toCopy = (i == l) ? r : hLen;
                System.MemoryExtensions.AsSpan(block, 0, toCopy).CopyTo(output.Slice(offset, toCopy));
                offset += toCopy;
            }
        }
        finally
        {
            System.Array.Clear(block, 0, block.Length);
            System.Buffers.ArrayPool<System.Byte>.Shared.Return(block);
        }
    }

    /// <summary>
    /// Tries to derive a key into <paramref name="output"/> and reports success.
    /// </summary>
    public System.Boolean TryDeriveKey(System.ReadOnlySpan<System.Byte> password, System.Span<System.Byte> output, out System.Int32 bytesWritten)
    {
        System.ObjectDisposedException.ThrowIf(_disposed, this);
        if (output.Length < _keyLength)
        {
            bytesWritten = 0;
            return false;
        }
        DeriveKey(password, output);
        bytesWritten = _keyLength;
        return true;
    }

    #region Core PBKDF2

    // RFC 8018, Section 5.2:
    // U1 = PRF(P, S || INT_32_BE(i))
    // Uc = PRF(P, U_{c-1})
    // T_i = U1 XOR U2 XOR ... XOR Uiterations
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    private static void F(
        System.ReadOnlySpan<System.Byte> password,
        System.ReadOnlySpan<System.Byte> salt,
        System.Int32 iterations,
        System.Int32 blockIndex,
        HashType prf,
        System.Span<System.Byte> output)
    {
        System.Int32 hLen = prf == HashType.Sha256 ? 32 : 64;

        System.Span<System.Byte> u = stackalloc System.Byte[hLen];
        System.Span<System.Byte> t = stackalloc System.Byte[hLen];

        // salt || INT(i) (big-endian)
        System.Span<System.Byte> si = stackalloc System.Byte[salt.Length + 4];
        salt.CopyTo(si);
        WriteInt32BE(blockIndex, si.Slice(salt.Length, 4));

        // U1
        Hmac(password, si, prf, u);
        u.CopyTo(t);

        // U2..Uc
        for (System.Int32 c = 2; c <= iterations; c++)
        {
            Hmac(password, u, prf, u);
            XorInPlace(t, u);
        }

        t.CopyTo(output); // T_i
        u.Clear();
        t.Clear();
        si.Clear();
    }

    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    private static void Hmac(
        System.ReadOnlySpan<System.Byte> key,
        System.ReadOnlySpan<System.Byte> data,
        HashType prf, System.Span<System.Byte> output)
    {
        // HMAC classes accept byte[]; minimal, contained allocations:
        if (prf == HashType.Sha256)
        {
            using System.Security.Cryptography.HMACSHA256 h = new(key.ToArray());
            System.Byte[] hash = h.ComputeHash(data.ToArray());
            System.MemoryExtensions.AsSpan(hash).CopyTo(output);
            System.Array.Clear(hash, 0, hash.Length);
        }
        else
        {
            using System.Security.Cryptography.HMACSHA512 h = new(key.ToArray());
            System.Byte[] hash = h.ComputeHash(data.ToArray());
            System.MemoryExtensions.AsSpan(hash).CopyTo(output);
            System.Array.Clear(hash, 0, hash.Length);
        }
    }

    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    private static void XorInPlace(
        System.Span<System.Byte> a,
        System.ReadOnlySpan<System.Byte> b)
    {
        for (System.Int32 i = 0; i < a.Length; i++)
        {
            a[i] ^= b[i];
        }
    }

    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    private static void WriteInt32BE(
        System.Int32 value, System.Span<System.Byte> dest)
    {
        dest[0] = (System.Byte)((System.UInt32)value >> 24);
        dest[1] = (System.Byte)((System.UInt32)value >> 16);
        dest[2] = (System.Byte)((System.UInt32)value >> 8);
        dest[3] = (System.Byte)value;
    }

    #endregion

    #region IDisposable

    /// <summary>Disposes internal resources and clears sensitive data.</summary>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        // Clear salt buffer
        System.Array.Clear(_salt, 0, _salt.Length);
    }

    #endregion
}
