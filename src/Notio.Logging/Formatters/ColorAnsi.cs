using Notio.Common.Enums;

namespace Notio.Logging.Formatters;

internal class ColorAnsi
{
    // Reset màu sắc về mặc định
    public const string Reset = "\x1b[0m";

    // Màu chữ (Foreground Colors)
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

    // Màu nền (Background Colors)
    public const string BlackBackground = "\x1b[40m";       // Nền đen

    public const string RedBackground = "\x1b[41m";         // Nền đỏ
    public const string GreenBackground = "\x1b[42m";       // Nền xanh lá
    public const string YellowBackground = "\x1b[43m";      // Nền vàng
    public const string BlueBackground = "\x1b[44m";        // Nền xanh dương
    public const string MagentaBackground = "\x1b[45m";     // Nền tím
    public const string CyanBackground = "\x1b[46m";        // Nền xanh lơ
    public const string WhiteBackground = "\x1b[47m";       // Nền trắng
    public const string LightGrayBackground = "\x1b[48;5;246m"; // Nền xám nhẹ
    public const string DarkGrayBackground = "\x1b[48;5;240m";  // Nền xám đậm
    public const string OrangeBackground = "\x1b[48;5;208m";    // Nền cam
    public const string PinkBackground = "\x1b[48;5;205m";      // Nền hồng
    public const string LightBlueBackground = "\x1b[48;5;45m";  // Nền xanh dương nhạt
    public const string LightGreenBackground = "\x1b[48;5;120m"; // Nền xanh lá nhạt
    public const string LightYellowBackground = "\x1b[48;5;228m"; // Nền vàng nhạt
    public const string LightCyanBackground = "\x1b[48;5;51m";  // Nền xanh lơ nhạt
    public const string LightMagentaBackground = "\x1b[48;5;213m"; // Nền tím nhạt

    internal static string GetColorCode(LoggingLevel level)
    {
        return level switch
        {
            LoggingLevel.Trace => ColorAnsi.Orange,   // Nhẹ nhàng cho thông báo kiểm tra
            LoggingLevel.Information => ColorAnsi.LightGreen,  // Thông tin
            LoggingLevel.Debug => ColorAnsi.LightCyan,   // Debug tốt cho việc kiểm tra thêm
            LoggingLevel.Warning => ColorAnsi.LightYellow,  // Cảnh báo với màu dễ nhận thấy
            LoggingLevel.Error => ColorAnsi.LightMagenta,  // Lỗi nhẹ nhàng nhưng nổi bật
            LoggingLevel.Critical => ColorAnsi.Red,      // Lỗi nghiêm trọng (đỏ)
            LoggingLevel.None => ColorAnsi.Cyan,         // Màu dễ nhìn cho trường hợp không có mức độ log
            _ => ColorAnsi.White, // Mặc định
        };
    }
}