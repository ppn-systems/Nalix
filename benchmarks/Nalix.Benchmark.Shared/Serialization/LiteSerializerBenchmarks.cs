// Copyright (c) 2026 PPN Corporation. All rights reserved.

//13th Gen Intel Core i7-13620H 2.40GHz, 1 CPU, 16 logical and 10 physical cores
//.NET SDK 10.0.103
//  [Host]     : .NET 10.0.3 (10.0.3, 10.0.326.7603), X64 RyuJIT x86-64-v3 [AttachedDebugger]
//Job - HEMQOV : .NET 10.0.3 (10.0.3, 10.0.326.7603), X64 RyuJIT x86-64-v3

//InvocationCount=1  IterationCount=1  LaunchCount=1
//RunStrategy=Throughput  UnrollFactor=1  WarmupCount=0

//| Method                                                     | ArrayLength | Mean          | Error | Allocated |
//|----------------------------------------------------------- |------------ |--------------:| -----:| ---------:|
//| 'Serialize<int> -> byte[]'                                 | 1           | 3.906 ns      | NA    | -         |
//| 'Deserialize<int> <- ReadOnlySpan<byte> (ref)'             | 1           | 10.547 ns     | NA    | -         |
//| 'Serialize<LargeStruct> -> byte[]'                         | 1           | 7.812 ns      | NA    | 1 B       |
//| 'Serialize<LargeStruct> -> existing byte[] buffer'         | 1           | 13.281 ns     | NA    | -         |
//| 'Deserialize<LargeStruct> <- ReadOnlySpan<byte> (ref)'     | 1           | 26.562 ns     | NA    | 1 B       |
//| 'Serialize<int[]> -> byte[]'                               | 1           | 100.000 ns    | NA    | 2 B       |
//| 'Serialize<int[]> -> BufferLease (rent)'                   | 1           | 13,850.000 ns | NA    | 48 B      |
//| 'Deserialize<int[]> <- ReadOnlySpan<byte> (out bytesRead)' | 1           | 2,450.000 ns  | NA    | 32 B      |
//| 'Deserialize<int[]> <- BufferLease'                        | 1           | 9,550.000 ns  | NA    | 32 B      |
//| 'Serialize<int> -> byte[]'                                 | 256         | 3.906 ns      | NA    | -         |
//| 'Deserialize<int> <- ReadOnlySpan<byte> (ref)'             | 256         | 10.156 ns     | NA    | -         |
//| 'Serialize<LargeStruct> -> byte[]'                         | 256         | 10.156 ns     | NA    | 1 B       |
//| 'Serialize<LargeStruct> -> existing byte[] buffer'         | 256         | 18.750 ns     | NA    | -         |
//| 'Deserialize<LargeStruct> <- ReadOnlySpan<byte> (ref)'     | 256         | 22.656 ns     | NA    | 1 B       |
//| 'Serialize<int[]> -> byte[]'                               | 256         | 100.000 ns    | NA    | 66 B      |
//| 'Serialize<int[]> -> BufferLease (rent)'                   | 256         | 14,100.000 ns | NA    | 48 B      |
//| 'Deserialize<int[]> <- ReadOnlySpan<byte> (out bytesRead)' | 256         | 8,200.000 ns  | NA    | 1048 B    |
//| 'Deserialize<int[]> <- BufferLease'                        | 256         | 8,300.000 ns  | NA    | 1048 B    |
//| 'Serialize<int> -> byte[]'                                 | 2048        | 5.469 ns      | NA    | -         |
//| 'Deserialize<int> <- ReadOnlySpan<byte> (ref)'             | 2048        | 11.719 ns     | NA    | -         |
//| 'Serialize<LargeStruct> -> byte[]'                         | 2048        | 15.625 ns     | NA    | 1 B       |
//| 'Serialize<LargeStruct> -> existing byte[] buffer'         | 2048        | 17.188 ns     | NA    | -         |
//| 'Deserialize<LargeStruct> <- ReadOnlySpan<byte> (ref)'     | 2048        | 18.750 ns     | NA    | 1 B       |
//| 'Serialize<int[]> -> byte[]'                               | 2048        | 650.000 ns    | NA    | 514 B     |
//| 'Serialize<int[]> -> BufferLease (rent)'                   | 2048        | 19,000.000 ns | NA    | 48 B      |
//| 'Deserialize<int[]> <- ReadOnlySpan<byte> (out bytesRead)' | 2048        | 15,900.000 ns | NA    | 8216 B    |
//| 'Deserialize<int[]> <- BufferLease'                        | 2048        | 12,700.000 ns | NA    | 8216 B    |

using BenchmarkDotNet.Attributes;
using Nalix.Shared.Memory.Buffers;
using Nalix.Shared.Serialization;
using System;
using System.Buffers;

namespace Nalix.Benchmark.Shared.Serialization;

[MemoryDiagnoser]
// Reduce total runtime for development feedback:
// - launchCount:1 avoids multiple process launches
// - warmupCount:0 skips warmup (less stable but faster)
// - iterationCount:1 single measurement iteration
// - invocationCount:1 single invocation block per iteration
[SimpleJob(
    BenchmarkDotNet.Engines.RunStrategy.Throughput,
    warmupCount: 0,
    iterationCount: 1,
    invocationCount: 1,
    launchCount: 1)]
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