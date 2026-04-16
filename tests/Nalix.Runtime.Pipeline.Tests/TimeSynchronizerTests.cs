using System;
using System.Threading;
using System.Threading.Tasks;
using Nalix.Runtime.Timekeeping;
using Xunit;

namespace Nalix.Runtime.Tests;

public sealed class TimeSynchronizerTests : IDisposable
{
    private readonly TimeSynchronizer _synchronizer;

    public TimeSynchronizerTests()
    {
        _synchronizer = new TimeSynchronizer();
    }

    [Fact]
    public void IsRunning_Initially_IsFalse()
    {
        Assert.False(_synchronizer.IsRunning);
    }

    [Fact]
    public void Activate_StartsTheLoop()
    {
        _synchronizer.Activate();
        Assert.True(_synchronizer.IsRunning);
        Assert.True(_synchronizer.IsTimeSyncEnabled);
    }

    [Fact]
    public void Deactivate_StopsTheLoop()
    {
        _synchronizer.Activate();
        _synchronizer.Deactivate();
        Assert.False(_synchronizer.IsRunning);
        Assert.False(_synchronizer.IsTimeSyncEnabled);
    }

    [Fact]
    public async Task TimeSynchronized_RaisedPeriodically()
    {
        _synchronizer.Period = TimeSpan.FromMilliseconds(50);
        int tickCount = 0;
        _synchronizer.TimeSynchronized += _ => Interlocked.Increment(ref tickCount);

        _synchronizer.Activate();
        
        // Wait for a few ticks
        await Task.Delay(250);
        
        _synchronizer.Deactivate();

        Assert.True(tickCount >= 3, $"Expected at least 3 ticks, got {tickCount}");
    }

    [Fact]
    public void Period_ChangeWhileRunning_RestartsLoop()
    {
        _synchronizer.Activate();
        _synchronizer.Period = TimeSpan.FromMilliseconds(100);
        
        Assert.True(_synchronizer.IsRunning);
        Assert.Equal(TimeSpan.FromMilliseconds(100), _synchronizer.Period);
    }

    [Fact]
    public void Dispose_DisposesCorrectly()
    {
        _synchronizer.Activate();
        _synchronizer.Dispose();
        
        Assert.False(_synchronizer.IsRunning);
    }

    public void Dispose()
    {
        _synchronizer.Dispose();
    }
}
