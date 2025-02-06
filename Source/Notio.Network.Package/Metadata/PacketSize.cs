namespace Notio.Network.Package.Metadata;

/// <summary>
/// Kích thước các thành phần trong tiêu đề gói tin.
/// </summary>
public static class PacketSize
{
    public const int Length = sizeof(ushort);

    public const int Id = sizeof(ushort);

    public const int Type = sizeof(byte);

    public const int Flags = sizeof(byte);

    public const int Priority = sizeof(byte);

    public const int Command = sizeof(ushort);

    public const int Timestamp = sizeof(ulong);

    public const int Checksum = sizeof(uint);

    public const int Header = Length + Id + Type + Flags + Priority + Command + Timestamp + Checksum;
}