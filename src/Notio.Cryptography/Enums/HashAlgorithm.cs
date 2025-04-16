namespace Notio.Cryptography.Enums;

/// <summary>
/// Supported hash algorithms for HMAC computation.
/// </summary>
public enum HashAlgorithm : byte
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
}
