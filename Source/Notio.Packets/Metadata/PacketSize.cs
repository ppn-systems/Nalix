namespace Notio.Packets.Metadata;

/// <summary>
/// Kích thước các thành phần trong tiêu đề gói tin.
/// </summary>
public static class PacketSize
{
    /// <summary>Kích thước thành phần độ dài.</summary>
    public const int Length = sizeof(short);

    /// <summary>Kích thước thành phần loại.</summary>
    public const int Type = sizeof(byte);

    /// <summary>Kích thước thành phần cờ hiệu.</summary>
    public const int Flags = sizeof(byte);

    /// <summary>Kích thước thành phần lệnh.</summary>
    public const int Command = sizeof(short);

    /// <summary>Tổng kích thước tiêu đề.</summary>
    public const int Header = Length + Type + Flags + Command;
}
