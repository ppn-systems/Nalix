using System;
using BenchmarkDotNet.Attributes;
using Nalix.Codec.Options;
using Nalix.Codec.Security.Hashing;
using Nalix.Environment.Configuration;
using Nalix.Benchmarks.Shared;

namespace Nalix.Codec.Benchmarks.Security;

[Config(typeof(NalixBenchmarkConfig))]
public class HashingBenchmarks
{
    private static readonly byte[] s_key32 = new byte[32];
    private static readonly byte[] s_tag16 = new byte[16];

    [Params(64, 1024)]
    public int PayloadSize;

    private byte[] _payload = null!;
    private byte[] _hashOutput = null!;

    // PBKDF2 parameters
    private string _credential = null!;
    private byte[] _salt = null!;
    private byte[] _pbkdf2Hash = null!;

    static HashingBenchmarks()
    {
        Random.Shared.NextBytes(s_key32);
    }

    [GlobalSetup]
    public void Setup()
    {
        _payload = new byte[PayloadSize];
        Random.Shared.NextBytes(_payload);
        _hashOutput = new byte[32];

        // Lower PBKDF2 iterations for benchmark speed
        ConfigurationManager.Instance.Get<SecurityOptions>().Pbkdf2Iterations = 1000;

        _credential = "nalix-benchmark-super-secret-password-12345!";
        _salt = new byte[32];
        Random.Shared.NextBytes(_salt);
        _pbkdf2Hash = new byte[32];
    }

    [Benchmark]
    public void Keccak256_Hash()
    {
        Keccak256.HashData(_payload, _hashOutput);
    }

    [Benchmark]
    public void HmacKeccak256_Compute()
    {
        HmacKeccak256.Compute(s_key32, _payload, _hashOutput);
    }

    [Benchmark]
    public void Poly1305_Compute()
    {
        Poly1305.Compute(s_key32, _payload, s_tag16);
    }

    [Benchmark]
    public void Pbkdf2_Hash()
    {
        Pbkdf2.Hash(_credential, out _, out _);
    }

    [Benchmark]
    public bool Pbkdf2_Verify()
    {
        return Pbkdf2.Verify(_credential, _salt, _pbkdf2Hash);
    }
}
