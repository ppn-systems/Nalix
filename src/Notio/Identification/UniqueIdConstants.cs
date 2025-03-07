namespace Notio.Identification;

/// <summary>
/// Provides constants for Base36 encoding and Unique ID representation.
/// </summary>
public class UniqueIdConstants
{
    /// <summary>
    /// The alphabet used for Base36 encoding (digits 0-9, letters A-Z).
    /// </summary>
    public const string Alphabet = "0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZ";

    /// <summary>
    /// The base of the encoding system (36 = 10 digits + 26 letters).
    /// </summary>
    public const int Base = 36;

    /// <summary>
    /// The minimum length of a Base36 string representation.
    /// Ensures consistent formatting for Unique IDs.
    /// </summary>
    public const int MinBase36Length = 7;

    /// <summary>
    /// The length of a hexadecimal string representation (8 hex digits for a 32-bit unsigned integer).
    /// Commonly used for representing hash values or compact identifiers.
    /// </summary>
    public const int HexLength = 8;
}
