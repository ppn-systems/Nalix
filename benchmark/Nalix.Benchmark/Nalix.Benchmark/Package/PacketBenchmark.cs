using BenchmarkDotNet.Attributes;
using Nalix.Common.Package.Enums;
using Nalix.Network.Package;

namespace Nalix.Benchmark.Package;

[MemoryDiagnoser]
public class PacketBenchmark
{
    private readonly byte[] _smallPayload = new byte[128];
    private readonly byte[] _largePayload = new byte[60 * 1024]; // 1MB
    private readonly string _stringPayload = new('a', 1024);

    [Benchmark]
    public Packet CreatePacketWithSmallPayload()
    {
        return new Packet(1, PacketCode.Success, _smallPayload);
    }

    [Benchmark]
    public Packet CreatePacketWithLargePayload()
    {
        return new Packet(1, PacketCode.Success, _largePayload);
    }

    [Benchmark]
    public void DisposeLargePayload()
    {
        var packet = new Packet(1, PacketCode.Success, _largePayload);
        packet.Dispose();
    }
}
