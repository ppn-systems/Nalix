namespace Nalix.Shared.LZ4.Encoders;

/// <summary>
/// Provides LZ4-related compression constants used across the encoder implementation.
/// </summary>
public static class LZ4Constants
{
    /// <summary>
    /// The minimum length of a match for LZ4 compression.
    /// </summary>
    public const int MinMatchLength = 4;

    /// <summary>
    /// The maximum backward offset allowed for a match (limited by 16-bit offset).
    /// </summary>
    public const int MaxOffset = 65535;

    /// <summary>
    /// The number of trailing bytes required to safely encode a final literal segment.
    /// </summary>
    public const int LastLiteralSize = 5;

    /// <summary>
    /// A bitmask for extracting the match length from the token (low 4 bits).
    /// </summary>
    public const int TokenMatchMask = 0x0F;

    /// <summary>
    /// A bitmask for extracting the literal length from the token (high 4 bits).
    /// </summary>
    public const int TokenLiteralMask = 0x0F;
}
