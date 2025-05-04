using BenchmarkDotNet.Attributes;
using Nalix.Common.Package.Enums;
using Nalix.Network.Package;
using Nalix.Network.Package.Extensions;

namespace Nalix.Benchmark.Package;

[MemoryDiagnoser]
public class PacketSerializationBenchmark
{
    private readonly Packet _packet;
    private readonly byte[] _buffer;

    public PacketSerializationBenchmark()
    {
        // Create a sample Packet for benchmarking
        var payload = new byte[60 * 1024]; // 64KB payload
        _packet = new Packet(1, PacketCode.Success, payload);

        // Prepare a buffer large enough for serialization
        _buffer = new byte[_packet.Length];
    }

    [Benchmark]
    public void SerializeToNewArray()
    {
        _ = _packet.Serialize();
    }

    [Benchmark]
    public void SerializeToExistingBuffer()
    {
        _packet.Serialize(_buffer.AsSpan());
    }

    [Benchmark]
    public void DeserializeFromBuffer()
    {
        _ = _buffer.ToArray().Deserialize();
    }
}
