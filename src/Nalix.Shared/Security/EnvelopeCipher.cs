// Copyright (c) 2025 PPN Corporation. All rights reserved.

using Nalix.Common.Enums;
using Nalix.Shared.Security.Engine;

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
    /// Encrypts <paramref name="plaintext"/> using the selected <paramref name="algorithm"/>,
    /// returning a newly allocated envelope buffer.
    /// </summary>
    /// <param name="key">Secret key (length depends on the suite).</param>
    /// <param name="plaintext">Plaintext to encrypt.</param>
    /// <param name="algorithm">
    /// Cipher suite to use. AEAD suites produce <c>header||nonce||ciphertext||tag</c>;
    /// non-AEAD (stream/CTR) suites produce <c>header||nonce||ciphertext</c>.
    /// </param>
    /// <param name="aad">
    /// Optional Associated Data (AEAD suites only). It is combined with header and nonce as
    /// <c>header || nonce || userAAD</c> under the tag.
    /// Ignored for non-AEAD suites.
    /// </param>
    /// <param name="seq">
    /// Optional 32-bit sequence written into the envelope header. When omitted, a random value
    /// is used. For non-AEAD suites, this value is also used as the initial counter.
    /// </param>
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
    public static System.Byte[] Encrypt(
        [System.Diagnostics.CodeAnalysis.NotNull] System.ReadOnlySpan<System.Byte> key,
        [System.Diagnostics.CodeAnalysis.NotNull] System.ReadOnlySpan<System.Byte> plaintext,
        [System.Diagnostics.CodeAnalysis.NotNull] CipherSuiteType algorithm,
        [System.Diagnostics.CodeAnalysis.NotNull] System.ReadOnlySpan<System.Byte> aad = default,
        [System.Diagnostics.CodeAnalysis.AllowNull] System.UInt32? seq = null)
    {
        return algorithm switch
        {
            CipherSuiteType.XTEA or
            CipherSuiteType.SPECK or
            CipherSuiteType.SALSA20 or
            CipherSuiteType.CHACHA20
                => SymmetricEngine.Encrypt(key, plaintext, algorithm, default, seq),

            CipherSuiteType.XTEA_POLY1305 or
            CipherSuiteType.SPECK_POLY1305 or
            CipherSuiteType.SALSA20_POLY1305 or
            CipherSuiteType.CHACHA20_POLY1305
                => AeadEngine.Encrypt(key, plaintext, algorithm, aad, seq),

            _ => throw new System.ArgumentException("Unsupported cipher type", nameof(algorithm))
        };
    }

    /// <summary>
    /// Attempts to decrypt an encrypted envelope produced by <see cref="Encrypt"/>.
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
        [System.Diagnostics.CodeAnalysis.NotNullWhen(true)] out System.Byte[]? plaintext,
        [System.Diagnostics.CodeAnalysis.NotNull] System.ReadOnlySpan<System.Byte> aad = default)
    {
        plaintext = null;

        // Quick parse to determine which engine to route to
        if (!EnvelopeFormat.TryParseEnvelope(envelope, out EnvelopeFormat.ParsedEnvelope env))
        {
            return false;
        }

        return env.AeadType switch
        {
            CipherSuiteType.XTEA or
            CipherSuiteType.SPECK or
            CipherSuiteType.SALSA20 or
            CipherSuiteType.CHACHA20
                => SymmetricEngine.Decrypt(key, envelope, out plaintext),

            CipherSuiteType.XTEA_POLY1305 or
            CipherSuiteType.SPECK_POLY1305 or
            CipherSuiteType.SALSA20_POLY1305 or
            CipherSuiteType.CHACHA20_POLY1305
                => AeadEngine.Decrypt(key, envelope, out plaintext, aad),

            _ => false
        };
    }
}
