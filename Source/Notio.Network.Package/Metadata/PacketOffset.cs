namespace Notio.Network.Package.Metadata;

/// <summary>
/// Offset các thành phần trong tiêu đề gói tin.
/// </summary>
public static class PacketOffset
{
    public const int Length = 0;
    public const int Id = Length + PacketSize.Length;
    public const int Type = Id + PacketSize.Id;
    public const int Flags = Type + PacketSize.Type;
    public const int Priority = Flags + PacketSize.Flags;
    public const int Command = Priority + PacketSize.Priority;
    public const int Timestamp = Command + PacketSize.Command;
    public const int Checksum = Timestamp + PacketSize.Timestamp;
    public const int Payload = Checksum + PacketSize.Checksum;
}