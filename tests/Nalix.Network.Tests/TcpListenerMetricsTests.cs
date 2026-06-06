// Copyright (c) 2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System;
using System.IO;
using System.Text;
using System.Text.Json;
using FluentAssertions;
using Nalix.Abstractions.Networking;
using Nalix.Network.Listeners.Tcp;
using NSubstitute;
using Xunit;

namespace Nalix.Network.Tests;

public sealed class TcpListenerMetricsTests
{
    private sealed class StubTcpListener : TcpListenerBase
    {
        public StubTcpListener(IProtocol protocol, IConnectionHub hub) : base(12345, protocol, hub) { }
    }

    [Fact]
    public void LMetrics_Properties_ShouldReflectIncrements()
    {
        var protocol = Substitute.For<IProtocol>();
        var hub = Substitute.For<IConnectionHub>();
        using var listener = new StubTcpListener(protocol, hub);

        listener.Metrics.TotalAccepted.Should().Be(0);
        listener.Metrics.TotalRejected.Should().Be(0);
        listener.Metrics.TotalErrors.Should().Be(0);
        listener.Metrics.TotalProxyProtocolErrors.Should().Be(0);
        listener.Metrics.TotalQueueFullRejections.Should().Be(0);
        listener.Metrics.TotalLimiterRejections.Should().Be(0);

        // Record metrics
        listener.Metrics.RECORD_ACCEPTED();
        listener.Metrics.RECORD_PROXY_ERROR();
        listener.Metrics.RECORD_QUEUE_FULL_REJECTION();
        listener.Metrics.RECORD_LIMITER_REJECTION();
        listener.Metrics.RECORD_ERROR();

        // Check values
        listener.Metrics.TotalAccepted.Should().Be(1);
        listener.Metrics.TotalProxyProtocolErrors.Should().Be(1);
        listener.Metrics.TotalQueueFullRejections.Should().Be(1);
        listener.Metrics.TotalLimiterRejections.Should().Be(1);
        listener.Metrics.TotalErrors.Should().Be(1);

        // TotalRejected should be the sum of _totalRejected (0) + TotalQueueFullRejections (1) + TotalLimiterRejections (1) = 2
        listener.Metrics.TotalRejected.Should().Be(2);
    }

    [Fact]
    public void GenerateReport_ShouldContainNewMetrics()
    {
        var protocol = Substitute.For<IProtocol>();
        var hub = Substitute.For<IConnectionHub>();
        using var listener = new StubTcpListener(protocol, hub);

        listener.Metrics.RECORD_PROXY_ERROR();
        listener.Metrics.RECORD_QUEUE_FULL_REJECTION();
        listener.Metrics.RECORD_LIMITER_REJECTION();

        string report = listener.GenerateReport();

        report.Should().Contain("Queue Full      : 1");
        report.Should().Contain("Limiter/Guard   : 1");
        report.Should().Contain("Proxy Protocol Errs : 1");
        report.Should().Contain("Accept Queue Depth  : 0");
    }

    [Fact]
    public void WriteReportData_ShouldSerializeNewMetrics()
    {
        var protocol = Substitute.For<IProtocol>();
        var hub = Substitute.For<IConnectionHub>();
        using var listener = new StubTcpListener(protocol, hub);

        listener.Metrics.RECORD_PROXY_ERROR();
        listener.Metrics.RECORD_QUEUE_FULL_REJECTION();
        listener.Metrics.RECORD_LIMITER_REJECTION();

        using var ms = new MemoryStream();
        using (var writer = new Utf8JsonWriter(ms))
        {
            listener.WriteReportData(writer);
        }

        string json = Encoding.UTF8.GetString(ms.ToArray());
        using var doc = JsonDocument.Parse(json);
        var metricsElement = doc.RootElement.GetProperty("Metrics");

        metricsElement.GetProperty("QueueFullRejections").GetInt64().Should().Be(1);
        metricsElement.GetProperty("LimiterRejections").GetInt64().Should().Be(1);
        metricsElement.GetProperty("ProxyProtocolErrors").GetInt64().Should().Be(1);
        metricsElement.GetProperty("AcceptQueueDepth").GetInt64().Should().Be(0);
    }
}
