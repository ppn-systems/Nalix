// Copyright (c) 2025 PPN Corporation. All rights reserved.

namespace Nalix.Logging.Internal.Pooling;

/// <summary>
/// String interning cache for frequently used logging strings.
/// Reduces memory pressure by reusing immutable string instances.
/// </summary>
[System.Diagnostics.DebuggerNonUserCode]
[System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverage]
internal static class InternCache
{
    /// <summary>
    /// Gets the pre-interned opening bracket string.
    /// </summary>
    public static System.String BracketOpen { get; } = "[";

    /// <summary>
    /// Gets the pre-interned closing bracket string.
    /// </summary>
    public static System.String BracketClose { get; } = "]";

    /// <summary>
    /// Gets the pre-interned space string.
    /// </summary>
    public static System.String Space { get; } = " ";

    /// <summary>
    /// Gets the pre-interned dash with spaces string.
    /// </summary>
    public static System.String DashWithSpaces { get; } = " - ";

    /// <summary>
    /// Gets the pre-interned colon string.
    /// </summary>
    public static System.String Colon { get; } = ":";

    /// <summary>
    /// Gets the pre-interned newline string.
    /// </summary>
    public static System.String NewLine { get; } = System.Environment.NewLine;
}
