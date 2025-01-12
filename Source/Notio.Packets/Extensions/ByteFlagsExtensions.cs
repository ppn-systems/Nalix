using Notio.Packets.Enums;

namespace Notio.Packets.Extensions;

/// <summary>
/// Cung cấp các phương thức hỗ trợ cho byte flags.
/// </summary>
public static class ByteFlagsExtensions
{
    /// <summary>
    /// Xác định liệu các flags dạng byte được chỉ định có chứa flag được chỉ định hay không.
    /// </summary>
    public static bool HasFlag(this byte flags, PacketFlags flag)
        => ((PacketFlags)flags).HasFlag(flag);

    /// <summary>
    /// Thêm flag được chỉ định vào các flags dạng byte.
    /// </summary>
    public static byte AddFlag(this byte flags, PacketFlags flag)
        => (byte)(flags | (byte)flag);

    /// <summary>
    /// Loại bỏ flag được chỉ định khỏi các flags dạng byte.
    /// </summary>
    public static byte RemoveFlag(this byte flags, PacketFlags flag)
        => (byte)(flags & ~(byte)flag);

    /// <summary>
    /// Xác định liệu các flags dạng byte có giá trị None hay không.
    /// </summary>
    public static bool IsNone(this byte flags)
        => flags == (byte)PacketFlags.None;

    /// <summary>
    /// Chuyển đổi các flags dạng byte thành chuỗi có thể đọc được.
    /// </summary>
    public static string ToReadableString(this byte flags)
        => ((PacketFlags)flags).ToReadableString();

    /// <summary>
    /// Xác định liệu các flags dạng byte có khớp với các flags yêu cầu và các flags bị loại trừ hay không.
    /// </summary>
    public static bool Matches(this byte flags, PacketFlags requiredFlags, PacketFlags excludedFlags = PacketFlags.None)
        => flags.HasFlag(requiredFlags) && !flags.HasFlag(excludedFlags);
}