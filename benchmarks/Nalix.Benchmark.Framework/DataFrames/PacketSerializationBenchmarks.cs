using BenchmarkDotNet.Attributes;
using Nalix.Benchmark.Framework.Abstractions;
using Nalix.Common.Networking.Packets;
using Nalix.Common.Primitives;
using Nalix.Framework.DataFrames.SignalFrames;
using Nalix.Framework.Random;

namespace Nalix.Benchmark.Framework.DataFrames;

/// <summary>
/// Benchmarks for high-performance packet serialization and deserialization using PacketBase.
/// </summary>
public class PacketSerializationBenchmarks : NalixBenchmarkBase
{
    private Handshake _handshake = null!;
    private byte[] _serializedHandshake = null!;
    private byte[] _buffer = null!;

    [GlobalSetup]
    public void Setup()
    {
        _handshake = new Handshake();
        _handshake.Initialize(
            HandshakeStage.CLIENT_HELLO,
            new Bytes32(Csprng.GetBytes(32)),
            new Bytes32(Csprng.GetBytes(32)),
            new Bytes32(Csprng.GetBytes(32)),
            PacketFlags.RELIABLE);

        _serializedHandshake = _handshake.Serialize();
        _buffer = new byte[Handshake.Size];
    }

    [BenchmarkCategory("Serialization"), Benchmark(Baseline = true, Description = "Serialize (New Array)")]
    public byte[] SerializeNew() => _handshake.Serialize();

    [BenchmarkCategory("Serialization"), Benchmark(Description = "Serialize (Existing Span)")]
    public int SerializeSpan() => _handshake.Serialize(_buffer);

    [BenchmarkCategory("Deserialization"), Benchmark(Baseline = true, Description = "Deserialize (New Instance)")]
    public Handshake DeserializeNew() => Handshake.Deserialize(_serializedHandshake);

    [BenchmarkCategory("Memory"), Benchmark(Description = "ResetForPool")]
    public void ResetForPool() => _handshake.ResetForPool();
}
