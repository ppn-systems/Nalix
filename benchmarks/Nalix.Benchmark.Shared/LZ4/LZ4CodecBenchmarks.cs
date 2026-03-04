// Copyright (c) 2026 PPN Corporation. All rights reserved.

// 13th Gen Intel Core i7-13620H 2.40GHz, 1 CPU, 16 logical and 10 physical cores
// .NET SDK 10.0.103
//   [Host]     : .NET 10.0.3 (10.0.3, 10.0.326.7603), X64 RyuJIT x86-64-v3 [AttachedDebugger]
//   DefaultJob : .NET 10.0.3 (10.0.3, 10.0.326.7603), X64 RyuJIT x86-64-v3


// | Method                                                                | PayloadSize | Compressible | Mean          | Error       | StdDev      | Gen0   | Gen1   | Allocated |
// |---------------------------------------------------------------------- |------------ |------------- |--------------:|------------:|------------:|-------:|-------:|----------:|
// | 'Encode(ReadOnlySpan<byte>, Span<byte>)'                              | 128         | False        |  3,478.851 ns |   6.8669 ns |   6.4233 ns |      - |      - |         - |
// | 'Encode(byte[] input, byte[] output)'                                 | 128         | False        |  3,452.666 ns |   4.6226 ns |   4.3240 ns |      - |      - |         - |
// | 'Encode(ReadOnlySpan<byte>) -> new byte[]'                            | 128         | False        |  3,495.045 ns |   6.7441 ns |   5.9785 ns | 0.0267 |      - |     344 B |
// | 'Decode(ReadOnlySpan<byte>, Span<byte>)'                              | 128         | False        |      6.568 ns |   0.1267 ns |   0.1123 ns |      - |      - |         - |
// | 'Decode(ReadOnlySpan<byte>, out byte[] output, out int bytesWritten)' | 128         | False        |     15.186 ns |   0.2478 ns |   0.2318 ns | 0.0121 |      - |     152 B |
// | 'Encode(ReadOnlySpan<byte>, Span<byte>)'                              | 128         | True         |  3,324.872 ns |   4.4628 ns |   3.9562 ns |      - |      - |         - |
// | 'Encode(byte[] input, byte[] output)'                                 | 128         | True         |  3,326.005 ns |   5.1759 ns |   4.8415 ns |      - |      - |         - |
// | 'Encode(ReadOnlySpan<byte>) -> new byte[]'                            | 128         | True         |  3,350.376 ns |   8.8484 ns |   8.2768 ns | 0.0191 |      - |     240 B |
// | 'Decode(ReadOnlySpan<byte>, Span<byte>)'                              | 128         | True         |     51.248 ns |   0.8811 ns |   0.8242 ns |      - |      - |         - |
// | 'Decode(ReadOnlySpan<byte>, out byte[] output, out int bytesWritten)' | 128         | True         |     64.012 ns |   1.2104 ns |   1.1322 ns | 0.0120 |      - |     152 B |
// | 'Encode(ReadOnlySpan<byte>, Span<byte>)'                              | 1024        | False        |  4,749.185 ns |  26.0546 ns |  24.3715 ns |      - |      - |         - |
// | 'Encode(byte[] input, byte[] output)'                                 | 1024        | False        |  4,750.077 ns |  30.7564 ns |  28.7696 ns |      - |      - |         - |
// | 'Encode(ReadOnlySpan<byte>) -> new byte[]'                            | 1024        | False        |  4,962.543 ns |  45.9117 ns |  42.9459 ns | 0.1678 |      - |    2144 B |
// | 'Decode(ReadOnlySpan<byte>, Span<byte>)'                              | 1024        | False        |     12.726 ns |   0.2122 ns |   0.1881 ns |      - |      - |         - |
// | 'Decode(ReadOnlySpan<byte>, out byte[] output, out int bytesWritten)' | 1024        | False        |     45.467 ns |   0.8175 ns |   0.8029 ns | 0.0835 |      - |    1048 B |
// | 'Encode(ReadOnlySpan<byte>, Span<byte>)'                              | 1024        | True         |  3,339.272 ns |   7.1472 ns |   5.5801 ns |      - |      - |         - |
// | 'Encode(byte[] input, byte[] output)'                                 | 1024        | True         |  3,348.668 ns |  30.4015 ns |  28.4375 ns |      - |      - |         - |
// | 'Encode(ReadOnlySpan<byte>) -> new byte[]'                            | 1024        | True         |  3,421.800 ns |   9.7746 ns |   9.1432 ns | 0.0877 |      - |    1144 B |
// | 'Decode(ReadOnlySpan<byte>, Span<byte>)'                              | 1024        | True         |    402.214 ns |   7.8688 ns |   8.7461 ns |      - |      - |         - |
// | 'Decode(ReadOnlySpan<byte>, out byte[] output, out int bytesWritten)' | 1024        | True         |    407.521 ns |   3.6937 ns |   3.4551 ns | 0.0834 |      - |    1048 B |
// | 'Encode(ReadOnlySpan<byte>, Span<byte>)'                              | 8192        | False        | 16,591.212 ns | 330.6362 ns | 339.5390 ns |      - |      - |         - |
// | 'Encode(byte[] input, byte[] output)'                                 | 8192        | False        | 16,241.461 ns | 320.1017 ns | 368.6295 ns |      - |      - |         - |
// | 'Encode(ReadOnlySpan<byte>) -> new byte[]'                            | 8192        | False        | 16,974.127 ns | 328.6837 ns | 438.7833 ns | 1.3123 | 0.0305 |   16536 B |
// | 'Decode(ReadOnlySpan<byte>, Span<byte>)'                              | 8192        | False        |     68.076 ns |   1.2931 ns |   1.3279 ns |      - |      - |         - |
// | 'Decode(ReadOnlySpan<byte>, out byte[] output, out int bytesWritten)' | 8192        | False        |    279.846 ns |   5.5311 ns |   5.4323 ns | 0.6542 |      - |    8216 B |
// | 'Encode(ReadOnlySpan<byte>, Span<byte>)'                              | 8192        | True         |  3,454.290 ns |   5.5454 ns |   4.6307 ns |      - |      - |         - |
// | 'Encode(byte[] input, byte[] output)'                                 | 8192        | True         |  3,478.143 ns |   4.8864 ns |   4.3316 ns |      - |      - |         - |
// | 'Encode(ReadOnlySpan<byte>) -> new byte[]'                            | 8192        | True         |  3,770.644 ns |  17.3478 ns |  15.3784 ns | 0.6638 |      - |    8368 B |
// | 'Decode(ReadOnlySpan<byte>, Span<byte>)'                              | 8192        | True         |  2,902.304 ns |  15.4183 ns |  12.0376 ns |      - |      - |         - |
// | 'Decode(ReadOnlySpan<byte>, out byte[] output, out int bytesWritten)' | 8192        | True         |  3,171.872 ns |  63.4617 ns |  52.9934 ns | 0.6523 |      - |    8216 B |

using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Jobs;
using Nalix.Shared.LZ4; // LZ4Codec, LZ4BlockEncoder
using Nalix.Shared.LZ4.Encoders;
using System;
using System.Security.Cryptography;

namespace Nalix.Benchmark.Shared.LZ4;

// MemoryDiagnoser to capture allocations; we vary payload size and compressibility.
[MemoryDiagnoser]
[SimpleJob(
    RuntimeMoniker.Net10_0,
    launchCount: 1,
    warmupCount: 3,
    iterationCount: 10
)]
public class LZ4CodecBenchmarks
{
    // Payload sizes to exercise small/medium/large inputs
    [Params(128, 1024, 8192)]
    public Int32 PayloadSize;

    // Whether input is compressible (repetitive) or incompressible (random)
    [Params(true, false)]
    public Boolean Compressible;

    private Byte[] _input = Array.Empty<Byte>();
    private Byte[] _compressOutputBuffer = Array.Empty<Byte>();
    private Byte[] _decompressOutputBuffer = Array.Empty<Byte>();

    // Precomputed compressed payload (slice) used for Decode benchmarks
    private Byte[] _compressedPayload = Array.Empty<Byte>();
    private Int32 _compressedLength;

    [GlobalSetup]
    public void GlobalSetup()
    {
        // Prepare input data according to compressibility
        _input = new Byte[PayloadSize];
        if (Compressible)
        {
            // Fill with repeated pattern which compresses well
            Byte[] pattern = new Byte[16];
            RandomNumberGenerator.Fill(pattern);
            for (Int32 i = 0; i < _input.Length; i++)
            {
                _input[i] = pattern[i % pattern.Length];
            }
        }
        else
        {
            // Random data — typically incompressible
            RandomNumberGenerator.Fill(_input);
        }

        // Prepare output buffers
        Int32 maxOut = LZ4BlockEncoder.GetMaxLength(_input.Length); // ensure enough capacity
        _compressOutputBuffer = new Byte[maxOut];
        _decompressOutputBuffer = new Byte[_input.Length];

        // Pre-compute compressed payload for decode benchmarks (use span-based Encode to avoid extra alloc)
        _compressedLength = LZ4Codec.Encode(_input.AsSpan(), _compressOutputBuffer.AsSpan());
        if (_compressedLength < 0)
        {
            throw new InvalidOperationException("Precompression failed in GlobalSetup.");
        }
        _compressedPayload = new Byte[_compressedLength];
        Array.Copy(_compressOutputBuffer, 0, _compressedPayload, 0, _compressedLength);
    }

    // -------------------------
    // ENCODE benchmarks
    // -------------------------

    // Span(in) -> Span(out)
    [Benchmark(Description = "Encode(ReadOnlySpan<byte>, Span<byte>)")]
    public Int32 Encode_SpanToSpan()
    {
        Int32 written = LZ4Codec.Encode(_input.AsSpan(), _compressOutputBuffer.AsSpan());
        return written < 0 ? throw new InvalidOperationException("Encode failed") : written;
    }

    // Byte[] -> Byte[] (caller-supplied arrays)
    [Benchmark(Description = "Encode(byte[] input, byte[] output)")]
    public Int32 Encode_ArrayToArray()
    {
        Int32 written = LZ4Codec.Encode(_input, _compressOutputBuffer);
        return written < 0 ? throw new InvalidOperationException("Encode failed") : written;
    }

    // One-shot Encode that returns a new byte[] (allocates exact-sized result)
    [Benchmark(Description = "Encode(ReadOnlySpan<byte>) -> new byte[]")]
    public Byte[] Encode_ToNewArray() => LZ4Codec.Encode(_input.AsSpan());

    // -------------------------
    // DECODE benchmarks
    // -------------------------

    // Span(in compressed) -> Span(out decompressed)
    [Benchmark(Description = "Decode(ReadOnlySpan<byte>, Span<byte>)")]
    public Int32 Decode_SpanToSpan()
    {
        Int32 written = LZ4Codec.Decode(_compressedPayload.AsSpan(), _decompressOutputBuffer.AsSpan());
        return written;
    }

    // Decode returning a newly allocated output array (out parameter)
    [Benchmark(Description = "Decode(ReadOnlySpan<byte>, out byte[] output, out int bytesWritten)")]
    public Int32 Decode_ToNewArray()
    {
        Boolean ok = LZ4Codec.Decode(_compressedPayload.AsSpan(), out var outArr, out var bytesWritten);
        if (!ok || outArr == null)
        {
            throw new InvalidOperationException("Decode failed");
        }
        // Return length to avoid JIT eliding result
        return bytesWritten;
    }
}