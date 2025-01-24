using Notio.Packets;
using Notio.Packets.Utilities;
using System;

namespace Notio.Testing;

public class PacketTesting
{
    private static readonly byte[] payload = [10, 20, 30, 40];

    public static void Main()
    {
        var tests = new Action[]
        {
            TestPacketInitialization,
            TestPacketEquality,
            TestPacketWithPayload,
            TestPacketDispose,
            TestPacketCompress,
            TestPacketArray,
            TestPacketSign
        };

        foreach (var test in tests)
        {
            try
            {
                test.Invoke();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"{test.Method.Name}: Failed - {ex.Message}");
            }
        }
    }

    private static void TestPacketInitialization()
    {
        byte type = 1;
        byte flags = 2;
        short command = 100;

        var packet = new Packet(type, flags, 0x00, command, PacketTesting.payload);

        Console.WriteLine(packet.Type == type && packet.Flags == flags && packet.Command == command && packet.Payload.Span.SequenceEqual(payload)
            ? "TestPacketInitialization: Passed"
            : "TestPacketInitialization: Failed");
    }

    private static void TestPacketEquality()
    {
        var packet1 = new Packet(1, 0x00, 2, 100, PacketTesting.payload);
        var packet2 = new Packet(1, 0x00, 2, 100, PacketTesting.payload);

        Console.WriteLine(packet1 == packet2
            ? "TestPacketEquality: Passed"
            : "TestPacketEquality: Failed");
    }

    private static void TestPacketWithPayload()
    {
        var newPayload = new byte[] { 10, 11, 12 };
        var packet = new Packet(1, 0x00, 2, 100, PacketTesting.payload);
        var updatedPacket = packet.WithPayload(newPayload);

        Console.WriteLine(updatedPacket.Payload.Span.SequenceEqual(newPayload)
            ? "TestPacketWithPayload: Passed"
            : "TestPacketWithPayload: Failed");
    }

    private static void TestPacketDispose()
    {
        try
        {
            var payload = new byte[300];
            var packet = new Packet(1, 0x00, 2, 100, payload);
            packet.Dispose();

            Console.WriteLine("TestPacketDispose: Passed");
        }
        catch
        {
            Console.WriteLine("TestPacketDispose: Failed");
        }
    }

    private static void TestPacketCompress()
    {
        Packet packet = new(1, 0x00, 2, 100, PacketTesting.payload);

        Packet compressedPacket = packet.CompressPayload();
        Packet decompressedPacket = compressedPacket.DecompressPayload();

        Console.WriteLine(decompressedPacket.Payload.Span.SequenceEqual(packet.Payload.Span)
            ? "TestPacketCompress: Passed"
            : "TestPacketCompress: Failed");
    }

    private static void TestPacketArray()
    {
        Packet packet = new(1, 0x00, 2, 100, PacketTesting.payload);

        ReadOnlySpan<byte> serialized = packet.ToByteArray();
        Packet deserializedPacket = serialized.FromByteArray();

        Console.WriteLine(packet.Payload.Span.SequenceEqual(deserializedPacket.Payload.Span)
            ? "TestPacketArray: Passed"
            : "TestPacketArray: Failed");
    }

    private static void TestPacketSign()
    {
        var packet = new Packet(0x00, 0x00, 0x00, 0x00, PacketTesting.payload);

        var signedPacket = packet.SignPacket();
        var isVerified = signedPacket.VerifyPacket();

        Console.WriteLine(isVerified
            ? "TestPacketSign: Passed"
            : "TestPacketSign: Failed");
    }
}