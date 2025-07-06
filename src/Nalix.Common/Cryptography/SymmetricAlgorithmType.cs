namespace Nalix.Common.Cryptography;

/// <summary>
/// Specifies the encryption modes available for symmetric encryption.
/// </summary>
public enum SymmetricAlgorithmType : System.Byte
{
    /// <summary>
    /// No encryption is applied.
    /// </summary>
    None = 0,

    /// <summary>
    /// ChaCha20 encryption with Poly1305 for authenticated encryption.
    /// </summary>
    ChaCha20Poly1305 = 1,

    /// <summary>
    /// Salsa20 stream cipher.
    /// </summary>
    Salsa20 = 2,

    /// <summary>
    /// Speck block cipher.
    /// </summary>
    Speck = 3,

    /// <summary>
    /// Speck block cipher in CBC mode.
    /// </summary>
    SpeckCBC = 4,

    /// <summary>
    /// Blowfish block cipher.
    /// </summary>
    Blowfish = 5,

    /// <summary>
    /// Twofish block cipher.
    /// </summary>
    TwofishECB = 6,

    /// <summary>
    /// Twofish block cipher.
    /// </summary>
    TwofishCBC = 7,

    /// <summary>
    /// XTEA encryption algorithm.
    /// </summary>
    XTEA = 8,
}