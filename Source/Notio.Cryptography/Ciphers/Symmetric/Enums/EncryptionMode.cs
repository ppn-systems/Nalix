namespace Notio.Cryptography.Ciphers.Symmetric.Enums;

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
    /// AES encryption with GCM (Galois/Counter Mode) for authenticated encryption.
    /// </summary>
    AesGcm,

    /// <summary>
    /// AES encryption with CTR (Counter) mode.
    /// </summary>
    AesCtr,

    /// <summary>
    /// AES encryption with CBC (Cipher Block Chaining) mode.
    /// </summary>
    AesCbc,

    /// <summary>
    /// ChaCha20 encryption with Poly1305 for authenticated encryption.
    /// </summary>
    ChaCha20Poly1305
}