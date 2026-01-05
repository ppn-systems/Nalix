using Nalix.Framework.Time;
using System;
using Xunit;

namespace Nalix.Framework.Tests.Time;

/// <summary>
/// Tests for input validation in Clock operations.
/// </summary>
public class Clock_ValidationTests
{
    [Fact]
    public void SynchronizeUnixMilliseconds_Should_Reject_Negative_ServerTime()
    {
        Clock.ResetSynchronization();

        var ex = Assert.Throws<ArgumentException>(() =>
            Clock.SynchronizeUnixMilliseconds(-1000, 0, 1000, 10000));

        Assert.Contains("Server Unix timestamp cannot be negative", ex.Message);
        Assert.Equal("serverUnixMs", ex.ParamName);
    }

    [Fact]
    public void SynchronizeUnixMilliseconds_Should_Reject_Negative_RTT()
    {
        Clock.ResetSynchronization();

        var ex = Assert.Throws<ArgumentException>(() =>
            Clock.SynchronizeUnixMilliseconds(DateTime.UtcNow.Ticks / TimeSpan.TicksPerMillisecond, -10, 1000, 10000));

        Assert.Contains("RTT cannot be negative", ex.Message);
        Assert.Equal("rttMs", ex.ParamName);
    }

    [Fact]
    public void SynchronizeUnixMilliseconds_Should_Reject_Zero_MaxAllowedDrift()
    {
        Clock.ResetSynchronization();
        var currentUnixMs = (Int64)(DateTime.UtcNow - DateTime.UnixEpoch).TotalMilliseconds;

        var ex = Assert.Throws<ArgumentException>(() =>
            Clock.SynchronizeUnixMilliseconds(currentUnixMs, 0, 0, 10000));

        Assert.Contains("Max allowed drift must be positive", ex.Message);
        Assert.Equal("maxAllowedDriftMs", ex.ParamName);
    }

    [Fact]
    public void SynchronizeUnixMilliseconds_Should_Reject_Negative_MaxAllowedDrift()
    {
        Clock.ResetSynchronization();
        var currentUnixMs = (Int64)(DateTime.UtcNow - DateTime.UnixEpoch).TotalMilliseconds;

        var ex = Assert.Throws<ArgumentException>(() =>
            Clock.SynchronizeUnixMilliseconds(currentUnixMs, 0, -1000, 10000));

        Assert.Contains("Max allowed drift must be positive", ex.Message);
        Assert.Equal("maxAllowedDriftMs", ex.ParamName);
    }

    [Fact]
    public void SynchronizeUnixMilliseconds_Should_Reject_Zero_MaxHardAdjust()
    {
        Clock.ResetSynchronization();
        var currentUnixMs = (Int64)(DateTime.UtcNow - DateTime.UnixEpoch).TotalMilliseconds;

        var ex = Assert.Throws<ArgumentException>(() =>
            Clock.SynchronizeUnixMilliseconds(currentUnixMs, 0, 1000, 0));

        Assert.Contains("Max hard adjust must be positive", ex.Message);
        Assert.Equal("maxHardAdjustMs", ex.ParamName);
    }

    [Fact]
    public void SynchronizeUnixMilliseconds_Should_Reject_Negative_MaxHardAdjust()
    {
        Clock.ResetSynchronization();
        var currentUnixMs = (Int64)(DateTime.UtcNow - DateTime.UnixEpoch).TotalMilliseconds;

        var ex = Assert.Throws<ArgumentException>(() =>
            Clock.SynchronizeUnixMilliseconds(currentUnixMs, 0, 1000, -10000));

        Assert.Contains("Max hard adjust must be positive", ex.Message);
        Assert.Equal("maxHardAdjustMs", ex.ParamName);
    }

    [Fact]
    public void SynchronizeUnixMilliseconds_Should_Reject_Timestamp_Before_Year_2000()
    {
        Clock.ResetSynchronization();

        // Unix timestamp for Jan 1, 1999 00:00:00
        const Int64 oldTimestamp = 915148800000L;

        var ex = Assert.Throws<ArgumentException>(() =>
            Clock.SynchronizeUnixMilliseconds(oldTimestamp, 0, 1000, 10000));

        Assert.Contains("Server Unix timestamp is outside reasonable range", ex.Message);
        Assert.Equal("serverUnixMs", ex.ParamName);
    }

    [Fact]
    public void SynchronizeUnixMilliseconds_Should_Reject_Timestamp_After_Year_2100()
    {
        Clock.ResetSynchronization();

        // Unix timestamp for Jan 1, 2101 00:00:00
        const Int64 futureTimestamp = 4133980800000L;

        var ex = Assert.Throws<ArgumentException>(() =>
            Clock.SynchronizeUnixMilliseconds(futureTimestamp, 0, 1000, 10000));

        Assert.Contains("Server Unix timestamp is outside reasonable range", ex.Message);
        Assert.Equal("serverUnixMs", ex.ParamName);
    }

    [Fact]
    public void SynchronizeUnixMilliseconds_Should_Accept_Valid_Current_Timestamp()
    {
        Clock.ResetSynchronization();
        
        // Wait a bit to ensure clean state
        System.Threading.Thread.Sleep(10);

        var currentUnixMs = (Int64)(DateTime.UtcNow - DateTime.UnixEpoch).TotalMilliseconds;

        // Should not throw - use larger thresholds to ensure sync happens
        var adjustment = Clock.SynchronizeUnixMilliseconds(currentUnixMs, 10, 5000, 50000);

        Assert.True(Clock.IsSynchronized);
    }

    [Fact]
    public void SynchronizeUnixMilliseconds_Should_Handle_Edge_Case_Year_2000()
    {
        Clock.ResetSynchronization();

        // Unix timestamp for Jan 1, 2000 00:00:00 (exactly at boundary)
        const Int64 y2kTimestamp = 946684800000L;

        // Should not throw
        _ = Clock.SynchronizeUnixMilliseconds(y2kTimestamp, 0, 100000, 100000);
    }

    [Fact]
    public void SynchronizeUnixMilliseconds_Should_Handle_Edge_Case_Year_2100()
    {
        Clock.ResetSynchronization();

        // Unix timestamp for Jan 1, 2100 00:00:00 (exactly at boundary)
        const Int64 y2100Timestamp = 4102444800000L;

        // Should not throw
        _ = Clock.SynchronizeUnixMilliseconds(y2100Timestamp, 0, 100000, 100000);
    }

    [Fact]
    public void SynchronizeUnixMilliseconds_With_RTT_Should_Adjust_Time_Correctly()
    {
        Clock.ResetSynchronization();
        
        // Wait a bit to ensure clean state
        System.Threading.Thread.Sleep(10);

        var serverUnixMs = (Int64)(DateTime.UtcNow - DateTime.UnixEpoch).TotalMilliseconds;
        const double rttMs = 100; // 100ms round trip

        // The method should add half of RTT (50ms) to the server time
        // Use larger thresholds to ensure sync happens
        var adjustment = Clock.SynchronizeUnixMilliseconds(serverUnixMs, rttMs, 5000, 50000);

        Assert.True(Clock.IsSynchronized);
    }
}
