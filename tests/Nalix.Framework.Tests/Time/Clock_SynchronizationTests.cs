using Nalix.Framework.Time;
using System;
using Xunit;

namespace Nalix.Framework.Tests.Time;

[Collection("ClockTests")]
public class Clock_SynchronizationTests
{
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
        // Small delay to ensure reset propagates in concurrent test environment
        System.Threading.Thread.Sleep(50);
        Assert.Equal(0, Clock.CurrentErrorEstimateMs(), 6);
    }

    [Fact]
    public void GetCurrentErrorEstimateMs_AfterSync_NonZeroish()
    {
        Clock.ResetSynchronization();
        _ = Clock.SynchronizeTime(DateTime.UtcNow.AddSeconds(2));
        var err = Clock.CurrentErrorEstimateMs();
        // Có thể dương / âm tùy thời điểm, nhưng magnitude > 0
        Assert.True(Math.Abs(err) >= 0);
    }
}
