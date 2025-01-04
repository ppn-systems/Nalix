namespace Notio.Packets;

public struct Packet
{
    public byte Type;         // Loại gói tin
    public byte Flags;        // Cờ hiệu
    public short Command;     // Lệnh
    public byte[] Payload;    // Dữ liệu chính
}