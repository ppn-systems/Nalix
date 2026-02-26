using System;
using System.Collections.Generic;
using Microsoft.Extensions.Logging;
using Nalix.Framework.Configuration;
using Nalix.Logging;
using Nalix.Logging.Options;
using Xunit;

namespace Nalix.Framework.Tests.Logging;

public sealed class NLogixLoggingTests
{
    [Fact]
    public void Publish_WhenTargetThrows_IsolatesFailureAndInvokesErrorHandler()
    {
        using NLogixDistributor distributor = new();
        RecordingTarget healthy = new();
        ThrowingTarget faulty = new();

        _ = distributor.RegisterTarget(faulty);
        _ = distributor.RegisterTarget(healthy);

        distributor.Publish(DateTime.UtcNow, LogLevel.Error, new EventId(13, "boom"), "payload", null);

        _ = Assert.Single(healthy.Messages);
        _ = Assert.Single(faulty.Errors);
        Assert.Equal(1, distributor.TotalPublishErrors);
        Assert.Equal(1, distributor.TotalTargetInvocations);
    }

    [Fact]
    public void PublishAfterDispose_DoesNotThrow()
    {
        ResetLoggingConfiguration();

        NLogix logger = new(options =>
        {
            _ = options.SetMinimumLevel(LogLevel.Trace);
            _ = options.RegisterTarget(new RecordingTarget());
        });

        logger.Dispose();

        Exception? exception = Record.Exception(() => logger.Publish(LogLevel.Information, new EventId(1, "disposed"), "ignored"));

        Assert.Null(exception);
        ResetLoggingConfiguration();
    }

    [Theory]
    [InlineData(LogLevel.Trace, LogLevel.Trace, true)]
    [InlineData(LogLevel.Debug, LogLevel.Trace, true)]
    [InlineData(LogLevel.Warning, LogLevel.Error, false)]
    [InlineData(LogLevel.Information, LogLevel.Warning, false)]
    public void IsEnabled_RespectsConfiguredMinimumLevel(LogLevel levelToCheck, LogLevel minimumLevel, bool expected)
    {
        ResetLoggingConfiguration();
        using NLogix logger = new(options => _ = options.SetMinimumLevel(minimumLevel));

        Assert.Equal(expected, logger.IsEnabled(levelToCheck));
        ResetLoggingConfiguration();
    }

    private sealed class RecordingTarget : INLogixTarget
    {
        public List<string> Messages { get; } = [];

        public void Publish(DateTime timestampUtc, LogLevel logLevel, EventId eventId, string message, Exception? exception)
            => this.Messages.Add(message);
    }

    private sealed class ThrowingTarget : INLogixTarget, INLogixErrorHandler
    {
        public List<Exception> Errors { get; } = [];

        public void Publish(DateTime timestampUtc, LogLevel logLevel, EventId eventId, string message, Exception? exception)
            => throw new InvalidOperationException("target failure");

        public void HandleError(Exception exception, DateTime timestampUtc, LogLevel logLevel, EventId eventId, string message, Exception? originalException)
            => this.Errors.Add(exception);
    }

    private static void ResetLoggingConfiguration()
        => _ = ConfigurationManager.Instance.Remove<NLogixOptions>();
}
