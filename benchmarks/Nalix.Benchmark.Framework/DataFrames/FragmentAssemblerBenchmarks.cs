using System;
using BenchmarkDotNet.Attributes;
using Nalix.Framework.DataFrames.Chunks;
using Nalix.Framework.Memory.Buffers;

namespace Nalix.Benchmark.Framework.DataFrames;

[MemoryDiagnoser]
[Config(typeof(global::Nalix.Benchmark.Framework.BenchmarkConfig))]
public class FragmentAssemblerBenchmarks
{
    private byte[][] _chunks = null!;
    private FragmentHeader[] _headers = null!;
    private byte[] _fragmentedPayload = null!;

    [Params(4, 16)]
    public int ChunkCount { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        int chunkSize = 256;
        _headers = new FragmentHeader[this.ChunkCount];
        _chunks = new byte[this.ChunkCount][];
        _fragmentedPayload = new byte[FragmentHeader.WireSize + chunkSize];

        for (int i = 0; i < this.ChunkCount; i++)
        {
            _headers[i] = new FragmentHeader(1, (ushort)i, (ushort)this.ChunkCount, i == this.ChunkCount - 1);
            _chunks[i] = new byte[chunkSize];
            for (int j = 0; j < chunkSize; j++)
            {
                _chunks[i][j] = (byte)((i + j) % 241);
            }
        }

        _headers[0].WriteTo(_fragmentedPayload);
    }

    [Benchmark]
    public int Assemble_SequentialChunks()
    {
        using FragmentAssembler assembler = new();
        BufferLease? completed = null;
        try
        {
            for (int i = 0; i < _headers.Length; i++)
            {
                completed = assembler.Add(_headers[i], _chunks[i], out _);
            }

            return completed?.Length ?? 0;
        }
        finally
        {
            completed?.Dispose();
        }
    }

    [Benchmark]
    public bool IsFragmentedFrame()
        => FragmentAssembler.IsFragmentedFrame(_fragmentedPayload, out _);
}
