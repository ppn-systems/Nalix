// Copyright (c) 2025 PPN Corporation. All rights reserved.

using System.Runtime.CompilerServices;

namespace Nalix.Logging.Internal.Pooling;

/// <summary>
/// String interning cache for frequently used logging strings.
/// Reduces memory pressure by reusing immutable string instances.
/// </summary>
[System.Diagnostics.DebuggerNonUserCode]
[System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverage]
internal static class InternCache
{
    #region Constants

    // Pre-interned common strings
    private static readonly System.String s_bracketOpen = "[";
    private static readonly System.String s_bracketClose = "]";
    private static readonly System.String s_space = " ";
    private static readonly System.String s_dashWithSpaces = " - ";
    private static readonly System.String s_colon = ":";
    private static readonly System.String s_newLine = System.Environment.NewLine;

    #endregion Constants

    #region Properties

    /// <summary>
    /// Gets the pre-interned opening bracket string.
    /// </summary>
    public static System.String BracketOpen
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => s_bracketOpen;
    }

    /// <summary>
    /// Gets the pre-interned closing bracket string.
    /// </summary>
    public static System.String BracketClose
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => s_bracketClose;
    }

    /// <summary>
    /// Gets the pre-interned space string.
    /// </summary>
    public static System.String Space
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => s_space;
    }

    /// <summary>
    /// Gets the pre-interned dash with spaces string.
    /// </summary>
    public static System.String DashWithSpaces
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => s_dashWithSpaces;
    }

    /// <summary>
    /// Gets the pre-interned colon string.
    /// </summary>
    public static System.String Colon
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => s_colon;
    }

    /// <summary>
    /// Gets the pre-interned newline string.
    /// </summary>
    public static System.String NewLine
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => s_newLine;
    }

    #endregion Properties
}
