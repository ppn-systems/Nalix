using System;
using System.Threading;
using System.Threading.Tasks;
using Nalix.Abstractions.Identity;
using Nalix.Abstractions.Networking.Sessions;
using Nalix.Abstractions.Primitives;
using Nalix.Network.Sessions;
using Xunit;

namespace Nalix.Network.Tests;

public sealed class InMemorySessionStoreTests : IDisposable
{
    private readonly InMemorySessionStore _store;

    public InMemorySessionStoreTests()
    {
        _store = new InMemorySessionStore();
    }

    [Fact]
    public async Task StoreAsync_And_ConsumeAsync_WorkCorrectly()
    {
        ulong token = (ulong)123456789UL;
        SessionSnapshot snapshot = new()
        {
            SessionToken = token,
            ExpiresAtUnixMilliseconds = long.MaxValue
        };
        SessionEntry entry = new(snapshot, (ulong)1UL);

        await _store.StoreAsync(entry);
        SessionEntry? retrieved = await _store.ConsumeAsync(token);

        Assert.NotNull(retrieved);
        Assert.Same(entry, retrieved);
        Assert.Equal(token, retrieved!.Snapshot.SessionToken);
    }

    [Fact]
    public async Task ConsumeAsync_WhenExpired_ReturnsNull()
    {
        ulong token = (ulong)999UL;
        SessionSnapshot snapshot = new()
        {
            SessionToken = token,
            ExpiresAtUnixMilliseconds = 0 // Already expired
        };
        SessionEntry entry = new(snapshot, (ulong)1UL);

        await _store.StoreAsync(entry);
        
        // This should trigger lazy expiration
        SessionEntry? consumed = await _store.ConsumeAsync(token);

        Assert.Null(consumed);
    }

    public void Dispose()
    {
    }
}















