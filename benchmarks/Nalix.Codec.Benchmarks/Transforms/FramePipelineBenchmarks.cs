using System;
using BenchmarkDotNet.Attributes;
using Nalix.Abstractions;
using Nalix.Abstractions.Networking.Packets;
using Nalix.Abstractions.Primitives;
using Nalix.Abstractions.Security;
using Nalix.Benchmarks.Shared;
using Nalix.Codec.Transforms;
using Nalix.Environment.Extensions;
using Nalix.Environment.Memory;

namespace Nalix.Codec.Benchmarks.Transforms;

[Config(typeof(NalixBenchmarkConfig))]
public class FramePipelineBenchmarks
{
    private static readonly byte[] s_testKey = new byte[32];

    [Params(64, 512, 4096)]
    public int PayloadSize;

    private byte[] _payload = null!;
    private byte[] _outboundBuffer = null!;
    private byte[] _inboundBuffer = null!;

    static FramePipelineBenchmarks()
    {
        Random.Shared.NextBytes(s_testKey);
    }

    [GlobalSetup]
    public void Setup()
    {
        _payload = new byte[PayloadSize];
        Random.Shared.NextBytes(_payload);

        // Pre-allocate arrays
        _outboundBuffer = new byte[FrameTransformer.Offset + PayloadSize + 128]; // Extra margin for cipher headers
        _inboundBuffer = new byte[FrameTransformer.Offset + PayloadSize + 128];
    }

    private IBufferLease CreateSourceLease()
    {
        var lease = BufferLease.Rent(FrameTransformer.Offset + PayloadSize);
        lease.CommitLength(FrameTransformer.Offset + PayloadSize);
        lease.Span[..FrameTransformer.Offset].Clear();
        lease.Span.AsHeaderRef() = new PacketHeader { Flags = PacketFlags.RELIABLE };
        _payload.CopyTo(lease.Span[FrameTransformer.Offset..]);
        return lease;
    }

    [Benchmark]
    public void ProcessOutbound_CompressOnly()
    {
        IBufferLease lease = CreateSourceLease();
        FramePipeline.ProcessOutbound(
            ref lease,
            enableCompress: true,
            minSizeToCompress: 1,
            enableEncrypt: false,
            secret: ReadOnlySpan<byte>.Empty,
            seq: null,
            algorithm: CipherSuiteType.None);
        lease.Dispose();
    }

    [Benchmark]
    public void ProcessOutbound_EncryptOnly()
    {
        IBufferLease lease = CreateSourceLease();
        FramePipeline.ProcessOutbound(
            ref lease,
            enableCompress: false,
            minSizeToCompress: 1,
            enableEncrypt: true,
            secret: s_testKey,
            seq: 1,
            algorithm: CipherSuiteType.Chacha20Poly1305);
        lease.Dispose();
    }

    [Benchmark]
    public void ProcessOutbound_Full()
    {
        IBufferLease lease = CreateSourceLease();
        FramePipeline.ProcessOutbound(
            ref lease,
            enableCompress: true,
            minSizeToCompress: 1,
            enableEncrypt: true,
            secret: s_testKey,
            seq: 1,
            algorithm: CipherSuiteType.Chacha20Poly1305);
        lease.Dispose();
    }

    // ── Inbound ──

    private IBufferLease CreateInboundLease(bool compressed, bool encrypted)
    {
        IBufferLease lease = CreateSourceLease();
        FramePipeline.ProcessOutbound(
            ref lease,
            enableCompress: compressed,
            minSizeToCompress: 1,
            enableEncrypt: encrypted,
            secret: s_testKey,
            seq: 1,
            algorithm: encrypted ? CipherSuiteType.Chacha20Poly1305 : CipherSuiteType.None);
        return lease;
    }

    [Benchmark]
    public void ProcessInbound_DecompressOnly()
    {
        IBufferLease lease = CreateInboundLease(compressed: true, encrypted: false);
        FramePipeline.ProcessInbound(ref lease, ReadOnlySpan<byte>.Empty, CipherSuiteType.None, out _);
        lease.Dispose();
    }

    [Benchmark]
    public void ProcessInbound_DecryptOnly()
    {
        IBufferLease lease = CreateInboundLease(compressed: false, encrypted: true);
        FramePipeline.ProcessInbound(ref lease, s_testKey, CipherSuiteType.Chacha20Poly1305, out _);
        lease.Dispose();
    }

    [Benchmark]
    public void ProcessInbound_Full()
    {
        IBufferLease lease = CreateInboundLease(compressed: true, encrypted: true);
        FramePipeline.ProcessInbound(ref lease, s_testKey, CipherSuiteType.Chacha20Poly1305, out _);
        lease.Dispose();
    }
}
