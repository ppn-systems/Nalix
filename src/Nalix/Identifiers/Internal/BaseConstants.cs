namespace Nalix.Identifiers.Internal;

/// <summary>
/// Provides constants for numeric encoding systems like Base36Value, Base58Value, and Hex.
/// </summary>
internal static class BaseConstants
{
    /// <summary>
    /// The Base32Value alphabet.
    /// </summary>
    public const string Alphabet32 = "ABCDEFGHIJKLMNOPQRSTUVWXYZ234567";

    /// <summary>
    /// The alphabet used for Base36Value encoding (digits 0-9, letters A-Z).
    /// </summary>
    public const string Alphabet36 = "0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZ";

    /// <summary>
    /// The Base58Value alphabet (removes easily confused characters like 0, O, I, l).
    /// </summary>
    public const string Alphabet58 = "123456789ABCDEFGHJKLMNPQRSTUVWXYZabcdefghijkmnopqrstuvwxyz";

    /// <summary>
    /// The Base64Value alphabet.
    /// </summary>
    public const string Alphabet64 = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789+/";

    /// <summary>
    /// The base value for Base32Value encoding.
    /// </summary>
    public const uint Base32Value = 32;

    /// <summary>
    /// The base value for Base36Value encoding.
    /// </summary>
    public const int Base36Value = 36;

    /// <summary>
    /// The base value for Base58Value encoding.
    /// </summary>
    public const int Base58Value = 58;

    /// <summary>
    /// The base value for Base64Value encoding.
    /// </summary>
    public const int Base64Value = 64;

    /// <summary>
    /// The length of a hexadecimal string representation (8 hex digits for a 32-bit unsigned integer).
    /// Commonly used for representing hash values or compact identifiers.
    /// </summary>
    public const int HexLength = 8;
}
