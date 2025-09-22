// Copyright (c) 2025 PPN Corporation. All rights reserved.

namespace Nalix.Common.Enums;

/// <summary>
/// Specifies the symmetric encryption algorithms and modes supported by the system.
/// </summary>
public enum SymmetricAlgorithmType : System.Byte
{
    /// <summary>
    /// No encryption is applied to the data.
    /// </summary>
    None = 0,

    /// <summary>
    /// ChaCha20 stream cipher combined with Poly1305 for authenticated encryption.
    /// Provides high performance and modern security guarantees.
    /// </summary>
    ChaCha20Poly1305 = 1,

    /// <summary>
    /// Salsa20 stream cipher, a fast and secure algorithm similar in design to ChaCha20.
    /// </summary>
    Salsa20 = 2,

    /// <summary>
    /// Speck block cipher in ECB (Electronic Codebook) mode.
    /// </summary>
    Speck = 3,

    /// <summary>
    /// XTEA (eXtended Tiny Encryption Algorithm) block cipher.
    /// </summary>
    XTEA = 8,
}
