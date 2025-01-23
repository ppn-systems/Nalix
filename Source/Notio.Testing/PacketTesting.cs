using Notio.Cryptography;
using Notio.Packets;
using Notio.Packets.Extensions;
using Notio.Packets.Extensions.Flags;
using System;

namespace Notio.Testing;

public class PacketTesting
{
    public static void Main()
    {
        TestPacketInitialization();
        TestPacketEquality();
        TestPacketWithPayload();
        TestPacketDispose();
        TestPacketCompress();
        // TestPacketCrypto();
        TestPacketArrary();
        TestPacketSign();
    }

    static void TestPacketInitialization()
    {
        byte type = 1;
        byte flags = 2;
        short command = 100;
        ReadOnlyMemory<byte> payload = new byte[] { 1, 2, 3, 4, 5, 6, 7, 8 };

        var packet = new Packet(type, flags, command, payload);

        if (packet.Type != type || packet.Flags != flags || packet.Command != command || !packet.Payload.Span.SequenceEqual(payload.Span))
        {
            Console.WriteLine("TestPacketInitialization: Thất bại");
        }
        else
        {
            Console.WriteLine("TestPacketInitialization: Thành công");
        }
    }

    static void TestPacketEquality()
    {
        var payload = new byte[] { 10, 20, 30, 40 };
        var packet1 = new Packet(1, 2, 100, payload);
        var packet2 = new Packet(1, 2, 100, payload);
        var packet3 = new Packet(1, 3, 101, new byte[] { 50, 60 });

        if (!packet1.Equals(packet2) || packet1 == packet3 || packet2 != packet1)
        {
            Console.WriteLine("TestPacketEquality: Thất bại");
        }
        else
        {
            Console.WriteLine("TestPacketEquality: Thành công");
        }
    }

    static void TestPacketWithPayload()
    {
        var payload = new byte[] { 5, 6, 7 };
        var newPayload = new byte[] { 10, 11, 12 };
        var packet = new Packet(1, 2, 100, payload);
        var updatedPacket = packet.WithPayload(newPayload);

        if (!updatedPacket.Payload.Span.SequenceEqual(newPayload))
        {
            Console.WriteLine("TestPacketWithPayload: Thất bại");
        }
        else
        {
            Console.WriteLine("TestPacketWithPayload: Thành công");
        }
    }

    static void TestPacketDispose()
    {
        var payload = new byte[200]; // Payload vượt quá MinPacketSize để kích hoạt _isPooled
        var packet = new Packet(1, 2, 100, payload);

        // Trước khi dispose
        if (packet.Payload.Length != 200)
        {
            Console.WriteLine("TestPacketDispose: Thất bại (trước khi dispose)");
            return;
        }

        packet.Dispose();

        // Sau khi dispose, kiểm tra xem bộ nhớ có được trả lại không
        Console.WriteLine("TestPacketDispose: Thành công (không có lỗi khi dispose)");
    }

    static void TestPacketCompress()
    {
        // Dữ liệu ban đầu
        byte[] payload = [10, 20, 30, 40];
        Packet packet1 = new(1, 2, 100, payload);

        // Nén payload
        Packet compressedPacket = packet1.CompressPayload();

        // Giải nén payload
        Packet decompressedPacket = compressedPacket.DecompressPayload();

        // So sánh kết quả sau khi nén và giải nén với packet ban đầu
        if (decompressedPacket.Payload.Span.SequenceEqual(packet1.Payload.Span))
        {
            Console.WriteLine("TestPacketCompress: Thành công");
        }
        else
        {
            Console.WriteLine("TestPacketCompress: Thất bại");
        }
    }

    static void TestPacketCrypto()
    {
        byte[] payload = [10, 20, 30, 40 , 50, 60, 70, 80, 90, 100, 110, 120, 130, 140, 150, 160, 170, 180];
        Packet packet1 = new(1, 2, 100, payload);

        byte[] key = Aes256.GenerateKey();

        // Mã hóa
        Packet encryptedPacket = packet1.EncryptPayload(key);

        // Giải mã
        Packet decryptedPacket = encryptedPacket.DecryptPayload(key);

        // So sánh
        if (decryptedPacket.Payload.Span.SequenceEqual(packet1.Payload.Span))
        {
            Console.WriteLine("TestPacketCrypto: Thành công");
        }
        else
        {
            Console.WriteLine("TestPacketCrypto: Thất bại");
        }
    }

    static void TestPacketArrary()
    {
        byte[] payload = { 10, 20, 30, 40, 50, 60, 70, 80, 90, 100, 110, 120, 130, 140, 150, 160, 170, 180 };
        Packet packet1 = new Packet(1, 2, 100, payload);

        ReadOnlySpan<byte> p = packet1.ToByteArray();

        Packet deserializedPacket = p.FromByteArray();

        if (packet1.Payload.Span.SequenceEqual(deserializedPacket.Payload.Span))
        {
            Console.WriteLine("\nTestPacketArrary: Thành công");
        }
        else
        {
            Console.WriteLine("\nTestPacketArrary: Thất bại");
        }
    }

    static void TestPacketSign()
    {
        Packet packet1 = new(0x00, 0x00, 0x00, new byte[] { 0x00, 0x00, 0x00, 0x00, 0x00 });

        Console.WriteLine("Dữ liệu gốc:");
        Console.WriteLine($"Length: {packet1.Length}");
        Console.WriteLine($"Payload: {string.Join(", ", packet1.Payload.ToArray())}");

        Console.WriteLine(packet1.Flags.HasFlag(Packets.Enums.PacketFlags.IsSigned));

        // Ký gói dữ liệu
        Packet signedPacket = packet1.SignPacket();

        Console.WriteLine("\nDữ liệu sau khi ký:");
        Console.WriteLine($"Length: {signedPacket.Length}");
        Console.WriteLine($"Payload: {string.Join(", ", signedPacket.Payload.ToArray())}");
        Console.WriteLine(signedPacket.Flags.HasFlag(Packets.Enums.PacketFlags.IsSigned));

        // Xác minh chữ ký của gói dữ liệu
        bool isVerified = signedPacket.VerifyPacket();

        if (isVerified)
        {
            Console.WriteLine("Chữ ký hợp lệ.");
        }
        else
        {
            Console.WriteLine("Chữ ký không hợp lệ.");
        }

        // Loại bỏ chữ ký
        Packet stripPacket = signedPacket.StripSignature();
        Console.WriteLine("\nDữ liệu sau khi xóa ký:");
        Console.WriteLine($"Length: {stripPacket.Length}");
        Console.WriteLine($"Payload: {string.Join(", ", stripPacket.Payload.ToArray())}");
        Console.WriteLine(stripPacket.Flags.HasFlag(Packets.Enums.PacketFlags.IsSigned));
    }
}