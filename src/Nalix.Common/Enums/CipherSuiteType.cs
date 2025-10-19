// Copyright (c) 2025 PPN Corporation. All rights reserved.

namespace Nalix.Common.Enums;

/// <summary>
/// Defines the supported symmetric and AEAD (Authenticated Encryption with Associated Data)
/// cipher suites available within the <c>Nalix.Framework.Cryptography</c> subsystem.
/// </summary>
/// <remarks>
/// <para>
/// This enumeration unifies both base symmetric ciphers and their AEAD counterparts
/// that combine a symmetric cipher with a <c>Poly1305</c> message authentication code.
/// </para>
/// <para>
/// Each member may represent one of two categories:
/// <list type="bullet">
/// <item>
/// <description>
/// <strong>Symmetric cipher</strong> — A standalone encryption algorithm that provides
/// confidentiality but not authentication (e.g., <c>Speck</c>, <c>ChaCha20</c>).
/// </description>
/// </item>
/// <item>
/// <description>
/// <strong>AEAD suite</strong> — A combined construction offering both confidentiality
/// and integrity protection, such as <c>ChaCha20-Poly1305</c> or <c>XTEA-Poly1305</c>.
/// </description>
/// </item>
/// </list>
/// </para>
/// <para>
/// Used throughout the Nalix cryptographic engine for algorithm selection,
/// factory dispatching, and format serialization.
/// </para>
/// </remarks>
public enum CipherSuiteType : System.Byte
{
    // ────────────────────────────────
    // Base symmetric ciphers
    // ────────────────────────────────

    /// <summary>
    /// XTEA (Extended Tiny Encryption Algorithm) block cipher.
    /// <para>
    /// A lightweight 64-bit block cipher with a 128-bit key.
    /// Commonly used in embedded or resource-constrained environments.
    /// </para>
    /// </summary>
    Xtea = 1,

    /// <summary>
    /// Speck lightweight block cipher.
    /// <para>
    /// Designed by the NSA for efficiency on constrained hardware.
    /// Although not standardized, it remains useful for internal testing
    /// and benchmarking scenarios.
    /// </para>
    /// </summary>
    Speck = 2,

    /// <summary>
    /// Salsa20 stream cipher.
    /// <para>
    /// A fast and simple stream cipher by Daniel J. Bernstein,
    /// known for excellent performance on general-purpose CPUs.
    /// Serves as the predecessor of <c>ChaCha20</c>.
    /// </para>
    /// </summary>
    Salsa20 = 3,

    /// <summary>
    /// ChaCha20 stream cipher.
    /// <para>
    /// A secure and efficient cipher standardized in RFC 8439.
    /// Provides high performance across both software and hardware implementations.
    /// </para>
    /// </summary>
    ChaCha20 = 4,

    // ────────────────────────────────
    // AEAD cipher suites (cipher + Poly1305)
    // ────────────────────────────────

    /// <summary>
    /// XTEA cipher combined with Poly1305 MAC (<c>XTEA-Poly1305</c>).
    /// <para>
    /// Provides authenticated encryption using the XTEA block cipher
    /// with a Poly1305 one-time authenticator.
    /// Intended for low-resource systems requiring integrity assurance.
    /// </para>
    /// </summary>
    XteaPoly1305 = 5,

    /// <summary>
    /// Speck cipher combined with Poly1305 MAC (<c>Speck-Poly1305</c>).
    /// <para>
    /// Constructs an AEAD mode based on the Speck cipher with
    /// a Poly1305 authenticator for message integrity.
    /// Used for research and controlled internal cryptographic evaluations.
    /// </para>
    /// </summary>
    SpeckPoly1305 = 6,

    /// <summary>
    /// Salsa20 cipher combined with Poly1305 MAC (<c>Salsa20-Poly1305</c>).
    /// <para>
    /// AEAD construction widely used in NaCl and libsodium libraries.
    /// Offers fast, secure authenticated encryption with proven reliability.
    /// </para>
    /// </summary>
    Salsa20Poly1305 = 7,

    /// <summary>
    /// ChaCha20 cipher combined with Poly1305 MAC (<c>ChaCha20-Poly1305</c>).
    /// <para>
    /// The modern, standardized AEAD suite defined in RFC 8439.
    /// Extensively deployed in TLS 1.3, SSH, and modern encryption frameworks.
    /// </para>
    /// </summary>
    ChaCha20Poly1305 = 8
}
