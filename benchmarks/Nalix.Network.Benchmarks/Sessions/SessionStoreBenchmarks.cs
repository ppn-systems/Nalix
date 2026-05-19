using System;
using System.Threading.Tasks;
using BenchmarkDotNet.Attributes;
using Nalix.Abstractions.Identity;
using Nalix.Abstractions.Networking.Sessions;
using Nalix.Abstractions.Security;
using Nalix.Network.Sessions;
using Nalix.Benchmarks.Shared;

namespace Nalix.Network.Benchmarks.Sessions;

[Config(typeof(NalixBenchmarkConfig))]
public class SessionStoreBenchmarks
{
    private InMemorySessionStore _store = null!;
    private SessionEntry _entry = null!;
    private ulong _token;

    [GlobalSetup]
    public void Setup()
    {
        _store = new InMemorySessionStore();
        
        var snapshot = new SessionSnapshot
        {
            SessionToken = 1234567890UL,
            CreatedAtUnixMilliseconds = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
            ExpiresAtUnixMilliseconds = DateTimeOffset.UtcNow.AddHours(1).ToUnixTimeMilliseconds(),
            Algorithm = CipherSuiteType.Chacha20Poly1305,
            Level = PermissionLevel.USER
        };

        _entry = new SessionEntry(snapshot, 999UL);
        _token = snapshot.SessionToken;
    }

    [Benchmark]
    public async Task StoreAndConsume()
    {
        await _store.StoreAsync(_entry);
        await _store.ConsumeAsync(_token);
    }
}
