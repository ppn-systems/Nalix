// Copyright (c) 2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Text.Json;
using Nalix.Abstractions;
using Nalix.Abstractions.Concurrency;
using Nalix.Abstractions.Diagnostics;
using Nalix.Abstractions.Networking;
using Nalix.Framework;
using Nalix.Framework.Injection;
using Xunit;

namespace Nalix.Framework.Tests;

/// <summary>
/// Verifies the correctness of the <see cref="ReportRegistry"/> class.
/// </summary>
public sealed class ReportRegistryTests : IDisposable
{
    private readonly ReportRegistry _registry = ReportRegistry.Instance;

    public ReportRegistryTests()
    {
        _registry.Clear();
    }

    public void Dispose()
    {
        _registry.Clear();
    }

    [Fact(DisplayName = "Register and Get should successfully map key and type to reportable instance")]
    public void RegisterAndGetShouldSuccessfullyMapKeyAndType()
    {
        var fake = new FakeReportable("telemetry data");
        _registry.Register<IReportable>(NetworkTransport.TCP, fake);

        var resolved = _registry.Get<IReportable>(NetworkTransport.TCP);
        Assert.Same(fake, resolved);

        var resolvedUdp = _registry.Get<IReportable>(NetworkTransport.UDP);
        Assert.Null(resolvedUdp);
    }

    [Fact(DisplayName = "Unregister should successfully remove registered instance")]
    public void UnregisterShouldRemoveInstance()
    {
        var fake = new FakeReportable("telemetry data");
        _registry.Register<IReportable>(NetworkTransport.TCP, fake);

        var removed = _registry.Unregister<IReportable>(NetworkTransport.TCP);
        Assert.True(removed);

        var resolved = _registry.Get<IReportable>(NetworkTransport.TCP);
        Assert.Null(resolved);
    }

    [Fact(DisplayName = "Clear should empty all registrations")]
    public void ClearShouldEmptyAllRegistrations()
    {
        var fake1 = new FakeReportable("1");
        var fake2 = new FakeReportable("2");

        _registry.Register<IReportable>(NetworkTransport.TCP, fake1);
        _registry.Register<IReportable>(NetworkTransport.UDP, fake2);

        _registry.Clear();

        Assert.Null(_registry.Get<IReportable>(NetworkTransport.TCP));
        Assert.Null(_registry.Get<IReportable>(NetworkTransport.UDP));
    }

    [Fact(DisplayName = "WriteReportData and GenerateReport should format telemetry successfully")]
    public void WriteReportDataAndGenerateReportShouldFormatTelemetry()
    {
        var fake = new FakeReportable("metrics content");
        _registry.Register<IReportable>(NetworkTransport.TCP, fake);

        string textReport = _registry.GenerateReport();
        Assert.Contains("NetworkTransport.TCP", textReport);
        Assert.Contains(nameof(FakeReportable), textReport);

        using var ms = new System.IO.MemoryStream();
        using (var writer = new Utf8JsonWriter(ms))
        {
            _registry.WriteReportData(writer);
        }
        string jsonReport = System.Text.Encoding.UTF8.GetString(ms.ToArray());
        Assert.Contains("TCP", jsonReport);
    }

    [Fact(DisplayName = "Registering task manager in InstanceManager should automatically index it in ReportRegistry")]
    public void RegisteringITaskManagerAutoIndexesInReportRegistry()
    {
        var manager = new InstanceManager();
        var taskManager = new Nalix.Framework.Tasks.TaskManager();

        try
        {
            manager.Register<ITaskManager>(taskManager);

            var resolved = _registry.Get<ITaskManager>(CoreTelemetryTarget.Tasks);
            Assert.Same(taskManager, resolved);
        }
        finally
        {
            taskManager.Dispose();
            manager.Clear(dispose: true);
        }
    }

    [Fact(DisplayName = "Registering different rate limiters in InstanceManager should automatically index them without collision")]
    public void RegisteringRateLimitersAutoIndexesWithoutCollision()
    {
        var manager = new InstanceManager();
        var gateMock = new ConcurrencyGate();
        var policyMock = new PolicyRateLimiter();
        var tokenMock = new TokenBucketLimiter();

        try
        {
            manager.Register<IReportable>(gateMock);
            manager.Register<IReportable>(policyMock);
            manager.Register<IReportable>(tokenMock);

            var resolvedGate = _registry.Get<IReportable>(CoreTelemetryTarget.ConcurrencyGate);
            var resolvedPolicy = _registry.Get<IReportable>(CoreTelemetryTarget.PolicyRateLimiter);
            var resolvedToken = _registry.Get<IReportable>(CoreTelemetryTarget.TokenBucketLimiter);

            Assert.Same(gateMock, resolvedGate);
            Assert.Same(policyMock, resolvedPolicy);
            Assert.Same(tokenMock, resolvedToken);
        }
        finally
        {
            manager.Clear(dispose: true);
        }
    }

    private sealed class ConcurrencyGate : IReportable
    {
        public string GenerateReport() => nameof(ConcurrencyGate);
        public void WriteReportData(Utf8JsonWriter writer) {}
    }

    private sealed class PolicyRateLimiter : IReportable
    {
        public string GenerateReport() => nameof(PolicyRateLimiter);
        public void WriteReportData(Utf8JsonWriter writer) {}
    }

    private sealed class TokenBucketLimiter : IReportable
    {
        public string GenerateReport() => nameof(TokenBucketLimiter);
        public void WriteReportData(Utf8JsonWriter writer) {}
    }

    private sealed class FakeReportable(string data) : IReportable
    {
        public string Data { get; } = data;

        public void WriteReportData(Utf8JsonWriter writer)
        {
            writer.WriteStartObject();
            writer.WriteString("Data", Data);
            writer.WriteEndObject();
        }

        public string GenerateReport() => $"Data: {Data}";
    }
}
