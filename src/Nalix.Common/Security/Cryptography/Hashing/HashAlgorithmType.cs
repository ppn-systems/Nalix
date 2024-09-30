namespace Nalix.Common.Security.Cryptography.Hashing;

/// <summary>
/// Supported hash algorithms for HMAC computation.
/// </summary>
public enum HashAlgorithmType : System.Byte
{
    /// <summary>
    /// No hash algorithm specified
    /// </summary>
    None = 0,

    /// <summary>
    /// SHA-1 hash algorithm (160-bit output)
    /// </summary>
    Sha1 = 1,

    /// <summary>
    /// SHA-224 hash algorithm (224-bit output)
    /// </summary>
    Sha224 = 2,

    /// <summary>
    /// SHA-256 hash algorithm (256-bit output)
    /// </summary>
    Sha256 = 3,

    /// <summary>
    /// SHA-384 hash algorithm (384-bit output)
    /// </summary>
    Sha384 = 4,

    /// <summary>
    /// SHA-512 hash algorithm (512-bit output)
    /// </summary>
    Sha512 = 5,
}