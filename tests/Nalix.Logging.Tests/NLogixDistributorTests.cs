using System;
using Microsoft.Extensions.Logging;
using Nalix.Logging;
using NSubstitute;
using Xunit;

namespace Nalix.Logging.Tests;

public sealed class NLogixDistributorTests
{
    [Fact]
    public void RegisterTarget_IncrementsTargetCount()
    {
        var distributor = new NLogixDistributor();
        var target = Substitute.For<INLogixTarget>();

        distributor.RegisterTarget(target);

        Assert.Contains("Active Targets: 1", distributor.ToString());
    }

    [Fact]
    public void UnregisterTarget_DecrementsTargetCount()
    {
        var distributor = new NLogixDistributor();
        var target = Substitute.For<INLogixTarget>();

        distributor.RegisterTarget(target);
        distributor.UnregisterTarget(target);

        Assert.Contains("Active Targets: 0", distributor.ToString());
    }

    [Fact]
    public void Publish_CallsAllRegisteredTargets()
    {
        var distributor = new NLogixDistributor();
        var target1 = Substitute.For<INLogixTarget>();
        var target2 = Substitute.For<INLogixTarget>();

        distributor.RegisterTarget(target1);
        distributor.RegisterTarget(target2);

        var timestamp = DateTime.UtcNow;
        var eventId = new EventId(1);
        var message = "Test message";

        distributor.Publish(timestamp, LogLevel.Information, eventId, message, null);

        target1.Received(1).Publish(timestamp, LogLevel.Information, eventId, message, null);
        target2.Received(1).Publish(timestamp, LogLevel.Information, eventId, message, null);
        Assert.Equal(1, distributor.TotalEntriesPublished);
        Assert.Equal(2, distributor.TotalTargetInvocations);
    }

    [Fact]
    public void Publish_WhenTargetThrows_IncrementsErrorCountAndContinues()
    {
        var distributor = new NLogixDistributor();
        var target1 = Substitute.For<INLogixTarget>();
        var target2 = Substitute.For<INLogixTarget>();

        target1.When(x => x.Publish(Arg.Any<DateTime>(), Arg.Any<LogLevel>(), Arg.Any<EventId>(), Arg.Any<string>(), Arg.Any<Exception>()))
               .Do(x => throw new InvalidOperationException("target failed"));

        distributor.RegisterTarget(target1);
        distributor.RegisterTarget(target2);

        distributor.Publish(DateTime.UtcNow, LogLevel.Information, new EventId(1), "msg", null);

        target1.Received(1).Publish(Arg.Any<DateTime>(), Arg.Any<LogLevel>(), Arg.Any<EventId>(), Arg.Any<string>(), Arg.Any<Exception>());
        target2.Received(1).Publish(Arg.Any<DateTime>(), Arg.Any<LogLevel>(), Arg.Any<EventId>(), Arg.Any<string>(), Arg.Any<Exception>());
        Assert.Equal(1, distributor.TotalPublishErrors);
    }

    [Fact]
    public void Dispose_DisposesAllTargets()
    {
        var distributor = new NLogixDistributor();
        var target = Substitute.For<INLogixTarget, IDisposable>();

        distributor.RegisterTarget(target);
        distributor.Dispose();

        ((IDisposable)target).Received(1).Dispose();
        Assert.Contains("Disposed: True", distributor.ToString());
    }
}
