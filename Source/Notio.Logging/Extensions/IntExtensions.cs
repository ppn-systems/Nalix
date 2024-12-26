using System;

namespace Notio.Logging.Extensions;

/// <summary>
/// Lớp mở rộng cung cấp các phương thức hỗ trợ cho các kiểu số nguyên.
/// </summary>
internal static class IntExtensions
{
    /// <summary>
    /// Đây là một hàm tối ưu hóa tính toán, trả về số chữ số thập phân của giá trị int được chỉ định.
    /// </summary>
    /// <param name="value">Giá trị số nguyên cần tính toán.</param>
    /// <returns>Số chữ số thập phân của giá trị.</returns>
    internal static int GetFormattedLength(this int value)
    {
        if (value == 0)
            return 1; // Kết quả nhanh cho giá trị EventId điển hình (0)

        uint absVal = (uint)Math.Abs(value);
        return (int)Math.Log10(absVal) + 1 + (value < 0 ? 1 : 0);
    }

    /// <summary>
    /// Đây là một hàm tối ưu hóa tính toán, trả về số chữ số thập phân của giá trị DateTime được chỉ định.
    /// </summary>
    /// <param name="value">Giá trị DateTime cần tính toán.</param>
    /// <returns>Số chữ số thập phân của giá trị.</returns>
    internal static int GetFormattedLength(this DateTime value)
    {
        if (value == DateTime.MinValue)
            return 1; // Kết quả nhanh cho giá trị DateTime nhỏ nhất

        ulong absTicks = (ulong)Math.Abs((long)value.Ticks);
        return (int)Math.Log10(absTicks) + 1 + (value < DateTime.MinValue ? 1 : 0);
    }
}