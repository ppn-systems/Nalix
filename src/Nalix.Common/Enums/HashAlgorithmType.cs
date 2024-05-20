// Copyright (c) 2025 PPN Corporation. All rights reserved.

namespace Nalix.Common.Enums;

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
    /// SHA-256 hash algorithm producing a 256-bit (32-byte) output.
    /// </summary>
    Sha256 = 32,

    /// <summary>
    /// SHA-512 hash algorithm producing a 512-bit (64-byte) output.
    /// </summary>
    Sha512 = 64,
}
