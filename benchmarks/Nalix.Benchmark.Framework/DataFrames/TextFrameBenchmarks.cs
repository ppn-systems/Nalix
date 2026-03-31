using BenchmarkDotNet.Attributes;
using Nalix.Common.Networking.Packets;
using Nalix.Common.Networking.Protocols;
using Nalix.Framework.DataFrames;
using Nalix.Framework.DataFrames.TextFrames;

namespace Nalix.Benchmark.Framework.DataFrames;

[MemoryDiagnoser]
[Config(typeof(BenchmarkConfig))]
public class TextFrameBenchmarks
{
    private Text256 _frame = null!;
    private byte[] _serializeBuffer = null!;
    private byte[] _serialized = null!;
    private PacketRegistry _registry = null!;

    [Params(32, 128, 256)]
    public int PayloadBytes { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        _frame = new Text256();
        _frame.Initialize(new string('x', this.PayloadBytes), ProtocolType.TCP);
        _frame.OpCode = PacketConstants.OpcodeDefault;
        _serializeBuffer = new byte[_frame.Length];
        _serialized = _frame.Serialize();
        _registry = new PacketRegistryFactory().CreateCatalog();
    }

    [Benchmark]
    public int ComputeLength() => _frame.Length;

    [Benchmark]
    public byte[] Serialize() => _frame.Serialize();

    [Benchmark]
    public int SerializeIntoSpan() => _frame.Serialize(_serializeBuffer);

    [Benchmark]
    public Text256 Deserialize() => Text256.Deserialize(_serialized);
}
