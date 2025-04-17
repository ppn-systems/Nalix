// Copyright (c) 2025 PPN Corporation. All rights reserved.

using Nalix.Framework.Cryptography.Hashing;
using Nalix.Framework.Cryptography.Symmetric;

namespace Nalix.Framework.Cryptography.Aead;

/// <summary>
/// Salsa20-Poly1305 authenticated encryption (NaCl/secretbox-style).
/// <para>
/// Construction:
///   - one-time Poly1305 key = first 32 bytes of Salsa20 keystream with counter = 0
///   - ciphertext = Salsa20 XOR of plaintext with counter starting at 1
///   - tag = Poly1305(ciphertext, one-time key)
/// </para>
/// <remarks>
/// This construction does NOT support associated data (AAD), by design (NaCl secretbox).
/// Key can be 16 or 32 bytes (Salsa20/128 or Salsa20/256). Nonce is 8 bytes (classic Salsa20).
/// Tag is 16 bytes (Poly1305).
/// </remarks>
/// </summary>
[System.Runtime.CompilerServices.SkipLocalsInit]
public static class Salsa20Poly1305
{
    #region Constants

    /// <summary>Accepted Salsa20 key sizes in bytes.</summary>
    public const System.Int32 KeySize128 = 16, KeySize256 = 32;

    /// <summary>Nonce size for Salsa20 (classic): 8 bytes.</summary>
    public const System.Int32 NonceSize = 8;

    /// <summary>Poly1305 tag size: 16 bytes.</summary>
    public const System.Int32 TagSize = 16;

    #endregion

    #region Public API (array-returning)

    /// <summary>
    /// Encrypts and authenticates <paramref name="plaintext"/> with Salsa20-Poly1305.
    /// </summary>
    /// <param name="key">16 or 32-byte key.</param>
    /// <param name="nonce">8-byte nonce.</param>
    /// <param name="plaintext">Plaintext to encrypt.</param>
    /// <returns>Ciphertext concatenated with 16-byte tag: C || T.</returns>
    /// <exception cref="System.ArgumentException">On invalid sizes.</exception>
    public static System.Byte[] Seal(
        System.ReadOnlySpan<System.Byte> key,
        System.ReadOnlySpan<System.Byte> nonce,
        System.ReadOnlySpan<System.Byte> plaintext)
    {
        System.Byte[] output = new System.Byte[plaintext.Length + TagSize];
        Seal(key, nonce, plaintext, output);
        return output;
    }

    /// <summary>
    /// Decrypts and verifies Salsa20-Poly1305 data produced by
    /// <see cref="Seal(System.ReadOnlySpan{System.Byte},System.ReadOnlySpan{System.Byte},System.ReadOnlySpan{System.Byte})"/>.
    /// </summary>
    /// <param name="key">16 or 32-byte key.</param>
    /// <param name="nonce">8-byte nonce.</param>
    /// <param name="ciphertextWithTag">Ciphertext||Tag buffer.</param>
    /// <returns>Decrypted plaintext if tag verifies; otherwise throws.</returns>
    /// <exception cref="System.ArgumentException">On invalid sizes.</exception>
    /// <exception cref="System.Security.SecurityException">If authentication fails.</exception>
    public static System.Byte[] Open(
        System.ReadOnlySpan<System.Byte> key,
        System.ReadOnlySpan<System.Byte> nonce,
        System.ReadOnlySpan<System.Byte> ciphertextWithTag)
    {
        if (ciphertextWithTag.Length < TagSize)
        {
            throw new System.ArgumentException("Input too short: missing tag.", nameof(ciphertextWithTag));
        }

        System.Int32 cLen = ciphertextWithTag.Length - TagSize;
        System.Byte[] plaintext = new System.Byte[cLen];
        if (!TryOpen(key, nonce, ciphertextWithTag, plaintext, out _))
        {
            throw new System.Security.SecurityException("Authentication failed (invalid tag).");
        }
        return plaintext;
    }

    #endregion

    #region Public API (span-based, zero-allocation friendly)

    /// <summary>
    /// Encrypts and authenticates into <paramref name="ciphertextWithTag"/>.
    /// </summary>
    /// <param name="key">16 or 32-byte key.</param>
    /// <param name="nonce">8-byte nonce.</param>
    /// <param name="plaintext">Plaintext to encrypt.</param>
    /// <param name="ciphertextWithTag">Destination buffer of size <c>plaintext.Length + 16</c>.</param>
    /// <returns>Total bytes written (= <c>plaintext.Length + 16</c>).</returns>
    public static System.Int32 Seal(
        System.ReadOnlySpan<System.Byte> key,
        System.ReadOnlySpan<System.Byte> nonce,
        System.ReadOnlySpan<System.Byte> plaintext,
        System.Span<System.Byte> ciphertextWithTag)
    {
        ValidateKeyNonceSizes(key, nonce);
        if (ciphertextWithTag.Length < plaintext.Length + TagSize)
        {
            throw new System.ArgumentException("Output buffer too small.", nameof(ciphertextWithTag));
        }

        // 1) Derive one-time Poly1305 key from Salsa20 keystream block with counter = 0
        System.Span<System.Byte> otk = stackalloc System.Byte[32];
        FillOneTimeKey(key, nonce, otk); // counter = 0

        // 2) Encrypt plaintext with Salsa20 from counter = 1
        System.Span<System.Byte> ct = ciphertextWithTag[..plaintext.Length];
        Salsa20.Encrypt(key, nonce, counter: 1UL, plaintext, ct);

        // 3) Compute Poly1305(tag) over ciphertext
        System.Span<System.Byte> tag = ciphertextWithTag.Slice(plaintext.Length, TagSize);
        Poly1305.Compute(otk, ct, tag);

        // Clear one-time key from stack
        otk.Clear();

        return plaintext.Length + TagSize;
    }

    /// <summary>
    /// Attempts to decrypt and authenticate <paramref name="ciphertextWithTag"/> into <paramref name="plaintext"/>.
    /// </summary>
    /// <param name="key">16 or 32-byte key.</param>
    /// <param name="nonce">8-byte nonce.</param>
    /// <param name="ciphertextWithTag">Ciphertext||Tag buffer.</param>
    /// <param name="plaintext">Destination buffer for plaintext (must be ciphertext.Length - 16).</param>
    /// <param name="bytesWritten">Number of plaintext bytes written on success.</param>
    /// <returns><c>true</c> if tag is valid and decryption succeeded; otherwise <c>false</c>.</returns>
    public static System.Boolean TryOpen(
        System.ReadOnlySpan<System.Byte> key,
        System.ReadOnlySpan<System.Byte> nonce,
        System.ReadOnlySpan<System.Byte> ciphertextWithTag,
        System.Span<System.Byte> plaintext,
        out System.Int32 bytesWritten)
    {
        bytesWritten = 0;
        ValidateKeyNonceSizes(key, nonce);

        if (ciphertextWithTag.Length < TagSize)
        {
            return false;
        }

        System.Int32 cLen = ciphertextWithTag.Length - TagSize;
        System.ReadOnlySpan<System.Byte> ct = ciphertextWithTag[..cLen];
        System.ReadOnlySpan<System.Byte> tag = ciphertextWithTag.Slice(cLen, TagSize);

        if (plaintext.Length < cLen)
        {
            // Caller didn't size output correctly
            return false;
        }

        // Derive Poly1305 one-time key from counter = 0
        System.Span<System.Byte> otk = stackalloc System.Byte[32];
        FillOneTimeKey(key, nonce, otk);

        // Verify tag over ciphertext in constant-time
        System.Boolean ok = Poly1305.Verify(otk, ct, tag);

        // Clear otk regardless of verification outcome
        otk.Clear();

        if (!ok)
        {
            return false;
        }

        // Decrypt with counter = 1
        Salsa20.Decrypt(key, nonce, counter: 1UL, ct, plaintext);
        bytesWritten = cLen;
        return true;
    }

    #endregion

    #region Helpers

    /// <summary>
    /// Validates key and nonce lengths for Salsa20.
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    private static void ValidateKeyNonceSizes(System.ReadOnlySpan<System.Byte> key, System.ReadOnlySpan<System.Byte> nonce)
    {
        if (key.Length is not KeySize128 and not KeySize256)
        {
            throw new System.ArgumentException("Key must be 16 or 32 bytes (128/256-bit).", nameof(key));
        }
        if (nonce.Length != NonceSize)
        {
            throw new System.ArgumentException("Nonce must be 8 bytes for Salsa20. For 24-byte nonces, use XSalsa20.", nameof(nonce));
        }
    }

    /// <summary>
    /// Fills the 32-byte Poly1305 one-time key using the first Salsa20 keystream block (counter = 0).
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    private static void FillOneTimeKey(
        System.ReadOnlySpan<System.Byte> key,
        System.ReadOnlySpan<System.Byte> nonce,
        System.Span<System.Byte> oneTimeKey32)
    {
        // Produce first 32 bytes of keystream by "encrypting" zero bytes.
        // We simply XOR a zero buffer to obtain the raw keystream.
        System.Span<System.Byte> zeros = stackalloc System.Byte[32];
        zeros.Clear();
        Salsa20.Encrypt(key, nonce, counter: 0UL, zeros, oneTimeKey32);
    }

    #endregion
}
