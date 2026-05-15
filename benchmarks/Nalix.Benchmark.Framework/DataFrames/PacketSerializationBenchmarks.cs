using BenchmarkDotNet.Attributes;
using Nalix.Abstractions.Serialization;
using Nalix.Codec.DataFrames;
using Nalix.Abstractions.Networking.Packets;
using Nalix.Abstractions.Primitives;
using Nalix.Benchmark.Framework.Abstractions;
using Nalix.Codec.ProtocolFrames;
using Nalix.Environment.Random;
using System;
using System.Collections.Generic;
using System.Globalization;

namespace Nalix.Benchmark.Framework.DataFrames;

/// <summary>
/// Benchmarks for high-performance packet serialization and deserialization using PacketBase.
/// </summary>
public partial class PacketSerializationBenchmarks : NalixBenchmarkBase
{
    private Handshake _handshake = null!;
    private byte[] _serializedHandshake = null!;
    private byte[] _buffer = null!;
    private StringPacket _stringPacket = null!;
    private ListPacket _listPacket = null!;
    private StringDictionaryPacket _stringDictionaryPacket = null!;

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

        _stringPacket = new StringPacket
        {
            Message = "xin chao Viet Nam - hello world - UTF8 payload"
        };

        _listPacket = new ListPacket
        {
            Values = []
        };

        for (int i = 0; i < 256; i++)
        {
            _listPacket.Values.Add(i);
        }

        _stringDictionaryPacket = new StringDictionaryPacket
        {
            Values = new Dictionary<string, string>(StringComparer.Ordinal)
        };

        for (int i = 0; i < 80; i++)
        {
            _stringDictionaryPacket.Values["Field" + i.ToString("D2", CultureInfo.InvariantCulture)] =
                new string((char)('a' + (i % 26)), 512);
        }
    }

    [BenchmarkCategory("Serialization"), Benchmark(Baseline = true, Description = "Serialize (New Array)")]
    public byte[] SerializeNew() => _handshake.Serialize();

    [BenchmarkCategory("Serialization"), Benchmark(Description = "Serialize (Existing Span)")]
    public int SerializeSpan() => _handshake.Serialize(_buffer);

    [BenchmarkCategory("Deserialization"), Benchmark(Baseline = true, Description = "Deserialize (New Instance)")]
    public Handshake DeserializeNew() => Handshake.Deserialize(_serializedHandshake);

    [BenchmarkCategory("Memory"), Benchmark(Description = "ResetForPool")]
    public void ResetForPool() => _handshake.ResetForPool();

    [BenchmarkCategory("Length"), Benchmark(Description = "Length (Fixed Packet)")]
    public int LengthFixedPacket() => _handshake.Length;

    [BenchmarkCategory("Length"), Benchmark(Description = "Length (String Packet)")]
    public int LengthStringPacket() => _stringPacket.Length;

    [BenchmarkCategory("Length"), Benchmark(Description = "Length (List<int> Packet)")]
    public int LengthListPacket() => _listPacket.Length;

    [BenchmarkCategory("Length"), Benchmark(Description = "Length (Dictionary<string,string> Packet)")]
    public int LengthStringDictionaryPacket() => _stringDictionaryPacket.Length;

    [GenerateFormatter]
    [SerializePackable(SerializeLayout.Sequential)]
    internal sealed partial class StringPacket : PacketBase<StringPacket>
    {
        public string Message { get; set; } = string.Empty;

        public static new StringPacket Deserialize(ReadOnlySpan<byte> buffer) => PacketBase<StringPacket>.Deserialize(buffer);
    }

    [GenerateFormatter]
    [SerializePackable(SerializeLayout.Sequential)]
    internal sealed partial class ListPacket : PacketBase<ListPacket>
    {
        public List<int> Values { get; set; } = [];

        public static new ListPacket Deserialize(ReadOnlySpan<byte> buffer) => PacketBase<ListPacket>.Deserialize(buffer);
    }

    [GenerateFormatter]
    [SerializePackable(SerializeLayout.Sequential)]
    internal sealed partial class StringDictionaryPacket : PacketBase<StringDictionaryPacket>
    {
        public Dictionary<string, string> Values { get; set; } = new(StringComparer.Ordinal);

        public static new StringDictionaryPacket Deserialize(ReadOnlySpan<byte> buffer) => PacketBase<StringDictionaryPacket>.Deserialize(buffer);
    }
}
