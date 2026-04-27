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
    public async Task StoreAsync_And_RetrieveAsync_WorkCorrectly()
    {
        ulong token = (ulong)123456789UL;
        SessionSnapshot snapshot = new()
        {
            SessionToken = token,
            ExpiresAtUnixMilliseconds = long.MaxValue
        };
        SessionEntry entry = new(snapshot, (ulong)1UL);

        await _store.StoreAsync(entry);
        SessionEntry? retrieved = await _store.RetrieveAsync(token);

        Assert.NotNull(retrieved);
        Assert.Same(entry, retrieved);
        Assert.Equal(token, retrieved.Snapshot.SessionToken);
    }

    [Fact]
    public async Task RetrieveAsync_WhenExpired_ReturnsNullAndRemoves()
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
        SessionEntry? retrieved = await _store.RetrieveAsync(token);

        Assert.Null(retrieved);
        
        // Verify it was removed (double check)
        SessionEntry? secondAttempt = await _store.RetrieveAsync(token);
        Assert.Null(secondAttempt);
    }

    [Fact]
    public async Task ConsumeAsync_RemovesEntry()
    {
        ulong token = (ulong)456UL;
        SessionSnapshot snapshot = new()
        {
            SessionToken = token,
            ExpiresAtUnixMilliseconds = long.MaxValue
        };
        SessionEntry entry = new(snapshot, (ulong)1UL);

        await _store.StoreAsync(entry);
        
        SessionEntry? consumed = await _store.ConsumeAsync(token);
        Assert.NotNull(consumed);
        Assert.Same(entry, consumed);

        SessionEntry? retrieved = await _store.RetrieveAsync(token);
        Assert.Null(retrieved);
    }

    [Fact]
    public async Task RemoveAsync_WorksCorrectly()
    {
        ulong token = (ulong)789UL;
        SessionSnapshot snapshot = new()
        {
            SessionToken = token,
            ExpiresAtUnixMilliseconds = long.MaxValue
        };
        SessionEntry entry = new(snapshot, (ulong)1UL);

        await _store.StoreAsync(entry);
        await _store.RemoveAsync(token);

        SessionEntry? retrieved = await _store.RetrieveAsync(token);
        Assert.Null(retrieved);
    }

    public void Dispose()
    {
        _store.Dispose();
    }
}














