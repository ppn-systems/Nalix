using BenchmarkDotNet.Attributes;
using Nalix.Common.Package.Enums;
using Nalix.Network.Package;
using Nalix.Network.Package.Extensions;

namespace Nalix.Benchmark.Package;

[MemoryDiagnoser]
public class PacketCompressionBenchmark
{
    private readonly Packet _originalPacket;

    public PacketCompressionBenchmark()
    {
        // Create a sample Packet with a significant payload size (e.g., 64KB)
        var payload = new byte[60 * 1024]; // 64KB payload
        new Random().NextBytes(payload); // Fill with random data
        _originalPacket = new Packet(1, PacketCode.Success, payload);
    }

    [Benchmark]
    public Packet CompressPayload() => _originalPacket.CompressPayload();

    [Benchmark]
    public Packet DecompressPayload()
    {
        var compressedPacket = _originalPacket.CompressPayload();
        return compressedPacket.DecompressPayload();
    }
}
