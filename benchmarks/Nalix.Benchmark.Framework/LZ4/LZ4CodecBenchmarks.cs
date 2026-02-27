using System;
using BenchmarkDotNet.Attributes;
using Nalix.Framework.LZ4;
using Nalix.Framework.LZ4.Encoders;
using Nalix.Framework.Memory.Buffers;

namespace Nalix.Benchmark.Framework.LZ4;

[Config(typeof(global::Nalix.Benchmark.Framework.BenchmarkConfig))]
public class LZ4CodecBenchmarks
{
    private byte[] _input = null!;
    private byte[] _compressed = null!;
    private byte[] _decoded = null!;
    private int _compressedLength;

    [Params(1024, 16384)]
    public int PayloadBytes { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        _input = new byte[this.PayloadBytes];

        for (int i = 0; i < _input.Length; i++)
        {
            _input[i] = (byte)(i % 23);
        }

        _compressed = new byte[LZ4BlockEncoder.GetMaxLength(_input.Length)];
        _decoded = new byte[_input.Length];
        _compressedLength = LZ4Codec.Encode(_input, _compressed);
    }

    [Benchmark]
    public int Encode_ToSpan()
        => LZ4Codec.Encode(_input, _compressed);

    [Benchmark]
    public int Decode_ToSpan()
        => LZ4Codec.Decode(MemoryExtensions.AsSpan(_compressed, 0, _compressedLength), _decoded);

    [Benchmark]
    public int Encode_ToLease()
    {
        LZ4Codec.Encode(_input, out BufferLease lease, out int written);

        using (lease)
        {
            return written;
        }
    }

    [Benchmark]
    public int Decode_ToLease()
    {
        LZ4Codec.Decode(MemoryExtensions.AsSpan(_compressed, 0, _compressedLength), out BufferLease? lease, out int written);

        using (lease)
        {
            return written;
        }
    }
}
