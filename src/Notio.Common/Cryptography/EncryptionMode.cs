namespace Notio.Common.Cryptography;

/// <summary>
/// Specifies the encryption modes available for symmetric encryption.
/// </summary>
public enum EncryptionMode : byte
{
    /// <summary>
    /// XTEA (Extended TEA) encryption algorithm.
    /// </summary>
    Xtea,

    /// <summary>
    /// ChaCha20 encryption with Poly1305 for authenticated encryption.
    /// </summary>
    ChaCha20Poly1305
}
