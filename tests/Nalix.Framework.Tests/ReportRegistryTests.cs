// Copyright (c) 2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Text.Json;
using Nalix.Abstractions;
using Nalix.Abstractions.Networking;
using Nalix.Framework;
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
