using System.Text.Json;
using BenchmarkDotNet.Attributes;
using MessagePack;
using MemoryPack;
using Nalix.Benchmarks.Shared;
using Nalix.Benchmarks.Shared.Helpers;
using Nalix.Benchmarks.Shared.Payloads;
using Nalix.Codec.Serialization;

namespace Nalix.Codec.Benchmarks.Serialization;

[Config(typeof(NalixBenchmarkConfig))]
public class SerializerComparisonBenchmarks
{
    [Params(16, 128, 1024)]
    public int ItemCount;

    private BenchPayload _payload = null!;

    private byte[] _liteBytes = null!;
    private byte[] _msgPackBytes = null!;
    private byte[] _memPackBytes = null!;
    private byte[] _jsonBytes = null!;
    private byte[] _preallocatedBuffer = null!;

    [GlobalSetup]
    public void Setup()
    {
        _payload = PayloadGenerator.Generate(ItemCount);

        // Pre-serialize
        _liteBytes = LiteSerializer.Serialize(_payload);
        _msgPackBytes = MessagePackSerializer.Serialize(_payload);
        _memPackBytes = MemoryPackSerializer.Serialize(_payload);
        _jsonBytes = JsonSerializer.SerializeToUtf8Bytes(_payload);
        
        // Allocate a buffer large enough for ItemCount = 1024 payloads (around 128KB max)
        _preallocatedBuffer = new byte[256 * 1024]; 
    }

    // ── Serialize Benchmarks ──

    [Benchmark]
    public byte[] LiteSerializer_Serialize()
    {
        return LiteSerializer.Serialize(_payload);
    }

    [Benchmark]
    public int LiteSerializer_Serialize_Span()
    {
        return LiteSerializer.Serialize(_payload, _preallocatedBuffer.AsSpan());
    }

    [Benchmark]
    public byte[] MessagePack_Serialize()
    {
        return MessagePackSerializer.Serialize(_payload);
    }

    [Benchmark]
    public byte[] MemoryPack_Serialize()
    {
        return MemoryPackSerializer.Serialize(_payload);
    }

    [Benchmark]
    public int MemoryPack_Serialize_Span()
    {
        var writer = new ArrayBufferWriter(_preallocatedBuffer);
        MemoryPackSerializer.Serialize(ref writer, _payload);
        return writer.WrittenCount;
    }

    [Benchmark]
    public byte[] SystemTextJson_Serialize()
    {
        return JsonSerializer.SerializeToUtf8Bytes(_payload);
    }

    // ── Deserialize Benchmarks ──

    [Benchmark]
    public BenchPayload LiteSerializer_Deserialize()
    {
        BenchPayload? result = null;
        LiteSerializer.Deserialize(_liteBytes, ref result);
        return result!;
    }

    [Benchmark]
    public BenchPayload MessagePack_Deserialize()
    {
        return MessagePackSerializer.Deserialize<BenchPayload>(_msgPackBytes);
    }

    [Benchmark]
    public BenchPayload MemoryPack_Deserialize()
    {
        return MemoryPackSerializer.Deserialize<BenchPayload>(_memPackBytes)!;
    }

    [Benchmark]
    public BenchPayload SystemTextJson_Deserialize()
    {
        return JsonSerializer.Deserialize<BenchPayload>(_jsonBytes)!;
    }

    private struct ArrayBufferWriter : System.Buffers.IBufferWriter<byte>
    {
        private readonly byte[] _buffer;
        private int _written;

        public ArrayBufferWriter(byte[] buffer)
        {
            _buffer = buffer;
            _written = 0;
        }

        public int WrittenCount => _written;

        public void Advance(int count)
        {
            _written += count;
        }

        public Memory<byte> GetMemory(int sizeHint = 0)
        {
            return _buffer.AsMemory(_written);
        }

        public Span<byte> GetSpan(int sizeHint = 0)
        {
            return _buffer.AsSpan(_written);
        }
    }
}
