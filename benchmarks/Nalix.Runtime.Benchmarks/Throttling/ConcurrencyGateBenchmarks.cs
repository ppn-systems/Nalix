using System;
using BenchmarkDotNet.Attributes;
using Nalix.Abstractions.Networking.Packets;
using Nalix.Environment.Configuration;
using Nalix.Runtime.Options;
using Nalix.Runtime.Throttling;
using Nalix.Benchmarks.Shared;

namespace Nalix.Runtime.Benchmarks.Throttling;

[Config(typeof(NalixBenchmarkConfig))]
public class ConcurrencyGateBenchmarks
{
    private ConcurrencyGate _gate = null!;
    private PacketConcurrencyLimitAttribute _attribute = null!;

    [GlobalSetup]
    public void Setup()
    {
        var options = ConfigurationManager.Instance.Get<ConcurrencyOptions>();
        options.CleanupIntervalMinutes = 60;

        _gate = new ConcurrencyGate();
        _attribute = new PacketConcurrencyLimitAttribute(100000, false, 0);
    }

    [Benchmark]
    public void TryEnterAndDispose()
    {
        if (_gate.TryEnter(1, _attribute, out var lease))
        {
            lease.Dispose();
        }
    }
}
