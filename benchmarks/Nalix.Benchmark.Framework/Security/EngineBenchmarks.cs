using System;
using BenchmarkDotNet.Attributes;
using Nalix.Common.Security;
using Nalix.Framework.Security.Engine;

namespace Nalix.Benchmark.Framework.Security;

[MemoryDiagnoser]
[Config(typeof(global::Nalix.Benchmark.Framework.BenchmarkConfig))]
public class EngineBenchmarks
{
    private byte[] _key = null!;
    private byte[] _nonce12 = null!;
    private byte[] _nonce8 = null!;
    private byte[] _aad = null!;
    private byte[] _plaintext = null!;
    private byte[] _symEnvelope = null!;
    private byte[] _aeadEnvelope = null!;
    private byte[] _output = null!;
    private int _symWritten;
    private int _aeadWritten;

    [Params(64, 1024)]
    public int PayloadBytes { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        _key = new byte[32];
        _nonce12 = new byte[12];
        _nonce8 = new byte[8];
        _aad = new byte[16];
        _plaintext = new byte[this.PayloadBytes];
        _output = new byte[this.PayloadBytes + 128];

        for (int i = 0; i < _key.Length; i++) _key[i] = (byte)(i + 1);
        for (int i = 0; i < _nonce12.Length; i++) _nonce12[i] = (byte)(i + 2);
        for (int i = 0; i < _nonce8.Length; i++) _nonce8[i] = (byte)(i + 3);
        for (int i = 0; i < _aad.Length; i++) _aad[i] = (byte)(i + 4);
        for (int i = 0; i < _plaintext.Length; i++) _plaintext[i] = (byte)(i % 211);

        _symEnvelope = new byte[this.PayloadBytes + 64];
        SymmetricEngine.Encrypt(_key, _plaintext, _symEnvelope, _nonce12, 7u, CipherSuiteType.Chacha20, out _symWritten);

        _aeadEnvelope = new byte[this.PayloadBytes + 64];
        AeadEngine.Encrypt(_key, _plaintext, _aeadEnvelope, _nonce12, _aad, 7u, CipherSuiteType.Chacha20Poly1305, out _aeadWritten);
    }

    [Benchmark]
    public int SymmetricEngine_Encrypt_Envelope()
    {
        SymmetricEngine.Encrypt(_key, _plaintext, _symEnvelope, _nonce12, 7u, CipherSuiteType.Chacha20, out int written);
        return written;
    }

    [Benchmark]
    public int SymmetricEngine_Decrypt_Envelope()
    {
        SymmetricEngine.Decrypt(_key, MemoryExtensions.AsSpan(_symEnvelope, 0, _symWritten), _output, out int written);
        return written;
    }

    [Benchmark]
    public int AeadEngine_Encrypt()
    {
        AeadEngine.Encrypt(_key, _plaintext, _aeadEnvelope, _nonce12, _aad, 7u, CipherSuiteType.Chacha20Poly1305, out int written);
        return written;
    }

    [Benchmark]
    public int AeadEngine_Decrypt()
    {
        AeadEngine.Decrypt(_key, MemoryExtensions.AsSpan(_aeadEnvelope, 0, _aeadWritten), _output, _aad, out int written);
        return written;
    }
}
