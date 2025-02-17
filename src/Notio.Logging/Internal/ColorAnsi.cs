using Notio.Common.Enums;
using Notio.Common.Logging;

namespace Notio.Logging.Internal;

internal static class ColorAnsi
{
    public const string Reset = "\e[0m";
    public const string Black = "\e[30m";          // Màu đen
    public const string Red = "\e[31m";            // Màu đỏ
    public const string Green = "\e[32m";          // Màu xanh lá
    public const string Yellow = "\e[33m";         // Màu vàng
    public const string Blue = "\e[34m";           // Màu xanh dương
    public const string Magenta = "\e[35m";        // Màu tím
    public const string Cyan = "\e[36m";           // Màu xanh lơ
    public const string White = "\e[37m";          // Màu trắng
    public const string LightGray = "\e[38;5;246m"; // Màu xám nhẹ
    public const string DarkGray = "\e[38;5;240m";  // Màu xám đậm
    public const string Orange = "\e[38;5;208m";    // Màu cam
    public const string Pink = "\e[38;5;205m";      // Màu hồng
    public const string LightBlue = "\e[38;5;45m";  // Màu xanh dương nhạt
    public const string LightGreen = "\e[38;5;120m"; // Màu xanh lá nhạt
    public const string LightYellow = "\e[38;5;228m"; // Màu vàng nhạt
    public const string LightCyan = "\e[38;5;51m";  // Màu xanh lơ nhạt
    public const string LightMagenta = "\e[38;5;213m"; // Màu tím nhạt

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
