namespace Notio.Identification;

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
    /// </summary>
    public const int MinBase36Length = 7;

    /// <summary>
    /// The length of a hexadecimal string representation (8 hex digits for a uint).
    /// </summary>
    public const int HexLength = 8;
}
