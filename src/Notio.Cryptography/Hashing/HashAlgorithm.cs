namespace Notio.Cryptography.Hashing;

/// <summary>
/// Supported hash algorithms for HMAC computation.
/// </summary>
public enum HashAlgorithm
{
    /// <summary>
    /// SHA-1 hash algorithm (160-bit output)
    /// </summary>
    Sha1,

    /// <summary>
    /// SHA-224 hash algorithm (224-bit output)
    /// </summary>
    Sha224,

    /// <summary>
    /// SHA-256 hash algorithm (256-bit output)
    /// </summary>
    Sha256
}
