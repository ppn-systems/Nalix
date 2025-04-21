namespace Nalix.Defaults;

/// <summary>
/// Provides constants for numeric encoding systems like Base36, Base58, and Hex.
/// </summary>
public static class DefaultEncodings
{
    /// <summary>
    /// The Base32 alphabet.
    /// </summary>
    public const string Base32Alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZ234567";

    /// <summary>
    /// The alphabet used for Base36 encoding (digits 0-9, letters A-Z).
    /// </summary>
    public const string Base36Alphabet = "0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZ";

    /// <summary>
    /// The Base58 alphabet (removes easily confused characters like 0, O, I, l).
    /// </summary>
    public const string Base58Alphabet = "123456789ABCDEFGHJKLMNPQRSTUVWXYZabcdefghijkmnopqrstuvwxyz";

    /// <summary>
    /// The Base64 alphabet.
    /// </summary>
    public const string Base64Alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789+/";

    /// <summary>
    /// The base value for Base32 encoding.
    /// </summary>
    public const uint Base32 = 32;

    /// <summary>
    /// The base value for Base36 encoding.
    /// </summary>
    public const int Base36 = 36;

    /// <summary>
    /// The base value for Base58 encoding.
    /// </summary>
    public const int Base58 = 58;

    /// <summary>
    /// The base value for Base64 encoding.
    /// </summary>
    public const int Base64 = 64;

    /// <summary>
    /// The length of a hexadecimal string representation (8 hex digits for a 32-bit unsigned integer).
    /// Commonly used for representing hash values or compact identifiers.
    /// </summary>
    public const int HexLength = 8;
}
