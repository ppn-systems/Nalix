using BenchmarkDotNet.Attributes;
using Nalix.Benchmark.Framework.Abstractions;
using Nalix.Common.Identity;
using Nalix.Framework.Identifiers;

namespace Nalix.Benchmark.Framework.Identifiers;

/// <summary>
/// Benchmarks for Snowflake identifier generation, serialization, and string conversion.
/// </summary>
public class SnowflakeBenchmarks : NalixBenchmarkBase
{
    private Snowflake _snowflake;
    private byte[] _bytes = null!;
    private byte[] _destination = null!;

    [GlobalSetup]
    public void Setup()
    {
        _snowflake = Snowflake.NewId(0x11223344, 0x5566, (SnowflakeType)0x77);
        _bytes = _snowflake.ToByteArray();
        _destination = new byte[Snowflake.Size];
    }

    [Benchmark]
    public Snowflake CreateFromComponents()
        => Snowflake.NewId(0x11223344, 0x5566, (SnowflakeType)0x77);

    [Benchmark]
    public Snowflake CreateFromGenerator()
        => Snowflake.NewId(SnowflakeType.Unknown, machineId: 7);

    [Benchmark]
    public bool WriteToBytes()
        => _snowflake.TryWriteBytes(_destination);

    [Benchmark]
    public byte[] SerializeToByteArray()
        => _snowflake.ToByteArray();

    [Benchmark]
    public Snowflake DeserializeFromBytes()
        => Snowflake.FromBytes(_bytes);

    [Benchmark]
    public string ConvertToString()
        => _snowflake.ToString();
}
