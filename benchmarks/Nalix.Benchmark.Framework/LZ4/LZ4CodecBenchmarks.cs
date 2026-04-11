using System;
using BenchmarkDotNet.Attributes;
using Nalix.Benchmark.Framework.Abstractions;
using Nalix.Framework.LZ4;
using Nalix.Framework.LZ4.Encoders;
using Nalix.Framework.Memory.Buffers;

namespace Nalix.Benchmark.Framework.LZ4;

/// <summary>
/// Benchmarks for LZ4 encoding and decoding performance.
/// </summary>
public class LZ4CodecBenchmarks : NalixBenchmarkBase
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
        _input = new byte[PayloadBytes];
        // Fill with some repeating pattern for compression testing
        for (int i = 0; i < _input.Length; i++) _input[i] = (byte)(i % 23);

        _compressed = new byte[LZ4BlockEncoder.GetMaxLength(_input.Length)];
        _decoded = new byte[_input.Length];
        _compressedLength = LZ4Codec.Encode(_input, _compressed);
    }

    [BenchmarkCategory("Span"), Benchmark(Baseline = true)]
    public int EncodeToSpan() => LZ4Codec.Encode(_input, _compressed);

    [BenchmarkCategory("Span"), Benchmark]
    public int DecodeToSpan() => LZ4Codec.Decode(_compressed.AsSpan(0, _compressedLength), _decoded);

    [BenchmarkCategory("Lease"), Benchmark]
    public int EncodeToLease()
    {
        LZ4Codec.Encode(_input, out BufferLease lease, out int written);
        using (lease) return written;
    }

    [BenchmarkCategory("Lease"), Benchmark]
    public int DecodeToLease()
    {
        LZ4Codec.Decode(_compressed.AsSpan(0, _compressedLength), out BufferLease? lease, out int written);
        using (lease) return written;
    }
}
