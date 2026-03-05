// Copyright (c) 2025-2026 PPN Corporation. All rights reserved.

using Nalix.Common.Enums;
using Nalix.Framework.Random;
using Nalix.Shared.Security.Engine;
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
[System.Diagnostics.DebuggerNonUserCode]
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
    public const System.Int32 EncryptionOverheadBytes = EnvelopeFormat.TagSize + EnvelopeFormat.HeaderSize;

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
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public static System.Int32 GetNonceLength(CipherSuiteType type) => type switch
    {
        CipherSuiteType.CHACHA20 => ChaCha20.NonceSize,
        CipherSuiteType.CHACHA20_POLY1305 => ChaCha20.NonceSize,
        CipherSuiteType.SALSA20 => Salsa20.NonceSize,
        CipherSuiteType.SALSA20_POLY1305 => Salsa20.NonceSize,
        _ => throw new System.ArgumentException("Unsupported symmetric algorithm", nameof(type))
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
    /// <exception cref="System.ArgumentException">
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
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
    [return: System.Diagnostics.CodeAnalysis.NotNull]
    public static System.Boolean Encrypt(
        [System.Diagnostics.CodeAnalysis.NotNull] System.ReadOnlySpan<System.Byte> key,
        [System.Diagnostics.CodeAnalysis.NotNull] System.ReadOnlySpan<System.Byte> plaintext,
        [System.Diagnostics.CodeAnalysis.NotNull] System.Span<System.Byte> ciphertext,
        System.ReadOnlySpan<System.Byte> aad, System.UInt32? seq, CipherSuiteType algorithm,
        [System.Diagnostics.CodeAnalysis.NotNullWhen(true)] out System.Int32 written)
    {
        written = 0;

        System.Int32 nonceLength = GetNonceLength(algorithm);
        System.Span<System.Byte> nonceStack = stackalloc System.Byte[System.Math.Max(16, nonceLength)];
        System.Span<System.Byte> nonce = nonceStack[..nonceLength];
        Csprng.Fill(nonce);

        switch (algorithm)
        {
            case CipherSuiteType.SALSA20:
            case CipherSuiteType.CHACHA20:
                {
                    // Assume SymmetricEngine.Encrypt uses an out parameter for written
                    SymmetricEngine.Encrypt(key, plaintext, ciphertext, nonce, seq, algorithm, out written);
                    break;
                }

            case CipherSuiteType.SALSA20_POLY1305:
            case CipherSuiteType.CHACHA20_POLY1305:
                {
                    AeadEngine.Encrypt(key, plaintext, ciphertext, nonce, aad, seq, algorithm, out written);
                    break;
                }

            default:
                throw new System.ArgumentException("Unsupported cipher type", nameof(algorithm));
        }

        return true;
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
    /// <param name="written"></param>
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
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
    [return: System.Diagnostics.CodeAnalysis.NotNull]
    public static System.Boolean Decrypt(
        [System.Diagnostics.CodeAnalysis.NotNull] System.ReadOnlySpan<System.Byte> key,
        [System.Diagnostics.CodeAnalysis.NotNull] System.ReadOnlySpan<System.Byte> envelope,
        [System.Diagnostics.CodeAnalysis.NotNull] System.Span<System.Byte> plaintext,
        System.ReadOnlySpan<System.Byte> aad,
        [System.Diagnostics.CodeAnalysis.NotNullWhen(true)] out System.Int32 written)
    {
        written = 0;

        if (!EnvelopeFormat.TryParseEnvelope(envelope, out EnvelopeFormat.ParsedEnvelope env))
        {
            return false; // Parsing failed, likely not a valid envelope
        }

        switch (env.AeadType)
        {
            case CipherSuiteType.SALSA20:
            case CipherSuiteType.CHACHA20:
                {
                    // Assume SymmetricEngine.Encrypt uses an out parameter for written
                    if (SymmetricEngine.Decrypt(key, envelope, plaintext, out written))
                    {
                        return true;
                    }

                    break;
                }

            case CipherSuiteType.SALSA20_POLY1305:
            case CipherSuiteType.CHACHA20_POLY1305:
                {
                    if (AeadEngine.Decrypt(key, envelope, plaintext, aad, out written))
                    {
                        return true;
                    }

                    break;
                }

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
    /// <exception cref="System.ArgumentException">
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
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
    [return: System.Diagnostics.CodeAnalysis.NotNull]
    public static System.Boolean Encrypt(
        [System.Diagnostics.CodeAnalysis.NotNull] System.ReadOnlySpan<System.Byte> key,
        [System.Diagnostics.CodeAnalysis.NotNull] System.ReadOnlySpan<System.Byte> plaintext,
        [System.Diagnostics.CodeAnalysis.NotNull] System.Span<System.Byte> ciphertext,
        System.UInt32? seq, CipherSuiteType algorithm,
        [System.Diagnostics.CodeAnalysis.NotNullWhen(true)] out System.Int32 written) => Encrypt(key, plaintext, ciphertext, default, seq, algorithm, out written);

    /// <summary>
    /// Attempts to decrypt an encrypted envelope.
    /// </summary>
    /// <param name="key">Secret key (length depends on the suite).</param>
    /// <param name="envelope">Concatenation of <c>header || nonce || ciphertext</c> [|| <c>tag</c>].</param>
    /// <param name="plaintext">
    /// On success, receives a newly allocated plaintext buffer; otherwise set to <c>null</c>.
    /// </param>
    /// <param name="written"></param>
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
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
    [return: System.Diagnostics.CodeAnalysis.NotNull]
    public static System.Boolean Decrypt(
        [System.Diagnostics.CodeAnalysis.NotNull] System.ReadOnlySpan<System.Byte> key,
        [System.Diagnostics.CodeAnalysis.NotNull] System.ReadOnlySpan<System.Byte> envelope,
        [System.Diagnostics.CodeAnalysis.NotNull] System.Span<System.Byte> plaintext,
        [System.Diagnostics.CodeAnalysis.NotNullWhen(true)] out System.Int32 written) => Decrypt(key, envelope, plaintext, default, out written);
}