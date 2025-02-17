using Notio.Common.Enums;

namespace Notio.Logging.Internal;

internal class ColorAnsi
{
    public const string Reset = "\x1b[0m";
    public const string Black = "\x1b[30m";          // Màu đen
    public const string Red = "\x1b[31m";            // Màu đỏ
    public const string Green = "\x1b[32m";          // Màu xanh lá
    public const string Yellow = "\x1b[33m";         // Màu vàng
    public const string Blue = "\x1b[34m";           // Màu xanh dương
    public const string Magenta = "\x1b[35m";        // Màu tím
    public const string Cyan = "\x1b[36m";           // Màu xanh lơ
    public const string White = "\x1b[37m";          // Màu trắng
    public const string LightGray = "\x1b[38;5;246m"; // Màu xám nhẹ
    public const string DarkGray = "\x1b[38;5;240m";  // Màu xám đậm
    public const string Orange = "\x1b[38;5;208m";    // Màu cam
    public const string Pink = "\x1b[38;5;205m";      // Màu hồng
    public const string LightBlue = "\x1b[38;5;45m";  // Màu xanh dương nhạt
    public const string LightGreen = "\x1b[38;5;120m"; // Màu xanh lá nhạt
    public const string LightYellow = "\x1b[38;5;228m"; // Màu vàng nhạt
    public const string LightCyan = "\x1b[38;5;51m";  // Màu xanh lơ nhạt
    public const string LightMagenta = "\x1b[38;5;213m"; // Màu tím nhạt

    internal static string GetColorCode(LoggingLevel level)
    {
        return level switch
        {
            LoggingLevel.None => Cyan,
            LoggingLevel.Trace => Orange,
            LoggingLevel.Critical => Red,
            LoggingLevel.Debug => LightCyan,
            LoggingLevel.Error => LightMagenta,
            LoggingLevel.Warning => LightYellow,
            LoggingLevel.Information => LightGreen,

            _ => White,
        };
    }
}
