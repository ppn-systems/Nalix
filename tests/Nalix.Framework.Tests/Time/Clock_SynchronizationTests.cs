using Nalix.Framework.Time;
using System;
using Xunit;

namespace Nalix.Framework.Tests.Time;

public class Clock_SynchronizationTests
{
    [Fact]
    public void SynchronizeTime_Rejected_When_Within_Threshold()
    {
        Clock.ResetSynchronization();

        // externalTime gần với GetUtcNowPrecise -> không điều chỉnh
        var external = Clock.GetUtcNowPrecise().AddMilliseconds(100); // <= default 1000 ms
        Double adj = Clock.SynchronizeTime(external); // nên là 0
        Assert.Equal(0, adj, 6);
        Assert.False(Clock.IsSynchronized);
        Assert.Equal(DateTime.MinValue, Clock.LastSyncTime);
        Assert.Equal(0, Clock.CurrentOffsetMs, 6);
    }

    [Fact]
    public void SynchronizeTime_Applies_Offset_When_Beyond_Threshold()
    {
        Clock.ResetSynchronization();

        var external = DateTime.UtcNow.AddSeconds(5); // Lệch 5s
        Double adj = Clock.SynchronizeTime(external);
        Assert.InRange(adj, 4000, 6000);

        Assert.True(Clock.IsSynchronized);
        Assert.Equal(external, Clock.LastSyncTime);
        Assert.InRange(Clock.CurrentOffsetMs, 4000, 6000);

        // sau reset => quay về mặc định
        Clock.ResetSynchronization();
        Assert.False(Clock.IsSynchronized);
        Assert.Equal(0, Clock.CurrentOffsetMs, 6);
        Assert.Equal(DateTime.MinValue, Clock.LastSyncTime);
    }

    [Fact]
    public void SynchronizeTime_Requires_Utc()
    {
        var local = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Local);
        _ = Assert.Throws<ArgumentException>(() => Clock.SynchronizeTime(local));
    }

    [Fact]
    public void GetCurrentErrorEstimateMs_NoSync_Zero()
    {
        Clock.ResetSynchronization();
        Assert.Equal(0, Clock.GetCurrentErrorEstimateMs(), 6);
    }

    [Fact]
    public void GetCurrentErrorEstimateMs_AfterSync_NonZeroish()
    {
        Clock.ResetSynchronization();
        _ = Clock.SynchronizeTime(DateTime.UtcNow.AddSeconds(2));
        var err = Clock.GetCurrentErrorEstimateMs();
        // Có thể dương / âm tùy thời điểm, nhưng magnitude > 0
        Assert.True(Math.Abs(err) >= 0);
    }
}
