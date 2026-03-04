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
/// confidentiality but not authentication (e.g., <c>SPECK</c>, <c>CHACHA20</c>).
/// </description>
/// </item>
/// <item>
/// <description>
/// <strong>AEAD suite</strong> — A combined construction offering both confidentiality
/// and integrity protection, such as <c>CHACHA20-Poly1305</c> or <c>XTEA-Poly1305</c>.
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
    /// </summary>
    /// <remarks>
    /// <para>
    /// <strong>⚠️ Legacy cipher warning:</strong>
    /// </para>
    /// <para>
    /// XTEA is a lightweight 64-bit block cipher with a 128-bit key,
    /// designed for simplicity and minimal code size.
    /// </para>
    /// <para>
    /// Due to its small block size and age, XTEA is vulnerable to
    /// block collision risks when encrypting large volumes of data
    /// and is <strong>not recommended for modern cryptographic systems</strong>.
    /// </para>
    /// <para>
    /// Suitable only for low-volume data, legacy compatibility,
    /// or constrained environments with clearly defined threat models.
    /// </para>
    /// </remarks>
    XTEA = 1,

    /// <summary>
    /// SPECK lightweight block cipher.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <strong>⚠️ Security and compliance notice:</strong>
    /// </para>
    /// <para>
    /// SPECK is a lightweight block cipher designed by the
    /// :contentReference[oaicite:0]{index=0} (NSA)
    /// for constrained environments.
    /// </para>
    /// <para>
    /// While no practical cryptographic breaks are currently known,
    /// SPECK is <strong>not standardized</strong> by NIST or any major
    /// international standards body and is <strong>not recommended for
    /// production use</strong> in security-sensitive or compliance-driven systems.
    /// </para>
    /// <para>
    /// This algorithm is retained strictly for:
    /// <list type="bullet">
    /// <item><description>Research and academic evaluation</description></item>
    /// <item><description>Internal benchmarking and comparison</description></item>
    /// <item><description>Legacy or isolated experimental systems</description></item>
    /// </list>
    /// </para>
    /// </remarks>
    SPECK = 2,

    /// <summary>
    /// SALSA20 stream cipher.
    /// <para>
    /// A fast and simple stream cipher by Daniel J. Bernstein,
    /// known for excellent performance on general-purpose CPUs.
    /// Serves as the predecessor of <c>CHACHA20</c>.
    /// </para>
    /// </summary>
    /// <remarks>
    /// <para>
    /// <strong>ℹ️ Superseded algorithm notice:</strong>
    /// </para>
    /// <para>
    /// SALSA20 is a well-studied and secure stream cipher designed by
    /// Daniel J. Bernstein. While still considered cryptographically sound,
    /// it has largely been superseded by <c>CHACHA20</c>, which offers
    /// improved diffusion and wider standardization.
    /// </para>
    /// <para>
    /// Prefer <c>CHACHA20</c> for new designs.
    /// </para>
    /// </remarks>
    SALSA20 = 3,

    /// <summary>
    /// CHACHA20 stream cipher.
    /// <para>
    /// A secure and efficient cipher standardized in RFC 8439.
    /// Provides high performance across both software and hardware implementations.
    /// </para>
    /// </summary>
    CHACHA20 = 4,

    // ────────────────────────────────
    // AEAD cipher suites (cipher + Poly1305)
    // ────────────────────────────────

    /// <summary>
    /// XTEA cipher combined with Poly1305 MAC (<c>XTEA-Poly1305</c>).
    /// </summary>
    /// <remarks>
    /// <para>
    /// <strong>⚠️ Limited-security AEAD construction:</strong>
    /// </para>
    /// <para>
    /// While Poly1305 provides strong message authentication,
    /// the overall security of this AEAD suite remains constrained
    /// by the underlying XTEA block cipher.
    /// </para>
    /// <para>
    /// This construction should not be used for encrypting
    /// large data streams or in high-assurance security contexts.
    /// </para>
    /// </remarks>
    XTEA_POLY1305 = 5,

    /// <summary>
    /// SPECK cipher combined with Poly1305 MAC (<c>SPECK-Poly1305</c>).
    /// </summary>
    /// <remarks>
    /// <para>
    /// <strong>⚠️ Experimental AEAD construction:</strong>
    /// </para>
    /// <para>
    /// This AEAD suite combines the SPECK block cipher with the Poly1305
    /// message authentication code to provide confidentiality and integrity.
    /// </para>
    /// <para>
    /// Due to the non-standardized status of SPECK and limited
    /// independent cryptographic review, this construction is
    /// <strong>not suitable for production deployments</strong>,
    /// especially in environments requiring regulatory compliance,
    /// formal certification, or long-term cryptographic assurance.
    /// </para>
    /// <para>
    /// Intended exclusively for controlled internal testing and benchmarking.
    /// </para>
    /// </remarks>
    SPECK_POLY1305 = 6,

    /// <summary>
    /// SALSA20 cipher combined with Poly1305 MAC (<c>SALSA20-Poly1305</c>).
    /// <para>
    /// AEAD construction widely used in NaCl and libsodium libraries.
    /// Offers fast, secure authenticated encryption with proven reliability.
    /// </para>
    /// </summary>
    SALSA20_POLY1305 = 7,

    /// <summary>
    /// CHACHA20 cipher combined with Poly1305 MAC (<c>CHACHA20-Poly1305</c>).
    /// <para>
    /// The modern, standardized AEAD suite defined in RFC 8439.
    /// Extensively deployed in TLS 1.3, SSH, and modern encryption frameworks.
    /// </para>
    /// </summary>
    CHACHA20_POLY1305 = 8
}
