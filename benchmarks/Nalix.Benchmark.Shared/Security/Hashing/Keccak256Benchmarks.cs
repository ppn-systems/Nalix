// Copyright (c) 2026 PPN Corporation. All rights reserved.

//13th Gen Intel Core i7-13620H 2.40GHz, 1 CPU, 16 logical and 10 physical cores
//.NET SDK 10.0.103
//  [Host]     : .NET 10.0.3 (10.0.3, 10.0.326.7603), X64 RyuJIT x86-64-v3 [AttachedDebugger]
//DefaultJob: .NET 10.0.3(10.0.3, 10.0.326.7603), X64 RyuJIT x86-64-v3

// | Method                                           | PayloadSize | Mean      | Error | Allocated |
// |------------------------------------------------- |------------ |----------:|------:|----------:|
// | 'Keccak256.HashData (one-shot)'                  | 16          |  2.600 us |    NA |     552 B |
// | 'Instance: Update + Finish()'                    | 16          |  2.800 us |    NA |     552 B |
// | 'Instance: Update + Finish(Span) (preallocated)' | 16          |  3.100 us |    NA |     552 B |
// | 'Incremental updates: 16-byte chunks'            | 16          |  3.150 us |    NA |     552 B |
// | 'Incremental updates: block-sized chunks (rate)' | 16          |  2.600 us |    NA |     552 B |
// | 'Keccak256.HashData (one-shot)'                  | 128         |  2.300 us |    NA |     552 B |
// | 'Instance: Update + Finish()'                    | 128         |  3.000 us |    NA |     552 B |
// | 'Instance: Update + Finish(Span) (preallocated)' | 128         |  3.300 us |    NA |     552 B |
// | 'Incremental updates: 16-byte chunks'            | 128         |  2.800 us |    NA |     552 B |
// | 'Incremental updates: block-sized chunks (rate)' | 128         |  2.500 us |    NA |     552 B |
// | 'Keccak256.HashData (one-shot)'                  | 1024        |  6.100 us |    NA |     552 B |
// | 'Instance: Update + Finish()'                    | 1024        |  6.500 us |    NA |     552 B |
// | 'Instance: Update + Finish(Span) (preallocated)' | 1024        |  6.400 us |    NA |     552 B |
// | 'Incremental updates: 16-byte chunks'            | 1024        |  8.000 us |    NA |     552 B |
// | 'Incremental updates: block-sized chunks (rate)' | 1024        |  5.700 us |    NA |     552 B |
// | 'Keccak256.HashData (one-shot)'                  | 8192        | 49.600 us |    NA |     552 B |
// | 'Instance: Update + Finish()'                    | 8192        | 29.500 us |    NA |     552 B |
// | 'Instance: Update + Finish(Span) (preallocated)' | 8192        | 29.200 us |    NA |     552 B |
// | 'Incremental updates: 16-byte chunks'            | 8192        | 39.600 us |    NA |     552 B |
// | 'Incremental updates: block-sized chunks (rate)' | 8192        | 37.400 us |    NA |     552 B |

using BenchmarkDotNet.Attributes;
using Nalix.Shared.Security.Hashing;
using System;
using System.Security.Cryptography;

namespace Nalix.Benchmark.Shared.Security.Hashing;

// MemoryDiagnoser captures allocations; multiple payload sizes exercise different code paths.
[SimpleJob(
    BenchmarkDotNet.Engines.RunStrategy.Throughput,
    warmupCount: 1,
    iterationCount: 1,
    invocationCount: 1,
    launchCount: 1)]
[MemoryDiagnoser]
public class Keccak256Benchmarks
{
    // Payload sizes in bytes: small, medium, large, very large
    [Params(16, 128, 1024, 8192)]
    public Int32 PayloadSize;

    private Byte[] _data = Array.Empty<Byte>();
    private Byte[] _outputBuffer = new Byte[32];

    // Prepare random input once per PayloadSize
    [GlobalSetup]
    public void GlobalSetup()
    {
        _data = new Byte[PayloadSize];
        RandomNumberGenerator.Fill(_data);
        _outputBuffer = new Byte[32]; // 32-byte digest
    }

    // One-shot convenience API: allocates and returns a new 32-byte array
    [Benchmark(Description = "Keccak256.HashData (one-shot)")]
    public Byte[] OneShot_HashData() => Keccak256.HashData(_data);

    // Create instance, call Update(data) then Finish() returning allocated array
    [Benchmark(Description = "Instance: Update + Finish()")]
    public Byte[] Instance_Update_Finish()
    {
        using Keccak256 sha = new();
        sha.Update(_data);
        return sha.Finish();
    }

    // Same as above but use the Span-based Finish(output) with preallocated buffer (no final allocation)
    [Benchmark(Description = "Instance: Update + Finish(Span) (preallocated)")]
    public Byte[] Instance_Update_Finish_Span()
    {
        using Keccak256 sha = new();
        sha.Update(_data);
        // write into preallocated buffer
        sha.Finish(_outputBuffer);
        // return a copy to avoid aliasing in subsequent iterations
        return (Byte[])_outputBuffer.Clone();
    }

    // Incremental updates in small chunks (16 bytes) to stress Update tail buffering path
    [Benchmark(Description = "Incremental updates: 16-byte chunks")]
    public Byte[] Incremental_Chunks_16()
    {
        using Keccak256 sha = new();
        Int32 offset = 0;
        Int32 remaining = _data.Length;
        const Int32 chunk = 16;
        while (remaining > 0)
        {
            Int32 take = Math.Min(chunk, remaining);
            sha.Update(_data.AsSpan(offset, take));
            offset += take;
            remaining -= take;
        }
        return sha.Finish();
    }

    // Incremental updates using block-sized chunks (RateBytes) to trigger AbsorbBlock fast path
    [Benchmark(Description = "Incremental updates: block-sized chunks (rate)")]
    public Byte[] Incremental_Chunks_Rate()
    {
        using Keccak256 sha = new();
        const Int32 rate = 136; // Keccak256.RateBytes
        Int32 offset = 0;
        Int32 remaining = _data.Length;
        while (remaining > 0)
        {
            Int32 take = Math.Min(rate, remaining);
            sha.Update(_data.AsSpan(offset, take));
            offset += take;
            remaining -= take;
        }
        return sha.Finish();
    }
}