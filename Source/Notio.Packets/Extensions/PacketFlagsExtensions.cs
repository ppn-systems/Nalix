using Notio.Packets.Enums;

namespace Notio.Packets.Extensions;

/// <summary>
/// Cung cấp các phương thức hỗ trợ cho PacketFlags.
/// </summary>
public static class PacketFlagsExtensions
{
    /// <summary>
    /// Xác định liệu các flags được chỉ định có chứa flag được chỉ định hay không.
    /// </summary>
    public static bool HasFlag(this PacketFlags flags, PacketFlags flag) 
        => flags.HasFlag(flag);

    /// <summary>
    /// Thêm flag được chỉ định vào các flags.
    /// </summary>
    public static PacketFlags AddFlag(this PacketFlags flags, PacketFlags flag) 
        => flags | flag;

    /// <summary>
    /// Loại bỏ flag được chỉ định khỏi các flags.
    /// </summary>
    public static PacketFlags RemoveFlag(this PacketFlags flags, PacketFlags flag) 
        => flags & ~flag;

    /// <summary>
    /// Xác định liệu các flags có giá trị None hay không.
    /// </summary>
    public static bool IsNone(this PacketFlags flags) 
        => flags == PacketFlags.None;

    /// <summary>
    /// Chuyển đổi các flags thành chuỗi có thể đọc được.
    /// </summary>
    public static string ToReadableString(this PacketFlags flags) 
        => flags == PacketFlags.None ? "None" : flags.ToString();

    /// <summary>
    /// Xác định liệu các flags có khớp với các flags yêu cầu và các flags bị loại trừ hay không.
    /// </summary>
    public static bool Matches(this PacketFlags flags, PacketFlags requiredFlags, PacketFlags excludedFlags = PacketFlags.None)
        => flags.HasFlag(requiredFlags) && !flags.HasFlag(excludedFlags);
}
