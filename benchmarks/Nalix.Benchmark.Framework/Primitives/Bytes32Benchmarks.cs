using BenchmarkDotNet.Attributes;
using Nalix.Benchmark.Framework.Abstractions;
using Nalix.Common.Primitives;
using System.Security.Cryptography;

namespace Nalix.Benchmark.Framework.Primitives;

/// <summary>
/// Benchmarks for Bytes32 primitive operations, focusing on constant-time equality and memory safety.
/// </summary>
public class Bytes32Benchmarks : NalixBenchmarkBase
{
    private Bytes32 _f1;
    private Bytes32 _f2;
    private Bytes32 _f3;
    private byte[] _rawData = null!;
    private byte[] _dstData = null!;

    [GlobalSetup]
    public void Setup()
    {
        _rawData = new byte[32];
        _dstData = new byte[32];
        RandomNumberGenerator.Fill(_rawData);

        _f1 = new Bytes32(_rawData);
        _f2 = new Bytes32(_rawData); // Same as f1

        byte[] otherData = (byte[])_rawData.Clone();
        otherData[31] ^= 0xFF; // Different at the very end
        _f3 = new Bytes32(otherData);
    }

    [BenchmarkCategory("Equality"), Benchmark(Baseline = true, Description = "Equals (Same)")]
    public bool EqualsSame() => _f1.Equals(_f2);

    [BenchmarkCategory("Equality"), Benchmark(Description = "Equals (Different)")]
    public bool EqualsDifferent() => _f1.Equals(_f3);

    [BenchmarkCategory("Equality"), Benchmark(Description = "Operator == (Same)")]
    public bool OperatorSame() => _f1 == _f2;

    [BenchmarkCategory("Memory"), Benchmark(Description = "New from Array")]
    public Bytes32 NewFromArray() => new(_rawData);

    [BenchmarkCategory("Memory"), Benchmark(Description = "WriteTo Array")]
    public void WriteToArray() => _f1.WriteTo(_dstData);

    [BenchmarkCategory("Hashing"), Benchmark(Description = "GetHashCode")]
    public int HashCode() => _f1.GetHashCode();
}
