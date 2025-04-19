// Copyright (c) 2025 PPN Corporation. All rights reserved.

namespace Nalix.Common.Enums;

/// <summary>
/// Specifies the supported AEAD (Authenticated Encryption with Associated Data)
/// cipher suites available in the Nalix cryptography subsystem.
/// </summary>
/// <remarks>
/// Each value represents an AEAD suite built from a symmetric cipher
/// combined with a Poly1305 message authentication code.
/// </remarks>
public enum AeadType : System.Byte
{
    /// <summary>
    /// XTEA cipher combined with Poly1305 MAC.
    /// <para>
    /// Lightweight 64-bit block cipher with 128-bit key, suitable for
    /// embedded or constrained environments. Provides basic AEAD protection
    /// but not standardized for high-security use.
    /// </para>
    /// </summary>
    Xtea = 1,

    /// <summary>
    /// Salsa20 stream cipher combined with Poly1305 MAC.
    /// <para>
    /// Designed by Daniel J. Bernstein; provides fast, secure AEAD construction
    /// used in legacy NaCl and libsodium implementations.
    /// </para>
    /// </summary>
    Salsa = 2,

    /// <summary>
    /// Speck cipher combined with Poly1305 MAC.
    /// <para>
    /// Lightweight block cipher designed by NSA for efficiency on constrained
    /// hardware; not standardized but useful for internal experiments.
    /// </para>
    /// </summary>
    Speck = 3,

    /// <summary>
    /// ChaCha stream cipher combined with Poly1305 MAC.
    /// <para>
    /// Standardized in RFC 8439; widely used in TLS 1.3, SSH, and modern AEAD
    /// constructions for secure and efficient authenticated encryption.
    /// </para>
    /// </summary>
    ChaCha = 4
}
