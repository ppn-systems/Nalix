namespace Nalix.Shared.LZ4.Encoders;

/// <summary>
/// Shared constants.
/// </summary>
internal static class LZ4Constants
{
    public const int MinMatchLength = 4;
    public const int MaxOffset = 65535; // Limited by ushort offset storage
    public const int LastLiteralSize = 5; // Need 5 bytes margin for last literal run encoding

    // Token details (similar to LZ4)
    // High 4 bits: Literal Length indicator
    // Low 4 bits: Match Length indicator (relative to MinMatchLength)

    // Max match length (relative) fitting directly in token
    public const int TokenMatchMask = 0x0F;

    // Max literal length fitting directly in token
    public const int TokenLiteralMask = 0x0F;
}
