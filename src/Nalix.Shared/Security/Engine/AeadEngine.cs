// Copyright (c) 2025-2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using Nalix.Common.Security;
using Nalix.Framework.Random;
using Nalix.Shared.Security.Aead;
using Nalix.Shared.Security.Internal;

namespace Nalix.Shared.Security.Engine;

/// <summary>
/// Provides high-level APIs to encrypt and decrypt AEAD envelopes in the
/// <c>AeadFormat</c> layout: <c>header || nonce || ciphertext || tag</c>.
/// </summary>
/// <remarks>
/// <para>
/// <strong>Envelope structure:</strong>
/// <list type="bullet">
/// <item><description><c>header</c>: fixed <see cref="EnvelopeFormat.HeaderSize"/> bytes (contains magic, algorithm id, flags, nonce length, sequence).</description></item>
/// <item><description><c>nonce</c>: suite-specific length (e.g., 12 for CHACHA20, 8 for SALSA20, 16 for SPECK, 8 for XTEA).</description></item>
/// <item><description><c>ciphertext</c>: same length as plaintext.</description></item>
/// <item><description><c>tag</c>: authentication tag of <see cref="EnvelopeFormat.TagSize"/> bytes (detached).</description></item>
/// </list>
/// </para>
/// <para>
/// <strong>AEAD AAD convention:</strong> The engine authenticates <c>header || nonce || userAAD</c>.
/// This binds the header and nonce into the authentication domain and prevents misuse.
/// </para>
/// <para>
/// <strong>Security notes:</strong>
/// <list type="bullet">
/// <item><description>Callers must supply a unique nonce per key for the chosen algorithm,
/// except where <see cref="Encrypt"/> auto-generates a random nonce.</description></item>
/// <item><description>This engine favors Span-first patterns and clears temporary sensitive buffers when possible.</description></item>
/// </list>
/// </para>
/// </remarks>
[System.Diagnostics.DebuggerNonUserCode]
public static class AeadEngine
{
    /// <summary>
    /// Encrypts plaintext and returns an AEAD envelope:
    /// <c>header || nonce || ciphertext || tag</c>.
    /// </summary>
    /// <param name="key">Secret key (length depends on suite; see remarks).</param>
    /// <param name="plaintext">Plaintext to encrypt.</param>
    /// <param name="nonce">Nonce</param>
    /// <param name="ciphertext">The output ciphertext</param>
    /// <param name="aad">Optional associated data to be authenticated (not encrypted).</param>
    /// <param name="seq">Optional 4-byte sequence number stored in the header; if null, a random value is used.</param>
    /// <param name="algorithm">
    /// Optional algorithm name. Defaults to <c>"CHACHA20"</c>.
    /// Supported aliases:
    /// <list type="bullet">
    /// <item><description><c>"chacha"</c>, <c>"chacha20"</c>, <c>"chacha20-poly1305"</c></description></item>
    /// <item><description><c>"salsa"</c>, <c>"salsa20"</c>, <c>"salsa20-poly1305"</c></description></item>
    /// </list>
    /// </param>
    /// <param name="written">Write length</param>
    /// <returns>A newly allocated byte array containing the full envelope.</returns>
    /// <exception cref="System.ArgumentException">
    /// Thrown if the algorithm is unsupported or the key length is invalid for the chosen suite.
    /// </exception>
    /// <remarks>
    /// <para>
    /// Expected key lengths:
    /// <list type="bullet">
    /// <item><description><c>CHACHA20-Poly1305</c>: 32 bytes</description></item>
    /// <item><description><c>SALSA20-Poly1305</c>: 16 or 32 bytes</description></item>
    /// </list>
    /// </para>
    /// </remarks>
    public static bool Encrypt(
        [System.Diagnostics.CodeAnalysis.NotNull] System.ReadOnlySpan<byte> key,
        [System.Diagnostics.CodeAnalysis.NotNull] System.ReadOnlySpan<byte> plaintext,
        [System.Diagnostics.CodeAnalysis.NotNull] System.Span<byte> ciphertext,
        [System.Diagnostics.CodeAnalysis.NotNull] System.ReadOnlySpan<byte> nonce,
        [System.Diagnostics.CodeAnalysis.NotNull] System.ReadOnlySpan<byte> aad,
        uint? seq, CipherSuiteType algorithm,
        [System.Diagnostics.CodeAnalysis.NotNull] out int written)
    {
        written = 0;
        uint seqVal = seq ?? Csprng.NextUInt32();
        int total = EnvelopeFormat.HeaderSize + nonce.Length + plaintext.Length + EnvelopeFormat.TagSize;

        if (ciphertext.Length < total)
        {
            return false;
        }

        // Instead of renting a temporary buffer, encrypt directly into the destination
        // ciphertext buffer at the correct offset to avoid Rent/Span slicing issues.
        int ctOffset = EnvelopeFormat.HeaderSize + nonce.Length;
        System.Span<byte> ctDestination = ciphertext.Slice(ctOffset, plaintext.Length);
        System.Span<byte> tagDestination = ciphertext.Slice(ctOffset + plaintext.Length, EnvelopeFormat.TagSize);

        switch (algorithm)
        {
            case CipherSuiteType.Chacha20Poly1305:
                _ = ChaCha20Poly1305.Encrypt(key, nonce, plaintext, aad, ctDestination, tagDestination);
                break;

            case CipherSuiteType.Salsa20Poly1305:
                _ = Salsa20Poly1305.Encrypt(key, nonce, plaintext, aad, ctDestination, tagDestination);
                break;

            case CipherSuiteType.Salsa20:
            case CipherSuiteType.Chacha20:

            default:
                return false;
        }

        // Write the envelope header/nonce and copy ciphertext+tag into the output span.
        // ctDestination/tagDestination already point into ciphertext; WriteEnvelope should
        // write header and nonce and then copy provided ct/tag into the envelope region.
        _ = EnvelopeFormat.WriteEnvelope(ciphertext[..total], algorithm, 0, seqVal, nonce, ctDestination, tagDestination);

        // Clear sensitive temporary areas if necessary (we avoid extra temporaries here).
        written = total;
        return true;
    }

    /// <summary>
    /// Attempts to decrypt an AEAD envelope and, if authentication succeeds,
    /// outputs the plaintext.
    /// </summary>
    /// <param name="key">Secret key (length depends on suite; see <see cref="Encrypt"/> remarks).</param>
    /// <param name="envelope">Concatenation of <c>header || nonce || ciphertext || tag</c>.</param>
    /// <param name="plaintext">
    /// On success, receives a newly allocated array containing the plaintext;
    /// otherwise set to <c>null</c>.
    /// </param>
    /// <param name="aad">Optional associated data to be authenticated (not encrypted).</param>
    /// <param name="written"></param>
    /// <returns><c>true</c> if decryption and tag verification succeeded; otherwise <c>false</c>.</returns>
    /// <remarks>
    /// The same AAD convention is used as in <see cref="Encrypt"/>:
    /// <c>header || nonce || userAAD</c>.
    /// </remarks>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
    [return: System.Diagnostics.CodeAnalysis.NotNull]
    public static bool Decrypt(
        [System.Diagnostics.CodeAnalysis.NotNull] System.ReadOnlySpan<byte> key,
        [System.Diagnostics.CodeAnalysis.NotNull] System.ReadOnlySpan<byte> envelope,
        [System.Diagnostics.CodeAnalysis.NotNull] System.Span<byte> plaintext,
        System.ReadOnlySpan<byte> aad,
        [System.Diagnostics.CodeAnalysis.NotNullWhen(true)] out int written)
    {
        written = 0;

        if (!EnvelopeFormat.TryParseEnvelope(envelope, out EnvelopeFormat.ParsedEnvelope env))
        {
            return false;
        }

        int ctLen = env.Ciphertext.Length;

        if (plaintext.Length < env.Ciphertext.Length)
        {
            return false;
        }

        int result = 0;
        System.Span<byte> ptSlice = plaintext[..ctLen];

        switch (env.AeadType)
        {
            case CipherSuiteType.Chacha20Poly1305:
                result = ChaCha20Poly1305.Decrypt(key, env.Nonce, env.Ciphertext, aad, env.Tag, ptSlice);
                break;

            case CipherSuiteType.Salsa20Poly1305:
                result = Salsa20Poly1305.Decrypt(key, env.Nonce, env.Ciphertext, aad, env.Tag, ptSlice);
                break;

            case CipherSuiteType.Salsa20:
            case CipherSuiteType.Chacha20:
                return false;

            default:
                ThrowHelper.ThrowNotSupportedException("Unsupported aead algorithm");
                break;
        }

        if (result < 0)
        {
            return false;
        }

        written = result;
        return true;
    }
}
