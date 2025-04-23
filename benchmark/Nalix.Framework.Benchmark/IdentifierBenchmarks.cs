// Copyright (c) 2025 PPN Corporation.
// All rights reserved.

using System;
using System.Text;
using BenchmarkDotNet.Attributes;
using Nalix.Common.Enums;
using Nalix.Framework.Identity;

namespace Nalix.Framework.Benchmark;

/// <summary>
/// Benchmarks for <see cref="Identifier"/> generation,
/// formatting, parsing, and equality operations.
/// </summary>
[MemoryDiagnoser]
[DisassemblyDiagnoser(maxDepth: 1)]
[Config(typeof(BenchmarkConfig))]
public class IdentifierBenchmarks
{
    private Identifier _id;
    private String? _base36;
    private Byte[]? _utf8;

    [GlobalSetup]
    public void Setup()
    {
        _id = Identifier.NewId(IdentifierType.System, machineId: 42);
        _base36 = _id.ToBase36();
        _utf8 = Encoding.ASCII.GetBytes(_base36);
    }

    [Benchmark(Baseline = true, Description = "Create NewId()")]
    public Identifier Create_NewId()
        => Identifier.NewId(IdentifierType.Session, 99);

    [Benchmark(Description = "Serialize to Base36")]
    public String Serialize_Base36()
        => _id.ToBase36();

    [Benchmark(Description = "Serialize to UTF8 (TryFormatUtf8)")]
    public Int32 Serialize_Utf8()
    {
        Span<Byte> buf = stackalloc Byte[16];
        _id.TryFormatUtf8(buf, out Int32 written);
        return written;
    }

    [Benchmark(Description = "Parse from Base36 (string)")]
    public Identifier Parse_FromString()
        => Identifier.Parse(_base36!);

    [Benchmark(Description = "Parse from UTF8 (TryParse)")]
    public Boolean Parse_FromUtf8()
        => Identifier.TryParse(_utf8, out _);

    [Benchmark(Description = "ToBytes + FromBytes")]
    public Identifier RoundTrip_Bytes()
    {
        Span<Byte> buf = stackalloc Byte[7];
        _id.TryWriteBytes(buf, out _);
        return Identifier.FromBytes(buf);
    }

    [Benchmark(Description = "Equals() normal")]
    public Boolean Compare_Equals()
        => _id.Equals(_id);

    [Benchmark(Description = "EqualsFast() optimized")]
    public Boolean Compare_EqualsFast()
        => _id.EqualsFast(in _id);

    [Benchmark(Description = "EqualsConstTime() branchless")]
    public Boolean Compare_EqualsConstTime()
        => _id.EqualsConstTime(in _id);
}