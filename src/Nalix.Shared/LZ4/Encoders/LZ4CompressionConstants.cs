namespace Nalix.Shared.LZ4.Encoders;

/// <summary>
/// Provides LZ4-related compression constants used across the encoder implementation.
/// </summary>
[System.Diagnostics.DebuggerNonUserCode]
[System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never)]
public static class LZ4CompressionConstants
{
    /// <summary>
    /// The minimum length of a match for LZ4 compression.
    /// </summary>
    public const System.Int32 MinMatchLength = 4;

    /// <summary>
    /// The maximum backward offset allowed for a match (limited by 16-bit offset).
    /// </summary>
    public const System.Int32 MaxOffset = 65535;

    /// <summary>
    /// The number of trailing bytes required to safely encode a final literal segment.
    /// </summary>
    public const System.Int32 LastLiteralSize = 5;

    /// <summary>
    /// A bitmask for extracting the match length from the token (low 4 bits).
    /// </summary>
    public const System.Int32 TokenMatchMask = 0x0F;

    /// <summary>
    /// A bitmask for extracting the literal length from the token (high 4 bits).
    /// </summary>
    public const System.Int32 TokenLiteralMask = 0x0F;
}
