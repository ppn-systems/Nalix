using System;
using BenchmarkDotNet.Attributes;
using Nalix.Abstractions.Primitives;
using Nalix.Benchmarks.Shared;
using Nalix.Codec.Security;

namespace Nalix.Codec.Benchmarks.Security;

[Config(typeof(NalixBenchmarkConfig))]
public class HandshakeBenchmarks
{
    private Bytes32 _sharedSecretEE;
    private Bytes32 _sharedSecretSE;
    private Bytes32 _masterSecret;
    private Bytes32 _transcriptHash;
    private Bytes32 _clientNonce;
    private Bytes32 _serverNonce;

    [GlobalSetup]
    public void Setup()
    {
        Span<byte> randomBytes = stackalloc byte[32];
        
        Random.Shared.NextBytes(randomBytes);
        _sharedSecretEE = new Bytes32(randomBytes);

        Random.Shared.NextBytes(randomBytes);
        _sharedSecretSE = new Bytes32(randomBytes);

        Random.Shared.NextBytes(randomBytes);
        _masterSecret = new Bytes32(randomBytes);

        Random.Shared.NextBytes(randomBytes);
        _transcriptHash = new Bytes32(randomBytes);

        Random.Shared.NextBytes(randomBytes);
        _clientNonce = new Bytes32(randomBytes);

        Random.Shared.NextBytes(randomBytes);
        _serverNonce = new Bytes32(randomBytes);
    }

    [Benchmark]
    public Bytes32 ComputeMasterSecret()
    {
        return HandshakeX25519.ComputeMasterSecret(_sharedSecretEE, _sharedSecretSE);
    }

    [Benchmark]
    public Bytes32 ComputeServerProof()
    {
        return HandshakeX25519.ComputeServerProof(_masterSecret, _transcriptHash);
    }

    [Benchmark]
    public Bytes32 ComputeClientProof()
    {
        return HandshakeX25519.ComputeClientProof(_masterSecret, _transcriptHash);
    }

    [Benchmark]
    public Bytes32 DeriveSessionKey()
    {
        return HandshakeX25519.DeriveSessionKey(_masterSecret, _clientNonce, _serverNonce, _transcriptHash);
    }
}
