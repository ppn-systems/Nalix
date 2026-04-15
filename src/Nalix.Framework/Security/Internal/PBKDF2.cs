// Copyright (c) 2025-2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using Nalix.Framework.Security.Hashing;

namespace Nalix.Framework.Security.Internal;

/// <summary>
/// Provides a PBKDF2_I (Password-Based Key Derivation Function 2) implementation
/// using HMAC-SHA3-256 as the pseudorandom function (PRF).
/// </summary>
/// <remarks>
/// <para>
/// This implementation follows the structure defined in RFC 8018, Section 5.2,
/// but replaces HMAC-SHA1/SHA256/SHA512 with HMAC-SHA3-256.
/// </para>
/// <para>
/// Key properties:
/// <list type="bullet">
///   <item><description>Digest length (<c>hLen</c>): 32 bytes.</description></item>
///   <item><description>HMAC block size (<c>B</c>): 136 bytes (SHA3-256 rate).</description></item>
///   <item><description>Iteration count should be at least 100,000 in production for security.</description></item>
/// </list>
/// </para>
/// <para>
/// Note: Outputs from this implementation are <b>not interoperable</b> with PBKDF2_I using SHA-256 or SHA-512.
/// </para>
/// </remarks>
[System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never)]
internal sealed class PBKDF2 : System.IDisposable
{
    private readonly byte[] _salt;
    private readonly int _iterations;
    private readonly int _keyLength;

    private bool _disposed;

    /// <summary>
    /// Initializes a new instance of the <see cref="PBKDF2"/> class with the specified parameters.
    /// </summary>
    /// <param name="salt">
    /// Cryptographic salt (at least 8 bytes). The salt is copied internally.
    /// </param>
    /// <param name="iterations">
    /// The number of iterations to apply. Must be a positive integer. Recommended: 100,000 or more.
    /// </param>
    /// <param name="keyLength">
    /// Desired length of the derived key in bytes. Must be a positive integer.
    /// </param>
    /// <exception cref="System.ArgumentException">
    /// Thrown when <paramref name="salt"/> is <c>null</c> or shorter than 8 bytes.
    /// </exception>
    /// <exception cref="System.ArgumentOutOfRangeException">
    /// Thrown when <paramref name="iterations"/> or <paramref name="keyLength"/> is not positive.
    /// </exception>
    public PBKDF2(byte[] salt, int iterations, int keyLength)
    {
        if (salt is null || salt.Length < 8)
        {
            throw new System.ArgumentException("Salt must be at least 8 bytes.", nameof(salt));
        }

        System.ArgumentOutOfRangeException.ThrowIfNegativeOrZero(iterations);
        System.ArgumentOutOfRangeException.ThrowIfNegativeOrZero(keyLength);

        _salt = new byte[salt.Length];
        System.Buffer.BlockCopy(salt, 0, _salt, 0, salt.Length);
        _iterations = iterations;
        _keyLength = keyLength;
    }

    /// <summary>
    /// Derives a cryptographic key from a password string using UTF-8 encoding.
    /// </summary>
    /// <param name="password">The password string to derive the key from.</param>
    /// <returns>A new byte array containing the derived key.</returns>
    /// <exception cref="System.ArgumentNullException">
    /// Thrown when <paramref name="password"/> is <c>null</c>.
    /// </exception>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
    public byte[] GenerateKey([System.Diagnostics.CodeAnalysis.DisallowNull] string password)
    {
        System.ArgumentNullException.ThrowIfNull(password);
        byte[] pw = System.Text.Encoding.UTF8.GetBytes(password);
        try { return this.GenerateKey(pw); }
        finally { System.Array.Clear(pw, 0, pw.Length); }
    }

    /// <summary>
    /// Derives a cryptographic key from password bytes.
    /// </summary>
    /// <param name="password">The password bytes (e.g., UTF-8 encoded password).</param>
    /// <returns>A new byte array containing the derived key.</returns>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
    public byte[] GenerateKey([System.Diagnostics.CodeAnalysis.DisallowNull] System.ReadOnlySpan<byte> password)
    {
        byte[] dk = System.GC.AllocateUninitializedArray<byte>(_keyLength);
        this.GenerateKey(password, dk);
        return dk;
    }

    /// <summary>
    /// Derives a cryptographic key from password bytes into a caller-provided buffer.
    /// </summary>
    /// <param name="password">The password bytes.</param>
    /// <param name="output">
    /// The destination buffer that receives the derived key.
    /// Must be at least the configured key length.
    /// </param>
    /// <exception cref="System.ObjectDisposedException">
    /// Thrown if this instance has been disposed.
    /// </exception>
    /// <exception cref="System.ArgumentException">
    /// Thrown if <paramref name="output"/> is smaller than the configured key length.
    /// </exception>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
    public void GenerateKey(
        [System.Diagnostics.CodeAnalysis.DisallowNull] System.ReadOnlySpan<byte> password,
        [System.Diagnostics.CodeAnalysis.DisallowNull] System.Span<byte> output)
    {
        System.ObjectDisposedException.ThrowIf(_disposed, this);
        if (output.Length < _keyLength)
        {
            throw new System.ArgumentException("Output buffer is too small.", nameof(output));
        }

        const int hLen = 32;   // SHA3-256 digest length
        const int B = 136;     // HMAC block size (SHA3-256 rate)
        int l = (int)System.Math.Ceiling(_keyLength / (double)hLen);
        int r = _keyLength - ((l - 1) * hLen);

        byte[] block = System.Buffers.ArrayPool<byte>.Shared.Rent(hLen);
        try
        {
            int offset = 0;
            for (int i = 1; i <= l; i++)
            {
                F(password, _salt, _iterations, i, System.MemoryExtensions.AsSpan(block, 0, hLen), B);
                int toCopy = i == l ? r : hLen;
                System.MemoryExtensions.AsSpan(block, 0, toCopy).CopyTo(output.Slice(offset, toCopy));
                offset += toCopy;
            }
        }
        finally
        {
            System.Array.Clear(block, 0, block.Length);
            System.Buffers.ArrayPool<byte>.Shared.Return(block);
        }
    }

    /// <summary>
    /// Attempts to derive a cryptographic key into the provided buffer.
    /// </summary>
    /// <param name="password">The password bytes.</param>
    /// <param name="output">The destination buffer.</param>
    /// <param name="bytesWritten">When this method returns, contains the number of bytes written.</param>
    /// <returns>
    /// <c>true</c> if the key was successfully derived and written into <paramref name="output"/>;
    /// otherwise, <c>false</c>.
    /// </returns>
    /// <exception cref="System.ObjectDisposedException">
    /// Thrown if this instance has been disposed.
    /// </exception>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
    public bool GenerateKey(
        [System.Diagnostics.CodeAnalysis.DisallowNull] System.ReadOnlySpan<byte> password,
        [System.Diagnostics.CodeAnalysis.DisallowNull] System.Span<byte> output,
        [System.Diagnostics.CodeAnalysis.NotNullWhen(true)] out int bytesWritten)
    {
        System.ObjectDisposedException.ThrowIf(_disposed, this);
        if (output.Length < _keyLength)
        {
            bytesWritten = 0;
            return false;
        }
        this.GenerateKey(password, output);
        bytesWritten = _keyLength;
        return true;
    }

    #region Core PBKDF2

    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
    private static void F(
        System.ReadOnlySpan<byte> password,
        System.ReadOnlySpan<byte> salt,
        int iterations, int blockIndex,
        System.Span<byte> output, int blockSize)
    {
        const int hLen = 32;

        System.Span<byte> u = stackalloc byte[hLen];
        System.Span<byte> t = stackalloc byte[hLen];

        // salt || INT(i)
        System.Span<byte> si = stackalloc byte[salt.Length + 4];
        salt.CopyTo(si);
        WriteInt32BE(blockIndex, si.Slice(salt.Length, 4));

        // U1
        HmacSha3_256(password, si, u, blockSize);
        u.CopyTo(t);

        // U2..Uc
        for (int c = 2; c <= iterations; c++)
        {
            HmacSha3_256(password, u, u, blockSize);
            for (int i = 0; i < t.Length; i++)
            {
                t[i] ^= u[i];
            }
        }

        t.CopyTo(output);
        u.Clear();
        t.Clear();
        si.Clear();
    }

    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
    private static void HmacSha3_256(
        System.ReadOnlySpan<byte> key,
        System.ReadOnlySpan<byte> data,
        System.Span<byte> output, int blockSize)
    {
        // Prepare key block
        System.Span<byte> k0 = stackalloc byte[blockSize];
        if (key.Length > blockSize)
        {
            System.Span<byte> kh = stackalloc byte[32];
            Keccak256.HashData(key, kh);
            kh.CopyTo(k0);
            kh.Clear();
        }
        else
        {
            key.CopyTo(k0);
            if (key.Length < blockSize)
            {
                k0[key.Length..].Clear();
            }
        }

        // ipad/opad
        System.Span<byte> ipad = stackalloc byte[blockSize];
        System.Span<byte> opad = stackalloc byte[blockSize];
        for (int i = 0; i < blockSize; i++)
        {
            byte b = k0[i];
            ipad[i] = (byte)(b ^ 0x36);
            opad[i] = (byte)(b ^ 0x5c);
        }

        // inner = H(ipad || data)
        System.Span<byte> inner = stackalloc byte[32];
        {
            Keccak256.Sponge hInner = new();
            hInner.Absorb(ipad);
            hInner.Absorb(data);
            hInner.PadAndSqueeze(inner);
        }

        // outer = H(opad || inner)
        Keccak256.Sponge hOuter = new();
        hOuter.Absorb(opad);
        hOuter.Absorb(inner);
        hOuter.PadAndSqueeze(output);

        k0.Clear();
        ipad.Clear();
        opad.Clear();
        inner.Clear();
    }

    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    private static void WriteInt32BE(int value, System.Span<byte> dest)
    {
        dest[0] = (byte)((uint)value >> 24);
        dest[1] = (byte)((uint)value >> 16);
        dest[2] = (byte)((uint)value >> 8);
        dest[3] = (byte)value;
    }

    #endregion Core PBKDF2

    #region IDisposable

    /// <summary>
    /// Releases all resources used by the current instance of the <see cref="PBKDF2_I"/> class
    /// and clears sensitive data such as the internal salt.
    /// </summary>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        System.Array.Clear(_salt, 0, _salt.Length);
    }

    #endregion IDisposable
}
