using BenchmarkDotNet.Attributes;
using Nalix.Common.Identity;
using Nalix.Framework.Identifiers;

namespace Nalix.Benchmark.Framework.Identifiers;

[MemoryDiagnoser]
[Config(typeof(global::Nalix.Benchmark.Framework.BenchmarkConfig))]
public class SnowflakeBenchmarks
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
    public Snowflake NewId_FromComponents()
        => Snowflake.NewId(0x11223344, 0x5566, (SnowflakeType)0x77);

    [Benchmark]
    public Snowflake NewId_FromGenerator()
        => Snowflake.NewId(SnowflakeType.Unknown, machineId: 7);

    [Benchmark]
    public bool TryWriteBytes()
        => _snowflake.TryWriteBytes(_destination);

    [Benchmark]
    public byte[] ToByteArray()
        => _snowflake.ToByteArray();

    [Benchmark]
    public Snowflake FromBytes()
        => Snowflake.FromBytes(_bytes);

    [Benchmark]
    public string ToStringHex()
        => _snowflake.ToString();
}
