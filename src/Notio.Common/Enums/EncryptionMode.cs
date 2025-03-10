namespace Notio.Common.Enums;

/// <summary>
/// Specifies the encryption modes available for symmetric encryption.
/// </summary>
public enum EncryptionMode : byte
{
    /// <summary>
    /// ChaCha20 encryption with Poly1305 for authenticated encryption.
    /// </summary>
    ChaCha20Poly1305,

    /// <summary>
    /// Salsa20 stream cipher.
    /// </summary>
    Salsa20,

    /// <summary>
    /// Twofish block cipher.
    /// </summary>
    Twofish,

    /// <summary>
    /// XTEA (Extended TEA) encryption algorithm.
    /// </summary>
    Xtea,
}
