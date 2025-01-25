namespace Notio.Packets.Models;

/// <summary>
/// Kích thước các thành phần trong tiêu đề gói tin.
/// </summary>
public static class PacketSize
{
    public const int Length = sizeof(short);

    public const int Type = sizeof(byte);

    public const int Flags = sizeof(byte);

    public const int Priority = sizeof(byte);

    public const int Command = sizeof(short);

    public const int Header = Length + Type + Flags + Priority + Command;
}