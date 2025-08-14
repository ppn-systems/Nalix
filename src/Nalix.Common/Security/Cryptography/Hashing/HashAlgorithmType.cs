// Copyright (c) 2025 PPN Corporation. All rights reserved.

namespace Nalix.Common.Security.Cryptography.Hashing;

/// <summary>
/// Specifies the hash algorithms supported for HMAC (Hash-based Message Authentication Code) computation.
/// </summary>
public enum HashAlgorithmType : System.Byte
{
    /// <summary>
    /// No hash algorithm specified.
    /// </summary>
    None = 0,

    /// <summary>
    /// SHA-1 hash algorithm producing a 160-bit (20-byte) output.
    /// </summary>
    Sha1 = 1,

    /// <summary>
    /// SHA-224 hash algorithm producing a 224-bit (28-byte) output.
    /// </summary>
    Sha224 = 2,

    /// <summary>
    /// SHA-256 hash algorithm producing a 256-bit (32-byte) output.
    /// </summary>
    Sha256 = 3,

    /// <summary>
    /// SHA-384 hash algorithm producing a 384-bit (48-byte) output.
    /// </summary>
    Sha384 = 4,

    /// <summary>
    /// SHA-512 hash algorithm producing a 512-bit (64-byte) output.
    /// </summary>
    Sha512 = 5,
}
