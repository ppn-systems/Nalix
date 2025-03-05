namespace Notio.Cryptography;

/// <summary>
/// Provides default values for cryptographic ciphers.
/// </summary>
internal static class CiphersConstants
{
    /// <summary>
    /// Only allowed key lenght in bytes
    /// </summary>
    public const int AllowedKeyLength = 32;

    /// <summary>
    /// Only allowed nonce lenght in bytes
    /// </summary>
    public const int AllowedNonceLength = 12;

    /// <summary>
    /// How many bytes are processed per loop
    /// </summary>
    public const int ProcessBytesAtTime = 64;

    /// <summary>
    /// Size of the public key in bytes.
    /// </summary>
    public const int PublicKeySize = 32;

    /// <summary>
    /// Size of the signature in bytes.
    /// </summary>
    public const int SignatureSize = 64;

    /// <summary>
    /// The number of iterations
    /// </summary>
    public const uint DefaultTimeCost = 0x03;

    /// <summary>
    /// The desired length of the salt in bytes
    /// </summary>
    public const uint DefaultSaltLength = 0x10;

    /// <summary>
    /// The desired length of the hash in bytes
    /// </summary>
    public const uint DefaultHashLength = 0x20;

    /// <summary>
    /// The amount of memory to use in kibibytes (KiB)
    /// </summary>
    public const uint DefaultMemoryCost = 0x1000;

    /// <summary>
    /// The number of threads and compute lanes to use
    /// </summary>
    public const uint DefaultDegreeOfParallelism = 0x01;

    /// <summary>
    /// The encoding to use for converting strings to byte arrays
    /// </summary>
    public static readonly System.Text.Encoding DefaultEncoding = System.Text.Encoding.UTF8;
}
