// Copyright (c) 2025-2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.
// Copyright (c) 2026 PPN Corporation. All rights reserved.

using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Diagnosers;
using BenchmarkDotNet.Order;
using Nalix.Shared.Memory.Buffers;
using Nalix.Shared.Serialization;
using System;
using System.Buffers;

namespace Nalix.Benchmark.Shared.Serialization;

[RankColumn]
[MemoryDiagnoser]
[DisassemblyDiagnoser]
[MinColumn, MaxColumn]
[Orderer(SummaryOrderPolicy.FastestToSlowest)]
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