using Notio.Network.Package;
using Notio.Network.Package.Enums;
using Notio.Network.Package.Extensions;
using System;
using Xunit;

namespace Notio.Testing.Package
{
    public class PacketTests
    {
        private static readonly byte[] payload = { 10, 20, 30, 40 };

        [Fact]
        public void TestPacketInitialization()
        {
            byte type = 1;
            byte flags = 2;
            ushort command = 100;

            var packet = new Packet(type, flags, 0x00, command, payload);

            // Assertions
            Assert.Equal(type, packet.Type);
            Assert.Equal(flags, packet.Flags);
            Assert.Equal(command, packet.Command);
            Assert.True(packet.Payload.Span.SequenceEqual(payload));
        }

        [Fact]
        public void TestPacketEquality()
        {
            var packet1 = new Packet(1, 0x00, 2, 100, payload);
            var packet2 = new Packet(1, 0x00, 2, 100, payload);

            // Assert equality
            Assert.Equal(packet1, packet2);
        }

        [Fact]
        public void TestPacketDispose()
        {
            try
            {
                var payload = new byte[300];
                var packet = new Packet(1, 0x00, 2, 100, payload);
                packet.Dispose();

                // If Dispose doesn't throw an exception, it's passed
                Assert.True(true, "TestPacketDispose: Passed");
            }
            catch (Exception ex)
            {
                // If Dispose throws an exception, fail the test
                Assert.Fail($"TestPacketDispose: Failed - {ex.Message}");
            }
        }

        [Fact]
        public void TestPacketCompress()
        {
            var packet = new Packet(PacketType.Csv, PacketFlags.None, PacketPriority.None, 100, payload);

            var compressedPacket = (Packet)packet.CompressPayload();
            var decompressedPacket = (Packet)compressedPacket.DecompressPayload();

            // Verify decompressed payload is the same as the original
            Assert.True(decompressedPacket.Payload.Span.SequenceEqual(packet.Payload.Span));
        }

        [Fact]
        public void TestPacketArray()
        {
            var packet = new Packet(1, 0x00, 2, 100, payload);

            // Serialize and deserialize the packet
            ReadOnlySpan<byte> serialized = packet.Serialize();
            var deserializedPacket = (Packet)serialized.Deserialize();

            // Verify the payloads match
            Assert.True(packet.Payload.Span.SequenceEqual(deserializedPacket.Payload.Span));
        }
    }
}
