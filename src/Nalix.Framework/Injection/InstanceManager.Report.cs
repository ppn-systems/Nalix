// Copyright (c) 2025-2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using Nalix.Abstractions;
using Nalix.Abstractions.Concurrency;
using Nalix.Abstractions.Diagnostics;
using Nalix.Abstractions.Networking;

namespace Nalix.Framework.Injection;

public sealed partial class InstanceManager
{
    #region IReportable

    /// <summary>
    /// Generates a human-readable report of all cached instances.
    /// </summary>
    public string GenerateReport()
    {
        StringBuilder sb = new(2048);
        HashSet<RuntimeTypeHandle> activatorTargets = this.BUILD_ACTIVATOR_TARGETS();

        _ = sb.AppendLine(CultureInfo.InvariantCulture, $"[{DateTime.UtcNow:yyyy-MM-dd HH:mm:ss}] InstanceManager Status:");
        _ = sb.AppendLine(CultureInfo.InvariantCulture, $"CachedInstanceCount: {this.CachedInstanceCount}");
        _ = sb.AppendLine(CultureInfo.InvariantCulture, $"InstanceCreationCount: {Volatile.Read(ref _instanceCreationCount)}");
        _ = sb.AppendLine(CultureInfo.InvariantCulture, $"InstanceCacheHitCount: {Volatile.Read(ref _instanceCacheHitCount)}");
        _ = sb.AppendLine();
        _ = sb.AppendLine("Instances:");
        _ = sb.AppendLine("---------------------------------------------------------------------------");
        _ = sb.AppendLine("Type                                          | Disposable | Source        ");
        _ = sb.AppendLine("---------------------------------------------------------------------------");

        foreach (KeyValuePair<RuntimeTypeHandle, object> kvp in _instanceCache)
        {
            Type type = Type.GetTypeFromHandle(kvp.Key)!;
            object instance = kvp.Value;
            string typeName = type.FullName ?? type.Name;
            if (typeName.Length > 32)
            {
                typeName = "..." + typeName[^29..];
            }

            bool isDisposable = instance is IDisposable;
            string source = activatorTargets.Contains(type.TypeHandle) ? "ActivatorCache" : "ManualRegister";

            _ = sb.AppendLine(CultureInfo.InvariantCulture, $"{typeName.PadRight(45)} | {(isDisposable ? "Yes" : "No "),10} | {source}");
        }

        foreach (KeyValuePair<ActivatorKey, object> kvp in _signatureInstanceCache)
        {
            Type type = kvp.Value.GetType();
            string typeName = type.FullName ?? type.Name;
            if (typeName.Length > 32)
            {
                typeName = "..." + typeName[^29..];
            }

            bool isDisposable = kvp.Value is IDisposable;
            _ = sb.AppendLine(CultureInfo.InvariantCulture, $"{typeName.PadRight(45)} | {(isDisposable ? "Yes" : "No "),10} | SignatureCache");
        }

        _ = sb.AppendLine("---------------------------------------------------------------------------");
        return sb.ToString();
    }

    /// <inheritdoc/>
    public void WriteReportData(System.Text.Json.Utf8JsonWriter writer)
    {
        ArgumentNullException.ThrowIfNull(writer);

        HashSet<RuntimeTypeHandle> activatorTargets = this.BUILD_ACTIVATOR_TARGETS();

        writer.WriteStartObject();
        writer.WriteString("UtcNow", DateTime.UtcNow);
        writer.WriteNumber(nameof(this.CachedInstanceCount), this.CachedInstanceCount);
        writer.WriteNumber("InstanceCreationCount", Volatile.Read(ref _instanceCreationCount));
        writer.WriteNumber("InstanceCacheHitCount", Volatile.Read(ref _instanceCacheHitCount));

        writer.WriteNumber("SignatureInstanceCount", _signatureInstanceCache.Count);
        writer.WriteNumber("ActivatorFactoryCount", _activatorCache.Count);
        writer.WriteNumber("DisposableCount", _disposables.Count);
        writer.WriteNumber("SlotsInvalidated", Volatile.Read(ref s_slotsInvalidated));

        writer.WriteNumber("TotalGetOrCreateCalls",
        Volatile.Read(ref _instanceCreationCount) + Volatile.Read(ref _instanceCacheHitCount));

        writer.WriteNumber("HitRatePermille", CalculateHitRatePermille());

        writer.WriteStartArray("Instances");

        foreach (KeyValuePair<RuntimeTypeHandle, object> kvp in _instanceCache)
        {
            Type type = Type.GetTypeFromHandle(kvp.Key)!;
            object instance = kvp.Value;
            string typeName = type.FullName ?? type.Name;
            if (typeName.Length > 32)
            {
                typeName = "..." + typeName[^29..];
            }

            bool isDisposable = instance is IDisposable;
            string source = activatorTargets.Contains(type.TypeHandle) ? "ActivatorCache" : "ManualRegister";

            writer.WriteStartObject();
            writer.WriteString("Type", typeName);
            writer.WriteBoolean("IsDisposable", isDisposable);
            writer.WriteString("Source", source);
            writer.WriteEndObject();
        }

        foreach (KeyValuePair<ActivatorKey, object> kvp in _signatureInstanceCache)
        {
            Type type = kvp.Value.GetType();
            string typeName = type.FullName ?? type.Name;

            writer.WriteStartObject();
            writer.WriteString("Type", typeName);
            writer.WriteBoolean("IsDisposable", kvp.Value is IDisposable);
            writer.WriteString("Source", "SignatureCache");
            writer.WriteEndObject();
        }

        writer.WriteEndArray();

        writer.WriteEndObject();

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        int CalculateHitRatePermille()
        {
            long creations = Volatile.Read(ref _instanceCreationCount);
            long hits = Volatile.Read(ref _instanceCacheHitCount);
            long total = creations + hits;

            if (total == 0)
            {
                return 0;
            }

            return (int)(hits * 1000L / total);
        }
    }

    private static void TRY_AUTO_REGISTER_REPORTABLE(IReportable reportable)
    {
        CoreTelemetryTarget target = reportable switch
        {
            ITaskManager => CoreTelemetryTarget.Tasks,
            IConnectionHub => CoreTelemetryTarget.Connections,
            IBufferPoolManager => CoreTelemetryTarget.Buffers,
            IObjectPoolManager => CoreTelemetryTarget.ObjectPools,
            _ when reportable.GetType().Name == "TaskManager" => CoreTelemetryTarget.Tasks,
            _ when reportable.GetType().Name == "ConcurrencyGate" => CoreTelemetryTarget.ConcurrencyGate,
            _ when reportable.GetType().Name == "ConnectionGuard" => CoreTelemetryTarget.ConnectionGuard,
            _ when reportable.GetType().Name == "PolicyRateLimiter" => CoreTelemetryTarget.PolicyRateLimiter,
            _ when reportable.GetType().Name == "TokenBucketLimiter" => CoreTelemetryTarget.TokenBucketLimiter,
            _ when reportable.GetType().GetInterface("ISessionService") is not null => CoreTelemetryTarget.Sessions,
            _ when reportable.GetType().GetInterface("IPacketDispatch") is not null => CoreTelemetryTarget.PacketDispatch,
            _ => CoreTelemetryTarget.None
        };

        if (target != CoreTelemetryTarget.None)
        {
            ReportRegistry.Instance.Register<IReportable>(target, reportable);
        }
    }

    #endregion IReportable
}

