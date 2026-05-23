using System;
using System.Threading;
using System.Threading.Tasks;
using Nalix.Abstractions;
using Nalix.Abstractions.Identity;
using Nalix.Abstractions.Networking;
using Nalix.Abstractions.Networking.Sessions;
using Nalix.Abstractions.Primitives;
using Nalix.Runtime.Sessions;
using NSubstitute;
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

    [Fact]
    public async Task InMemorySessionStore_ReportContainsCorrectMetrics()
    {
        var store = new InMemorySessionStore();
        
        // Assert initial state of report
        string initialReport = store.GenerateReport();
        Assert.Contains("Active Sessions : 0", initialReport);
        Assert.Contains("Total Stored    : 0", initialReport);
        Assert.Contains("Total Consumed  : 0", initialReport);
        Assert.Contains("Total Expired   : 0", initialReport);

        // Store an entry
        ulong token = 123456789UL;
        SessionSnapshot snapshot = new()
        {
            SessionToken = token,
            ExpiresAtUnixMilliseconds = long.MaxValue
        };
        SessionEntry entry = new(snapshot, 1UL);
        await store.StoreAsync(entry);

        string reportAfterStore = store.GenerateReport();
        Assert.Contains("Active Sessions : 1", reportAfterStore);
        Assert.Contains("Total Stored    : 1", reportAfterStore);

        // Consume entry
        SessionEntry? consumed = await store.ConsumeAsync(token);
        Assert.NotNull(consumed);

        string reportAfterConsume = store.GenerateReport();
        Assert.Contains("Active Sessions : 0", reportAfterConsume);
        Assert.Contains("Total Consumed  : 1", reportAfterConsume);

        // Expired consume
        ulong expiredToken = 999UL;
        SessionSnapshot expiredSnapshot = new()
        {
            SessionToken = expiredToken,
            ExpiresAtUnixMilliseconds = 0 // Already expired
        };
        SessionEntry expiredEntry = new(expiredSnapshot, 1UL);
        await store.StoreAsync(expiredEntry);

        SessionEntry? expiredConsumed = await store.ConsumeAsync(expiredToken);
        Assert.Null(expiredConsumed);

        string reportAfterExpired = store.GenerateReport();
        Assert.Contains("Total Expired   : 1", reportAfterExpired);
        
        // JSON report testing
        using var ms = new System.IO.MemoryStream();
        using (var writer = new System.Text.Json.Utf8JsonWriter(ms))
        {
            store.WriteReportData(writer);
        }
        string json = System.Text.Encoding.UTF8.GetString(ms.ToArray());
        Assert.Contains("\"Type\":\"InMemorySessionStore\"", json);
        Assert.Contains("\"ActiveSessions\":0", json);
        Assert.Contains("\"TotalStored\":2", json);
        Assert.Contains("\"TotalConsumed\":1", json);
        Assert.Contains("\"TotalExpired\":1", json);
    }

    [Fact]
    public async Task SessionService_ReportContainsCorrectMetrics()
    {
        var store = new InMemorySessionStore();
        var service = new SessionService(store: store);

        // Initial state check
        string initialReport = service.GenerateReport();
        Assert.Contains("Total Stores Attempted  : 0", initialReport);
        Assert.Contains("Total Stores Succeeded  : 0", initialReport);
        Assert.Contains("Total Stores Failed     : 0", initialReport);
        Assert.Contains("Total Stores Rejected   : 0", initialReport);
        Assert.Contains("Total Consumes Attempted: 0", initialReport);
        Assert.Contains("Total Consumes Succeeded: 0", initialReport);
        Assert.Contains("Total Consumes Failed   : 0", initialReport);

        // Store with connection disposed policy violation (rejection)
        var connection = Substitute.For<IConnection>();
        connection.IsDisposed.Returns(true);
        await service.SaveSessionAsync(connection);

        string reportAfterDisposed = service.GenerateReport();
        Assert.Contains("Total Stores Attempted  : 1", reportAfterDisposed);
        Assert.Contains("Total Stores Rejected   : 1", reportAfterDisposed);

        // JSON report testing
        using var ms = new System.IO.MemoryStream();
        using (var writer = new System.Text.Json.Utf8JsonWriter(ms))
        {
            service.WriteReportData(writer);
        }
        string json = System.Text.Encoding.UTF8.GetString(ms.ToArray());
        Assert.Contains("\"Type\":\"SessionService\"", json);
        Assert.Contains("\"TotalStoresAttempted\":1", json);
        Assert.Contains("\"TotalStoresRejectedByPolicy\":1", json);
        Assert.Contains("\"Store\":", json);
        Assert.Contains("\"Type\":\"InMemorySessionStore\"", json);
    }

    public void Dispose()
    {
    }
}















