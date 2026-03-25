// Copyright (c) 2025 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;

namespace Nalix.Logging.Internal.Pooling;

/// <summary>
/// String interning cache for frequently used logging strings.
/// Reduces memory pressure by reusing immutable string instances.
/// </summary>
[DebuggerNonUserCode]
[ExcludeFromCodeCoverage]
internal static class InternCache
{
    /// <summary>
    /// Gets the pre-interned opening bracket string.
    /// </summary>
    public static string BracketOpen { get; } = "[";

    /// <summary>
    /// Gets the pre-interned closing bracket string.
    /// </summary>
    public static string BracketClose { get; } = "]";

    /// <summary>
    /// Gets the pre-interned space string.
    /// </summary>
    public static string Space { get; } = " ";

    /// <summary>
    /// Gets the pre-interned dash with spaces string.
    /// </summary>
    public static string DashWithSpaces { get; } = " - ";

    /// <summary>
    /// Gets the pre-interned colon string.
    /// </summary>
    public static string Colon { get; } = ":";

    /// <summary>
    /// Gets the pre-interned newline string.
    /// </summary>
    public static string NewLine { get; } = Environment.NewLine;
}
