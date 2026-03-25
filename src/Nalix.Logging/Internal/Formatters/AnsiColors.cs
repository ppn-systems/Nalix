// Copyright (c) 2025 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System.Runtime.CompilerServices;
using Nalix.Common.Diagnostics;

#if DEBUG
[assembly: InternalsVisibleTo("Nalix.Logging.Tests")]
[assembly: InternalsVisibleTo("Nalix.Logging.Benchmarks")]
#endif

namespace Nalix.Logging.Internal.Formatters;

/// <summary>
/// Provides ANSI color codes for console output styling in the logging system.
/// </summary>
internal static class AnsiColors
{
    #region Constants

    /// <summary>
    /// Basic colors
    /// </summary>
    public const string Reset = "\u001b[0m";       // Reset all styling

    /// <summary>
    /// Black text
    /// </summary>
    public const string Black = "\u001b[30m";
    /// <summary>
    /// Red text
    /// </summary>
    public const string Red = "\u001b[31m";
    /// <summary>
    /// Green text
    /// </summary>
    public const string Green = "\u001b[32m";
    /// <summary>
    /// Yellow text
    /// </summary>
    public const string Yellow = "\u001b[33m";
    /// <summary>
    /// Blue text
    /// </summary>
    public const string Blue = "\u001b[34m";
    /// <summary>
    /// Magenta text
    /// </summary>
    public const string Magenta = "\u001b[35m";
    /// <summary>
    /// Cyan text
    /// </summary>
    public const string Cyan = "\u001b[36m";
    /// <summary>
    /// White text
    /// </summary>
    public const string White = "\u001b[37m";

    /// <summary>
    /// Extended colors
    /// </summary>
    public const string LightGray = "\u001b[38;5;246m";   // Light gray text

    /// <summary>
    /// Dark gray text
    /// </summary>
    public const string DarkGray = "\u001b[38;5;240m";
    /// <summary>
    /// Orange text
    /// </summary>
    public const string Orange = "\u001b[38;5;208m";
    /// <summary>
    /// Pink text
    /// </summary>
    public const string Pink = "\u001b[38;5;205m";
    /// <summary>
    /// Light blue text
    /// </summary>
    public const string LightBlue = "\u001b[38;5;45m";
    /// <summary>
    /// Light green text
    /// </summary>
    public const string LightGreen = "\u001b[38;5;120m";
    /// <summary>
    /// Light yellow text
    /// </summary>
    public const string LightYellow = "\u001b[38;5;228m";
    /// <summary>
    /// Light cyan text
    /// </summary>
    public const string LightCyan = "\u001b[38;5;51m";
    /// <summary>
    /// Light magenta text
    /// </summary>
    public const string LightMagenta = "\u001b[38;5;213m";

    #endregion Constants

    #region Fields

    /// <summary>
    /// Cache of color codes by log level to avoid repeated switch statements
    /// </summary>
    private static readonly string[] _levelColorCache = new string[(int)LogLevel.Critical + 1];

    #endregion Fields

    #region Constructor

    /// <summary>
    /// Static constructor to initialize the color cache
    /// </summary>
    static AnsiColors()
    {
        // Initialize color cache
        _levelColorCache[(int)LogLevel.None] = Cyan;
        _levelColorCache[(int)LogLevel.Trace] = Orange;
        _levelColorCache[(int)LogLevel.Debug] = LightCyan;
        _levelColorCache[(int)LogLevel.Info] = LightGreen;
        _levelColorCache[(int)LogLevel.Warn] = LightYellow;
        _levelColorCache[(int)LogLevel.Error] = LightMagenta;
        _levelColorCache[(int)LogLevel.Critical] = Red;
    }

    #endregion Constructor

    #region APIs

    /// <summary>
    /// Gets the ANSI color code corresponding to the specified logging level.
    /// </summary>
    /// <param name="level">The logging level to get a color for.</param>
    /// <returns>An ANSI color code string.</returns>
    public static string GetForLevel(LogLevel level)
    {
        // Use the cached color if level is within range
        if ((int)level >= 0 && (int)level < _levelColorCache.Length)
        {
            return _levelColorCache[(int)level];
        }

        return White; // Default color for unknown levels
    }

    #endregion APIs
}
