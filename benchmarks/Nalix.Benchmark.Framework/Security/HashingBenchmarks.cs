using BenchmarkDotNet.Attributes;
using Nalix.Framework.Security.Hashing;

namespace Nalix.Benchmark.Framework.Security;

[Config(typeof(global::Nalix.Benchmark.Framework.BenchmarkConfig))]
public class HashingBenchmarks
{
    private byte[] _data = null!;
    private byte[] _key = null!;
    private byte[] _tag = null!;
    private byte[] _hash = null!;

    [Params(64, 4096)]
    public int PayloadBytes { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        _data = new byte[this.PayloadBytes];
        _key = new byte[32];
        _tag = new byte[16];
        _hash = new byte[32];

        for (int i = 0; i < _data.Length; i++)
        {
            _data[i] = (byte)(i % 233);
        }

        for (int i = 0; i < _key.Length; i++)
        {
            _key[i] = (byte)(i + 7);
        }

        Poly1305.Compute(_key, _data, _tag);
    }

    [Benchmark]
    public void Keccak256_HashToSpan()
        => Keccak256.HashData(_data, _hash);

    [Benchmark]
    public bool Keccak256_TryHashData()
        => Keccak256.TryHashData(_data, _hash);

    [Benchmark]
    public void Poly1305_Compute()
        => Poly1305.Compute(_key, _data, _tag);

    [Benchmark]
    public bool Poly1305_Verify()
        => Poly1305.Verify(_key, _data, _tag);
}
