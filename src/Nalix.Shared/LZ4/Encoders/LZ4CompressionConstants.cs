// Copyright (c) 2025 PPN Corporation. All rights reserved.

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
    public const System.Int32 MinMatchLength = 0x4;

    /// <summary>
    /// The maximum backward offset allowed for a match in LZ4 format.
    /// </summary>
    /// <remarks>
    /// This is a HARD LIMIT defined by the LZ4 specification.
    /// The offset field in compressed data is encoded as a 16-bit unsigned integer (2 bytes),
    /// which limits the maximum backward reference distance to 65,535 bytes (64KB - 1).
    /// <para>
    /// ⚠️ <b>DO NOT CHANGE THIS VALUE</b> - it would break LZ4 format compatibility.
    /// </para>
    /// </remarks>
    public const System.Int32 MaxOffset = 0xFFFF;

    /// <summary>
    /// The maximum block size supported by this implementation.
    /// </summary>
    /// <remarks>
    /// Set to 256KB to match LZ4 High Compression (HC) standard block size.
    /// This is an IMPLEMENTATION LIMIT (not a format constraint) chosen to balance:
    /// <list type="bullet">
    ///   <item><description>Compression efficiency for typical use cases</description></item>
    ///   <item><description>Memory safety (prevents excessive allocations from malformed data)</description></item>
    ///   <item><description>Performance (cache-friendly buffer sizes)</description></item>
    /// </list>
    /// <para>
    /// Note: LZ4 format theoretically supports blocks up to 2GB, but we limit to 256KB
    /// for practical reasons. For larger data, use chunked/streaming compression.
    /// </para>
    /// </remarks>
    public const System.Int32 MaxBlockSize = 0x40000;

    /// <summary>
    /// The number of trailing bytes required to safely encode a final literal segment.
    /// </summary>
    /// <remarks>
    /// This constant ensures there's enough space at the end of the input buffer
    /// to safely process the last few bytes without bounds checking in the hot loop.
    /// </remarks>
    public const System.Int32 LastLiteralSize = 0x5;

    /// <summary>
    /// A bitmask for extracting the match length from the token (low 4 bits).
    /// </summary>
    /// <remarks>
    /// LZ4 token structure (1 byte):
    /// <code>
    /// [7: 4] Literal length (0-15)
    /// [3:0] Match length (0-15)
    /// </code>
    /// </remarks>
    public const System.Int32 TokenMatchMask = 0x0F;

    /// <summary>
    /// A bitmask for extracting the literal length from the token (high 4 bits).
    /// </summary>
    /// <remarks>
    /// LZ4 token structure (1 byte):
    /// <code>
    /// [7:4] Literal length (0-15)
    /// [3:0] Match length (0-15)
    /// </code>
    /// </remarks>
    public const System.Int32 TokenLiteralMask = 0x0F;
}