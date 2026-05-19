using System;
using System.Net;
using BenchmarkDotNet.Attributes;
using Nalix.Abstractions.Networking;
using Nalix.Environment.Configuration;
using Nalix.Network.Options;
using Nalix.Network.RateLimiting;
using Nalix.Benchmarks.Shared;

namespace Nalix.Network.Benchmarks.RateLimiting;

[Config(typeof(NalixBenchmarkConfig))]
public class ConnectionGuardBenchmarks
{
    private ConnectionGuard _guard = null!;
    private IPEndPoint _allowedEndpoint = null!;
    private IPEndPoint _blacklistedEndpoint = null!;

    [GlobalSetup]
    public void Setup()
    {
        var quotaOptions = ConfigurationManager.Instance.Get<ConnectionQuotaOptions>();
        quotaOptions.MaxConnectionsPerIpAddress = 1000;
        quotaOptions.MaxConnectionsPerWindow = 10000;
        
        var guardOptions = ConfigurationManager.Instance.Get<ConnectionGuardOptions>();
        guardOptions.BlacklistedIpsString = "192.168.1.99";

        var storeOptions = ConfigurationManager.Instance.Get<ConnectionBanStoreOptions>();
        storeOptions.Enabled = false;

        _guard = new ConnectionGuard(quotaOptions);
        _allowedEndpoint = new IPEndPoint(IPAddress.Parse("127.0.0.1"), 8080);
        _blacklistedEndpoint = new IPEndPoint(IPAddress.Parse("192.168.1.99"), 8080);
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        _guard.Dispose();
    }

    [Benchmark]
    public bool TryAccept_Allowed()
    {
        return _guard.TryAccept(_allowedEndpoint);
    }

    [Benchmark]
    public bool TryAccept_Blacklisted()
    {
        return _guard.TryAccept(_blacklistedEndpoint);
    }
}
