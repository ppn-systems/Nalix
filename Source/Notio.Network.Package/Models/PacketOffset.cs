namespace Notio.Network.Package.Models;

/// <summary>
/// Offset các thành phần trong tiêu đề gói tin.
/// </summary>
public static class PacketOffset
{
    public const int Length = 0;

    public const int Type = Length + PacketSize.Length;

    public const int Flags = Type + PacketSize.Type;

    public const int Priority = Flags + PacketSize.Flags;

    public const int Command = Priority + PacketSize.Priority;

    public const int Payload = Command + PacketSize.Command;
}