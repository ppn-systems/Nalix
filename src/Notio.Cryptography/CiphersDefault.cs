namespace Notio.Cryptography;

/// <summary>
/// Provides default values for cryptographic ciphers.
/// </summary>
public static class CiphersDefault
{
    /// <summary>
    /// The number of iterations
    /// </summary>
    public const uint DefaultTimeCost = 3;

    /// <summary>
    /// The amount of memory to use in kibibytes (KiB)
    /// </summary>
    public const uint DefaultMemoryCost = 1 << 12;

    /// <summary>
    /// The number of threads and compute lanes to use
    /// </summary>
    public const uint DefaultDegreeOfParallelism = 1;

    /// <summary>
    /// The desired length of the salt in bytes
    /// </summary>
    public const uint DefaultSaltLength = 16;

    /// <summary>
    /// The desired length of the hash in bytes
    /// </summary>
    public const uint DefaultHashLength = 32;

    /// <summary>
    /// The encoding to use for converting strings to byte arrays
    /// </summary>
    public static readonly System.Text.Encoding DefaultEncoding = System.Text.Encoding.UTF8;
}
