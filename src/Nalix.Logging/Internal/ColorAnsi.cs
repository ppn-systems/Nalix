// Copyright (c) 2025 PPN Corporation. All rights reserved.

using Nalix.Common.Logging;

namespace Nalix.Logging.Internal;

/// <summary>
/// Provides ANSI color codes for console output styling in the logging system.
/// </summary>
internal static class ColorAnsi
{
    // Basic colors
    public const System.String Reset = "\u001b[0m";       // Reset all styling

    public const System.String Black = "\u001b[30m";      // Black text
    public const System.String Red = "\u001b[31m";        // Red text
    public const System.String Green = "\u001b[32m";      // Green text
    public const System.String Yellow = "\u001b[33m";     // Yellow text
    public const System.String Blue = "\u001b[34m";       // Blue text
    public const System.String Magenta = "\u001b[35m";    // Magenta text
    public const System.String Cyan = "\u001b[36m";       // Cyan text
    public const System.String White = "\u001b[37m";      // White text

    // Extended colors
    public const System.String LightGray = "\u001b[38;5;246m";   // Light gray text

    public const System.String DarkGray = "\u001b[38;5;240m";    // Dark gray text
    public const System.String Orange = "\u001b[38;5;208m";      // Orange text
    public const System.String Pink = "\u001b[38;5;205m";        // Pink text
    public const System.String LightBlue = "\u001b[38;5;45m";    // Light blue text
    public const System.String LightGreen = "\u001b[38;5;120m";  // Light green text
    public const System.String LightYellow = "\u001b[38;5;228m"; // Light yellow text
    public const System.String LightCyan = "\u001b[38;5;51m";    // Light cyan text
    public const System.String LightMagenta = "\u001b[38;5;213m"; // Light magenta text

    // Cache of color codes by log level to avoid repeated switch statements
    private static readonly System.String[] _levelColorCache = new System.String[(System.Int32)LogLevel.None + 1];

    /// <summary>
    /// Static constructor to initialize the color cache
    /// </summary>
    static ColorAnsi()
    {
        // Initialize color cache
        _levelColorCache[(System.Int32)LogLevel.None] = Cyan;
        _levelColorCache[(System.Int32)LogLevel.Meta] = Pink;
        _levelColorCache[(System.Int32)LogLevel.Trace] = Orange;
        _levelColorCache[(System.Int32)LogLevel.Debug] = LightCyan;
        _levelColorCache[(System.Int32)LogLevel.Information] = LightGreen;
        _levelColorCache[(System.Int32)LogLevel.Warning] = LightYellow;
        _levelColorCache[(System.Int32)LogLevel.Error] = LightMagenta;
        _levelColorCache[(System.Int32)LogLevel.Critical] = Red;
    }

    /// <summary>
    /// Gets the ANSI color code corresponding to the specified logging level.
    /// </summary>
    /// <param name="level">The logging level to get a color for.</param>
    /// <returns>An ANSI color code string.</returns>
    internal static System.String GetColorCode(LogLevel level)
    {
        // Use the cached color if level is within range
        if ((System.Int32)level >= 0 && (System.Int32)level < _levelColorCache.Length)
        {
            return _levelColorCache[(System.Int32)level];
        }

        return White; // Default color for unknown levels
    }
}
