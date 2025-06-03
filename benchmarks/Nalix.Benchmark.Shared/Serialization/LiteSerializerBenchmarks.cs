// Copyright (c) 2026 PPN Corporation. All rights reserved.

// 13th Gen Intel Core i7-13620H 2.40GHz, 1 CPU, 16 logical and 10 physical cores
// .NET SDK 10.0.103
//   [Host]     : .NET 10.0.3 (10.0.3, 10.0.326.7603), X64 RyuJIT x86-64-v3
//   Job-FCUYYQ : .NET 10.0.3 (10.0.3, 10.0.326.7603), X64 RyuJIT x86-64-v3

// IterationCount=20  LaunchCount=3  WarmupCount=5

// | Method                                                     | ArrayLength | Mean        | Error      | StdDev     | Median      | Code Size | Gen0   | Allocated |
// |----------------------------------------------------------- |------------ |------------:|-----------:|-----------:|------------:|----------:|-------:|----------:|
// | 'Serialize<int> -> byte[]'                                 | 1           |   0.0470 ns |  0.0014 ns |  0.0031 ns |   0.0476 ns |   9,058 B | 0.0000 |         - |
// | 'Deserialize<int> <- ReadOnlySpan<byte> (ref)'             | 1           |   0.0806 ns |  0.0017 ns |  0.0039 ns |   0.0801 ns |  16,120 B | 0.0000 |         - |
// | 'Serialize<LargeStruct> -> byte[]'                         | 1           |   0.1222 ns |  0.0019 ns |  0.0042 ns |   0.1225 ns |   9,144 B | 0.0001 |       1 B |
// | 'Serialize<LargeStruct> -> existing byte[] buffer'         | 1           |   0.0400 ns |  0.0015 ns |  0.0034 ns |   0.0390 ns |   8,202 B |      - |         - |
// | 'Deserialize<LargeStruct> <- ReadOnlySpan<byte> (ref)'     | 1           |   0.2077 ns |  0.0029 ns |  0.0064 ns |   0.2073 ns |  16,319 B | 0.0001 |       1 B |
// | 'Serialize<int[]> -> byte[]'                               | 1           |   0.5838 ns |  0.0084 ns |  0.0187 ns |   0.5862 ns |   2,232 B | 0.0002 |       2 B |
// | 'Serialize<int[]> -> BufferLease (rent)'                   | 1           |  80.5400 ns |  0.8299 ns |  1.8562 ns |  81.1356 ns |   7,872 B | 0.0038 |      48 B |
// | 'Deserialize<int[]> <- ReadOnlySpan<byte> (out bytesRead)' | 1           |  32.1597 ns |  0.4613 ns |  1.0317 ns |  32.3540 ns |   3,394 B | 0.0025 |      32 B |
// | 'Deserialize<int[]> <- BufferLease'                        | 1           |  32.8753 ns |  0.4733 ns |  1.0586 ns |  32.7878 ns |   3,527 B | 0.0025 |      32 B |
// | 'Serialize<int> -> byte[]'                                 | 256         |   0.0476 ns |  0.0010 ns |  0.0022 ns |   0.0481 ns |   9,058 B | 0.0000 |         - |
// | 'Deserialize<int> <- ReadOnlySpan<byte> (ref)'             | 256         |   0.1097 ns |  0.0119 ns |  0.0267 ns |   0.1271 ns |  16,120 B | 0.0000 |         - |
// | 'Serialize<LargeStruct> -> byte[]'                         | 256         |   0.1244 ns |  0.0015 ns |  0.0034 ns |   0.1240 ns |   9,144 B | 0.0001 |       1 B |
// | 'Serialize<LargeStruct> -> existing byte[] buffer'         | 256         |   0.0413 ns |  0.0006 ns |  0.0014 ns |   0.0412 ns |   8,202 B |      - |         - |
// | 'Deserialize<LargeStruct> <- ReadOnlySpan<byte> (ref)'     | 256         |   0.2114 ns |  0.0020 ns |  0.0045 ns |   0.2109 ns |  16,319 B | 0.0001 |       1 B |
// | 'Serialize<int[]> -> byte[]'                               | 256         |   3.0885 ns |  0.0880 ns |  0.1969 ns |   3.1674 ns |   2,232 B | 0.0053 |      66 B |
// | 'Serialize<int[]> -> BufferLease (rent)'                   | 256         |  90.0919 ns |  1.6972 ns |  3.7610 ns |  89.1577 ns |   7,872 B | 0.0038 |      48 B |
// | 'Deserialize<int[]> <- ReadOnlySpan<byte> (out bytesRead)' | 256         |  82.3285 ns |  4.0289 ns |  9.0113 ns |  86.2776 ns |   3,394 B | 0.0834 |    1048 B |
// | 'Deserialize<int[]> <- BufferLease'                        | 256         |  84.5338 ns |  3.8384 ns |  8.5851 ns |  89.3952 ns |   3,527 B | 0.0834 |    1048 B |
// | 'Serialize<int> -> byte[]'                                 | 2048        |   0.0501 ns |  0.0009 ns |  0.0019 ns |   0.0504 ns |   9,058 B | 0.0000 |         - |
// | 'Deserialize<int> <- ReadOnlySpan<byte> (ref)'             | 2048        |   0.0842 ns |  0.0015 ns |  0.0033 ns |   0.0848 ns |  16,120 B | 0.0000 |         - |
// | 'Serialize<LargeStruct> -> byte[]'                         | 2048        |   0.1231 ns |  0.0020 ns |  0.0045 ns |   0.1230 ns |   9,144 B | 0.0001 |       1 B |
// | 'Serialize<LargeStruct> -> existing byte[] buffer'         | 2048        |   0.0396 ns |  0.0017 ns |  0.0038 ns |   0.0408 ns |   8,202 B |      - |         - |
// | 'Deserialize<LargeStruct> <- ReadOnlySpan<byte> (ref)'     | 2048        |   0.2274 ns |  0.0060 ns |  0.0134 ns |   0.2250 ns |  16,319 B | 0.0001 |       1 B |
// | 'Serialize<int[]> -> byte[]'                               | 2048        |  16.2017 ns |  0.2650 ns |  0.5928 ns |  16.2173 ns |   2,232 B | 0.0408 |     514 B |
// | 'Serialize<int[]> -> BufferLease (rent)'                   | 2048        | 195.4992 ns |  2.5629 ns |  5.6255 ns | 196.2699 ns |   7,874 B | 0.0038 |      48 B |
// | 'Deserialize<int[]> <- ReadOnlySpan<byte> (out bytesRead)' | 2048        | 313.2143 ns |  5.4935 ns | 12.1732 ns | 312.2344 ns |   3,394 B | 0.6542 |    8216 B |
// | 'Deserialize<int[]> <- BufferLease'                        | 2048        | 330.9719 ns | 13.5564 ns | 30.0401 ns | 317.8686 ns |   3,527 B | 0.6542 |    8216 B |

using BenchmarkDotNet.Attributes;
using Nalix.Shared.Memory.Buffers;
using Nalix.Shared.Serialization;
using System;
using System.Buffers;

namespace Nalix.Benchmark.Shared.Serialization;

[MemoryDiagnoser]
[DisassemblyDiagnoser]
[SimpleJob(
    launchCount: 3,
    warmupCount: 5,
    iterationCount: 20)]
// Reduce total runtime for development feedback:
// - launchCount:1 avoids multiple process launches
// - warmupCount:0 skips warmup (less stable but faster)
// - iterationCount:1 single measurement iteration
// - invocationCount:1 single invocation block per iteration
[System.Diagnostics.CodeAnalysis.SuppressMessage("Style", "IDE0301:Simplify collection initialization", Justification = "<Pending>")]
public class LiteSerializerBenchmarks
{
    [Params(1 /*small*/, 256 /*medium*/, 2048 /*large*/)]
    public Int32 ArrayLength;

    private Int32 _sampleInt;
    private LargeStruct _sampleStruct;
    private Int32[] _sampleIntArray = Array.Empty<Int32>();
    private Byte[] _writeBuffer = Array.Empty<Byte>();
    private Byte[] _serializedArrayBytes = Array.Empty<Byte>();
    private BufferLease _serializedArrayLease = null;

    [GlobalSetup]
    public void GlobalSetup()
    {
        _sampleInt = 123456789;
        _sampleStruct = new LargeStruct { A = 1, B = 2, C = 3, D = 4, E = 5, F = 6, G = 7, H = 8 };

        _sampleIntArray = new Int32[ArrayLength];

        // Use fast pseudo-random for dev data to save time (not crypto-grade).
        var rng = new Random(42);
        for (Int32 i = 0; i < _sampleIntArray.Length; i++)
        {
            _sampleIntArray[i] = rng.Next();
        }

        // Precompute serialized payloads
        _serializedArrayBytes = LiteSerializer.Serialize<Int32[]>(_sampleIntArray);

        _serializedArrayLease?.Dispose();
        _serializedArrayLease = LiteSerializer.Serialize<Int32[]>(_sampleIntArray, zeroOnDispose: false);

        Int32 needed = Math.Max(_serializedArrayBytes.Length, System.Runtime.CompilerServices.Unsafe.SizeOf<LargeStruct>());
        _writeBuffer = ArrayPool<Byte>.Shared.Rent(needed + 16);
    }

    [GlobalCleanup]
    public void GlobalCleanup()
    {
        _serializedArrayLease?.Dispose();
        _serializedArrayLease = null;

        if (_writeBuffer.Length > 0)
        {
            ArrayPool<Byte>.Shared.Return(_writeBuffer);
            _writeBuffer = Array.Empty<Byte>();
        }
    }

    // ---------------------------
    // Scalar unmanaged (int)
    // ---------------------------

    // Use OperationsPerInvoke to reduce BenchmarkDotNet's per-invocation overhead.
    // It runs this method multiple times in a single measurement and divides result.
    // Be careful: does not change measured behavior other than averaging more operations.
    [Benchmark(Description = "Serialize<int> -> byte[]", OperationsPerInvoke = 128)]
    public Int32 Serialize_Int_ToArray()
    {
        var bytes = LiteSerializer.Serialize<Int32>(_sampleInt);
        return bytes.Length;
    }

    [Benchmark(Description = "Deserialize<int> <- ReadOnlySpan<byte> (ref)", OperationsPerInvoke = 128)]
    public Int32 Deserialize_Int_FromSpan()
    {
        Int32 dest = 0;
        var payload = LiteSerializer.Serialize<Int32>(_sampleInt);
        Int32 read = LiteSerializer.Deserialize<Int32>(payload.AsSpan(), ref dest);
        return read;
    }

    // ---------------------------
    // Unmanaged struct
    // ---------------------------

    [Benchmark(Description = "Serialize<LargeStruct> -> byte[]", OperationsPerInvoke = 64)]
    public Int32 Serialize_Struct_ToArray()
    {
        var bytes = LiteSerializer.Serialize<LargeStruct>(_sampleStruct);
        return bytes.Length;
    }

    [Benchmark(Description = "Serialize<LargeStruct> -> existing byte[] buffer", OperationsPerInvoke = 64)]
    public Int32 Serialize_Struct_ToBuffer()
    {
        Int32 written = LiteSerializer.Serialize<LargeStruct>(_sampleStruct, _writeBuffer);
        return written;
    }

    [Benchmark(Description = "Deserialize<LargeStruct> <- ReadOnlySpan<byte> (ref)", OperationsPerInvoke = 64)]
    public Int32 Deserialize_Struct_FromSpan()
    {
        var payload = LiteSerializer.Serialize<LargeStruct>(_sampleStruct);
        LargeStruct dest = default;
        Int32 read = LiteSerializer.Deserialize<LargeStruct>(payload.AsSpan(), ref dest);
        return read;
    }

    // ---------------------------
    // Unmanaged array (int[])
    // ---------------------------

    [Benchmark(Description = "Serialize<int[]> -> byte[]", OperationsPerInvoke = 16)]
    public Int32 Serialize_Array_ToArray()
    {
        var bytes = LiteSerializer.Serialize<Int32[]>(_sampleIntArray);
        return bytes.Length;
    }

    [Benchmark(Description = "Serialize<int[]> -> BufferLease (rent)")]
    public Int32 Serialize_Array_ToLease_AndDispose()
    {
        BufferLease lease = LiteSerializer.Serialize<Int32[]>(_sampleIntArray, zeroOnDispose: false);
        try
        {
            return lease.Length;
        }
        finally
        {
            lease.Dispose();
        }
    }

    [Benchmark(Description = "Deserialize<int[]> <- ReadOnlySpan<byte> (out bytesRead)")]
    public Int32 Deserialize_Array_FromSpan_OutBytes()
    {
        Int32[] arr = LiteSerializer.Deserialize<Int32[]>(_serializedArrayBytes.AsSpan(), out Int32 bytesRead);
        return arr?.Length ?? -1;
    }

    [Benchmark(Description = "Deserialize<int[]> <- BufferLease")]
    public Int32 Deserialize_Array_FromBufferLease()
    {
        if (_serializedArrayLease is null)
        {
            return -1;
        }

        Int32[] arr = LiteSerializer.Deserialize<Int32[]>(_serializedArrayLease, out Int32 bytesRead);
        return arr?.Length ?? -1;
    }
}

public struct LargeStruct
{
    public Int64 A;
    public Int64 B;
    public Int64 C;
    public Int64 D;
    public Int64 E;
    public Int64 F;
    public Int64 G;
    public Int64 H;
}