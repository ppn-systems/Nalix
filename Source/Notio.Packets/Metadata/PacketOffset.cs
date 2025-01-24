namespace Notio.Packets.Metadata;

/// <summary>
/// Offset các thành phần trong tiêu đề gói tin.
/// </summary>
public static class PacketOffset
{
    /// <summary>
    /// Offset của thành phần độ dài.
    /// </summary>
    public const int Length = 0;

    /// <summary>
    /// Offset của thành phần loại.
    /// </summary>
    public const int Type = Length + PacketSize.Length;

    /// <summary>
    /// Offset của thành phần cờ hiệu.
    /// </summary>
    public const int Flags = Type + PacketSize.Type;

    /// <summary>
    /// Offset của thành phần ưu tiên.
    /// </summary>
    public const int Priority = Flags + PacketSize.Flags;

    /// <summary>
    /// Offset của thành phần lệnh.
    /// </summary>
    public const int Command = Priority + PacketSize.Priority;

    /// <summary>
    /// Offset của phần dữ liệu.
    /// </summary>
    public const int Payload = Command + PacketSize.Command;
}