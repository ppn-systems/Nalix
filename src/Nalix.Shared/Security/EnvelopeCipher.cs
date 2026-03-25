// Copyright (c) 2025-2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using Nalix.Common.Security;
using Nalix.Framework.Random;
using Nalix.Shared.Security.Engine;
using Nalix.Shared.Security.Internal;
using Nalix.Shared.Security.Symmetric;

namespace Nalix.Shared.Security;

/// <summary>
/// Provides a unified high-level cryptographic façade that supports both
/// AEAD suites (e.g., CHACHA20-Poly1305) and pure symmetric stream/CTR ciphers
/// (e.g., CHACHA20, SALSA20, SPECK-CTR, XTEA-CTR).
/// <para>
/// The engine automatically dispatches to <see cref="AeadEngine"/> or
/// <see cref="SymmetricEngine"/> based on <see cref="CipherSuiteType"/>.
/// </para>
/// </summary>
/// <remarks>
/// <para>
/// <b>Envelope formats</b>
/// <list type="bullet">
/// <item>
/// <description><b>AEAD</b>: <c>header || nonce || ciphertext || tag</c> (detached tag).
/// Header size and tag size are defined by <see cref="EnvelopeFormat"/>.
/// AAD convention: <c>header || nonce || userAAD</c>.
/// </description>
/// </item>
/// <item>
/// <description><b>Symmetric/CTR</b>: <c>header || nonce || ciphertext</c> (no tag).
/// The <c>seq</c> value in the header is reused as the initial counter.
/// </description>
/// </item>
/// </list>
/// </para>
/// <para>
/// <b>Security notes</b>
/// <list type="bullet">
/// <item><description>
/// Callers must ensure per-key nonce uniqueness for the selected suite. AEAD encryption
/// auto-generates a fresh random nonce per call; do not reuse nonces with the same key.
/// </description></item>
/// <item><description>
/// For XTEA, a 32-byte key is deterministically reduced to 16 bytes by the lower-level engine.
/// </description></item>
/// <item><description>
/// <b>Decryption hard-fails softly:</b> this API returns <c>false</c> on parse/tag failure
/// and clears the output rather than throwing.
/// </description></item>
/// </list>
/// </para>
/// <para>
/// <b>Thread safety:</b> This class is stateless and safe for concurrent use.
/// </para>
/// </remarks>
[DebuggerNonUserCode]
public static class EnvelopeCipher
{
    /// <summary>
    /// Estimated number of additional bytes produced by the envelope encryption format.
    /// </summary>
    /// <value>
    /// This value includes the authentication tag length and any envelope header bytes (for example, nonce or metadata).
    /// Use this constant when sizing destination buffers for ciphertext to avoid buffer truncation.
    /// </value>
    /// <remarks>
    /// Computed as <c>EnvelopeFormat.TagSize + EnvelopeFormat.HeaderSize</c>.
    /// This is an estimate and may be conservative depending on the concrete cipher suite implementation.
    /// </remarks>
    public const int HeaderSize = EnvelopeFormat.HeaderSize;

    /// <summary>
    /// Gets the nonce length in bytes required by the specified cipher suite.
    /// </summary>
    /// <param name="type">
    /// The <see cref="CipherSuiteType"/> identifying the symmetric encryption algorithm.
    /// </param>
    /// <returns>
    /// The size of the nonce in bytes required by the cipher suite.
    /// </returns>
    /// <remarks>
    /// The nonce (number used once) is a unique value required for stream ciphers
    /// and AEAD constructions to ensure cryptographic security.
    /// <para/>
    /// For example:
    /// <list type="bullet">
    /// <item>
    /// <description>
    /// <see cref="CipherSuiteType.Chacha20"/> and
    /// <see cref="CipherSuiteType.Chacha20Poly1305"/> use a nonce size defined by <see cref="ChaCha20.NonceSize"/>.
    /// </description>
    /// </item>
    /// <item>
    /// <description>
    /// <see cref="CipherSuiteType.Salsa20"/> and
    /// <see cref="CipherSuiteType.Salsa20Poly1305"/> use a nonce size defined by <see cref="Salsa20.NonceSize"/>.
    /// </description>
    /// </item>
    /// </list>
    /// The returned value can be used when allocating buffers or generating nonces
    /// for encryption operations.
    /// </remarks>
    /// <exception cref="ArgumentException">
    /// Thrown when the specified cipher suite is not supported.
    /// </exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int GetNonceLength(CipherSuiteType type) => type switch
    {
        CipherSuiteType.Chacha20 => ChaCha20.NonceSize,
        CipherSuiteType.Chacha20Poly1305 => ChaCha20.NonceSize,
        CipherSuiteType.Salsa20 => Salsa20.NonceSize,
        CipherSuiteType.Salsa20Poly1305 => Salsa20.NonceSize,
        _ => throw new ArgumentException("Unsupported symmetric algorithm", nameof(type))
    };

    /// <summary>
    /// Gets the authentication tag length in bytes produced by the specified cipher suite.
    /// </summary>
    /// <param name="type">
    /// The <see cref="CipherSuiteType"/> identifying the symmetric encryption algorithm.
    /// </param>
    /// <returns>
    /// The size of the authentication tag in bytes.
    /// Returns <c>0</c> for cipher suites that do not provide built-in authentication.
    /// </returns>
    /// <remarks>
    /// Authentication tags are produced by AEAD (Authenticated Encryption with Associated Data)
    /// algorithms to guarantee ciphertext integrity and authenticity.
    /// <para/>
    /// <list type="bullet">
    /// <item>
    /// <description>
    /// <see cref="CipherSuiteType.Chacha20Poly1305"/> and
    /// <see cref="CipherSuiteType.Salsa20Poly1305"/> produce an authentication tag
    /// with size <see cref="EnvelopeFormat.TagSize"/>.
    /// </description>
    /// </item>
    /// <item>
    /// <description>
    /// <see cref="CipherSuiteType.Chacha20"/> and
    /// <see cref="CipherSuiteType.Salsa20"/> are stream ciphers without authentication,
    /// therefore the tag length is <c>0</c>.
    /// </description>
    /// </item>
    /// </list>
    /// This value is typically used when calculating the final ciphertext buffer size
    /// or parsing envelope encryption formats.
    /// </remarks>
    /// <exception cref="ArgumentException">
    /// Thrown when the specified cipher suite is not supported.
    /// </exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int GetTagLength(CipherSuiteType type) => type switch
    {
        CipherSuiteType.Chacha20 => 0,
        CipherSuiteType.Chacha20Poly1305 => EnvelopeFormat.TagSize,
        CipherSuiteType.Salsa20 => 0,
        CipherSuiteType.Salsa20Poly1305 => EnvelopeFormat.TagSize,
        _ => throw new ArgumentException("Unsupported symmetric algorithm", nameof(type))
    };

    /// <summary>
    /// Encrypts <paramref name="plaintext"/> using the selected <paramref name="algorithm"/>,
    /// returning a newly allocated envelope buffer.
    /// </summary>
    /// <param name="key">Secret key (length depends on the suite).</param>
    /// <param name="plaintext">Plaintext to encrypt.</param>
    /// <param name="ciphertext">The output ciphertext.</param>
    /// <param name="aad">
    /// Optional Associated Data (AEAD suites only). It is combined with header and nonce as
    /// <c>header || nonce || userAAD</c> under the tag.
    /// Ignored for non-AEAD suites.
    /// </param>
    /// <param name="seq">
    /// Optional 32-bit sequence written into the envelope header. When omitted, a random value
    /// is used. For non-AEAD suites, this value is also used as the initial counter.
    /// </param>
    /// <param name="algorithm">
    /// Cipher suite to use. AEAD suites produce <c>header||nonce||ciphertext||tag</c>;
    /// non-AEAD (stream/CTR) suites produce <c>header||nonce||ciphertext</c>.
    /// </param>
    /// <param name="written">Written output</param>
    /// <returns>
    /// A newly allocated byte array containing the full envelope.
    /// </returns>
    /// <exception cref="ArgumentException">
    /// Thrown if <paramref name="algorithm"/> is not recognized. Key/nonce length errors may be
    /// thrown by the underlying engines.
    /// </exception>
    /// <example>
    /// <code>
    /// // AEAD example (CHACHA20-Poly1305)
    /// var ct = EnvelopeCipher.Encrypt(key32, data, CipherSuiteType.CHACHA20_POLY1305, aad);
    ///
    /// // Stream/CTR example (CHACHA20)
    /// var ct2 = EnvelopeCipher.Encrypt(key32, data, CipherSuiteType.CHACHA20);
    /// </code>
    /// </example>
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    [return: NotNull]
    public static bool Encrypt(
        ReadOnlySpan<byte> key,
        ReadOnlySpan<byte> plaintext,
        Span<byte> ciphertext,
        ReadOnlySpan<byte> aad, uint? seq, CipherSuiteType algorithm,
        [NotNullWhen(true)] out int written)
    {
        written = 0;

        int nonceLength = GetNonceLength(algorithm);
        Span<byte> nonceStack = stackalloc byte[Math.Max(16, nonceLength)];
        Span<byte> nonce = nonceStack[..nonceLength];
        Csprng.Fill(nonce);

        return algorithm switch
        {
            CipherSuiteType.Salsa20 or CipherSuiteType.Chacha20 => SymmetricEngine.Encrypt(key, plaintext, ciphertext, nonce, seq, algorithm, out written),// Assume SymmetricEngine.Encrypt uses an out parameter for written
            CipherSuiteType.Salsa20Poly1305 or CipherSuiteType.Chacha20Poly1305 => AeadEngine.Encrypt(key, plaintext, ciphertext, nonce, aad, seq, algorithm, out written),
            _ => throw new ArgumentException("Unsupported cipher type", nameof(algorithm)),
        };
    }

    /// <summary>
    /// Attempts to decrypt an encrypted envelope.
    /// </summary>
    /// <param name="key">Secret key (length depends on the suite).</param>
    /// <param name="envelope">Concatenation of <c>header || nonce || ciphertext</c> [|| <c>tag</c>].</param>
    /// <param name="plaintext">
    /// On success, receives a newly allocated plaintext buffer; otherwise set to <c>null</c>.
    /// </param>
    /// <param name="aad">
    /// Optional Associated Data (AEAD suites only). Must match the value (if any) used at encryption time.
    /// Ignored for non-AEAD suites.
    /// </param>
    /// <param name="written">Number of plaintext bytes written on success.</param>
    /// <returns>
    /// <c>true</c> if parsing and (for AEAD) authentication succeeded; otherwise <c>false</c>.
    /// </returns>
    /// <remarks>
    /// For AEAD suites, the same AAD convention is used as in encryption:
    /// <c>header || nonce || userAAD</c>.
    /// </remarks>
    /// <example>
    /// <code>
    /// if (EnvelopeCipher.Decrypt(key32, envelope, out var pt, aad))
    /// {
    ///     // use pt
    /// }
    /// else
    /// {
    ///     // failed to authenticate or parse
    /// }
    /// </code>
    /// </example>
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    [return: NotNull]
    public static bool Decrypt(
        ReadOnlySpan<byte> key,
        ReadOnlySpan<byte> envelope,
        Span<byte> plaintext,
        ReadOnlySpan<byte> aad,
        [NotNullWhen(true)] out int written)
    {
        written = 0;

        if (!EnvelopeFormat.TryParseEnvelope(envelope, out EnvelopeFormat.ParsedEnvelope env))
        {
            return false; // Parsing failed, likely not a valid envelope
        }

        switch (env.AeadType)
        {
            case CipherSuiteType.Salsa20:
            case CipherSuiteType.Chacha20:
                // Assume SymmetricEngine.Encrypt uses an out parameter for written
                if (SymmetricEngine.Decrypt(key, envelope, plaintext, out written))
                {
                    return true;
                }

                break;

            case CipherSuiteType.Salsa20Poly1305:
            case CipherSuiteType.Chacha20Poly1305:
                if (AeadEngine.Decrypt(key, envelope, plaintext, aad, out written))
                {
                    return true;
                }

                break;

            default:
                return false;
        }

        return false;
    }

    /// <summary>
    /// Encrypts <paramref name="plaintext"/> using the selected <paramref name="algorithm"/>,
    /// returning a newly allocated envelope buffer.
    /// </summary>
    /// <param name="key">Secret key (length depends on the suite).</param>
    /// <param name="plaintext">Plaintext to encrypt.</param>
    /// <param name="ciphertext">The output ciphertext.</param>
    /// <param name="seq">
    /// Optional 32-bit sequence written into the envelope header. When omitted, a random value
    /// is used. For non-AEAD suites, this value is also used as the initial counter.
    /// </param>
    /// <param name="algorithm">
    /// Cipher suite to use. AEAD suites produce <c>header||nonce||ciphertext||tag</c>;
    /// non-AEAD (stream/CTR) suites produce <c>header||nonce||ciphertext</c>.
    /// </param>
    /// <param name="written">Written output</param>
    /// <returns>
    /// A newly allocated byte array containing the full envelope.
    /// </returns>
    /// <exception cref="ArgumentException">
    /// Thrown if <paramref name="algorithm"/> is not recognized. Key/nonce length errors may be
    /// thrown by the underlying engines.
    /// </exception>
    /// <example>
    /// <code>
    /// // AEAD example (CHACHA20-Poly1305)
    /// var ct = EnvelopeCipher.Encrypt(key32, data, CipherSuiteType.CHACHA20_POLY1305, aad);
    ///
    /// // Stream/CTR example (CHACHA20)
    /// var ct2 = EnvelopeCipher.Encrypt(key32, data, CipherSuiteType.CHACHA20);
    /// </code>
    /// </example>
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    [return: NotNull]
    public static bool Encrypt(
        ReadOnlySpan<byte> key,
        ReadOnlySpan<byte> plaintext,
        Span<byte> ciphertext,
        uint? seq, CipherSuiteType algorithm,
        [NotNullWhen(true)] out int written) => Encrypt(key, plaintext, ciphertext, default, seq, algorithm, out written);

    /// <summary>
    /// Attempts to decrypt an encrypted envelope.
    /// </summary>
    /// <param name="key">Secret key (length depends on the suite).</param>
    /// <param name="envelope">Concatenation of <c>header || nonce || ciphertext</c> [|| <c>tag</c>].</param>
    /// <param name="plaintext">
    /// On success, receives a newly allocated plaintext buffer; otherwise set to <c>null</c>.
    /// </param>
    /// <param name="written">Number of plaintext bytes written on success.</param>
    /// <returns>
    /// <c>true</c> if parsing and (for AEAD) authentication succeeded; otherwise <c>false</c>.
    /// </returns>
    /// <remarks>
    /// For AEAD suites, the same AAD convention is used as in encryption:
    /// <c>header || nonce || userAAD</c>.
    /// </remarks>
    /// <example>
    /// <code>
    /// if (EnvelopeCipher.Decrypt(key32, envelope, out var pt, aad))
    /// {
    ///     // use pt
    /// }
    /// else
    /// {
    ///     // failed to authenticate or parse
    /// }
    /// </code>
    /// </example>
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    [return: NotNull]
    public static bool Decrypt(
        ReadOnlySpan<byte> key,
        ReadOnlySpan<byte> envelope,
        Span<byte> plaintext,
        [NotNullWhen(true)] out int written) => Decrypt(key, envelope, plaintext, default, out written);
}
