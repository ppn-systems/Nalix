using Notio.Common.Enums;

namespace Notio.Logging.Internal;

/// <summary>
/// Provides ANSI color codes for console output styling in the logging system.
/// </summary>
internal static class ColorAnsi
{
    // Basic colors
    public const string Reset = "\u001b[0m";       // Reset all styling
    public const string Black = "\u001b[30m";      // Black text
    public const string Red = "\u001b[31m";        // Red text
    public const string Green = "\u001b[32m";      // Green text
    public const string Yellow = "\u001b[33m";     // Yellow text
    public const string Blue = "\u001b[34m";       // Blue text
    public const string Magenta = "\u001b[35m";    // Magenta text
    public const string Cyan = "\u001b[36m";       // Cyan text
    public const string White = "\u001b[37m";      // White text

    // Extended colors
    public const string LightGray = "\u001b[38;5;246m";   // Light gray text
    public const string DarkGray = "\u001b[38;5;240m";    // Dark gray text
    public const string Orange = "\u001b[38;5;208m";      // Orange text
    public const string Pink = "\u001b[38;5;205m";        // Pink text
    public const string LightBlue = "\u001b[38;5;45m";    // Light blue text
    public const string LightGreen = "\u001b[38;5;120m";  // Light green text
    public const string LightYellow = "\u001b[38;5;228m"; // Light yellow text
    public const string LightCyan = "\u001b[38;5;51m";    // Light cyan text
    public const string LightMagenta = "\u001b[38;5;213m"; // Light magenta text

    // Background colors
    public const string BgBlack = "\u001b[40m";     // Black background
    public const string BgRed = "\u001b[41m";       // Red background
    public const string BgGreen = "\u001b[42m";     // Green background
    public const string BgYellow = "\u001b[43m";    // Yellow background
    public const string BgBlue = "\u001b[44m";      // Blue background
    public const string BgMagenta = "\u001b[45m";   // Magenta background
    public const string BgCyan = "\u001b[46m";      // Cyan background
    public const string BgWhite = "\u001b[47m";     // White background

    // Text styles
    public const string Bold = "\u001b[1m";         // Bold text
    public const string Underline = "\u001b[4m";    // Underlined text
    public const string Italic = "\u001b[3m";       // Italic text (not widely supported)
    public const string Blink = "\u001b[5m";        // Blinking text (not widely supported)

    // Cache of color codes by log level to avoid repeated switch statements
    private static readonly string[] _levelColorCache = new string[(int)LoggingLevel.None + 1];

    /// <summary>
    /// Static constructor to initialize the color cache
    /// </summary>
    static ColorAnsi()
    {
        // Initialize color cache
        _levelColorCache[(int)LoggingLevel.None] = Cyan;
        _levelColorCache[(int)LoggingLevel.Trace] = Orange;
        _levelColorCache[(int)LoggingLevel.Debug] = LightCyan;
        _levelColorCache[(int)LoggingLevel.Information] = LightGreen;
        _levelColorCache[(int)LoggingLevel.Warning] = LightYellow;
        _levelColorCache[(int)LoggingLevel.Error] = LightMagenta;
        _levelColorCache[(int)LoggingLevel.Critical] = Red;
    }

    /// <summary>
    /// Gets the ANSI color code corresponding to the specified logging level.
    /// </summary>
    /// <param name="level">The logging level to get a color for.</param>
    /// <returns>An ANSI color code string.</returns>
    internal static string GetColorCode(LoggingLevel level)
    {
        // Use the cached color if level is within range
        if ((int)level >= 0 && (int)level < _levelColorCache.Length)
        {
            return _levelColorCache[(int)level];
        }

        return White; // Default color for unknown levels
    }

    /// <summary>
    /// Applies color to the given text based on logging level.
    /// </summary>
    /// <param name="text">The text to colorize.</param>
    /// <param name="level">The logging level determining the color.</param>
    /// <returns>Colorized text string with reset code appended.</returns>
    internal static string Colorize(string text, LoggingLevel level)
        => GetColorCode(level) + text + Reset;
}
