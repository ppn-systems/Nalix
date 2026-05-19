using System;
using System.Runtime.InteropServices;
using BenchmarkDotNet.Attributes;
using Nalix.Abstractions.Networking.Packets;
using Nalix.Abstractions.Primitives;
using Nalix.Codec.DataFrames;
using Nalix.Benchmarks.Shared;

namespace Nalix.Runtime.Benchmarks.Packets;

[Config(typeof(NalixBenchmarkConfig))]
public class PacketRegistryBenchmarks
{
    private byte[] _rawBytes = null!;
    private uint _magic;

    [GlobalSetup]
    public void Setup()
    {
        _magic = PacketRegistry.Compute(typeof(MemoryPacket));

        // Register deserializer
        if (!PacketRegistry.IsBuilt)
        {
            PacketRegistry.RegisterGenerated(_magic, "MemoryPacket", raw =>
            {
                var header = MemoryMarshal.Read<PacketHeader>(raw[..10]);
                return new MemoryPacket(raw[10..].ToArray(), header);
            });
            PacketRegistry.Build();
        }

        var header = new PacketHeader
        {
            MagicNumber = _magic,
            OpCode = 42,
            Flags = PacketFlags.NONE,
            Priority = PacketPriority.NONE,
            SequenceId = 1
        };

        _rawBytes = new byte[32];
        MemoryMarshal.Write(_rawBytes.AsSpan(0, 10), ref header);
        _rawBytes[10] = 0xAA;
        _rawBytes[11] = 0xBB;
    }

    [Benchmark]
    public bool TryDeserialize()
    {
        return PacketRegistry.TryDeserialize(_rawBytes, out _);
    }
}
