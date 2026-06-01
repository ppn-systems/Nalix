// Copyright (c) 2025-2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Text;
using System.Threading;

namespace Nalix.Runtime.Throttling;

public sealed partial class ConcurrencyGate
{
    #region Report Generation

    /// <summary>
    /// Generates a human-readable diagnostic report of the concurrency gate state.
    /// </summary>
    [StackTraceHidden]
    public string GenerateReport()
    {
        // Take snapshot
        List<KeyValuePair<ushort, Entry>> snapshot =
            [.. _table];

        // Sort by load (highest pressure first)
        snapshot.Sort((a, b) =>
        {
            int aPressure = a.Value.Capacity - a.Value.Sem.CurrentCount;
            int bPressure = b.Value.Capacity - b.Value.Sem.CurrentCount;

            int cmp = bPressure.CompareTo(aPressure);
            return cmp != 0 ? cmp : b.Value.QueueCount.CompareTo(a.Value.QueueCount);
        });

        // Calculate metrics
        double rejectionRate = 0.0;
        long totalAttempts = Interlocked.Read(ref _totalAcquired) + Interlocked.Read(ref _totalRejected);
        if (totalAttempts > 0)
        {
            rejectionRate = Interlocked.Read(ref _totalRejected) * 100.0 / totalAttempts;
        }

        // Build report
        StringBuilder sb = new();

        this.APPEND_REPORT_HEADER(sb, rejectionRate);
        APPEND_OPCODE_DETAILS(sb, snapshot);

        return sb.ToString();
    }

    /// <inheritdoc/>
    public void WriteReportData(System.Text.Json.Utf8JsonWriter writer)
    {
        ArgumentNullException.ThrowIfNull(writer);

        List<KeyValuePair<ushort, Entry>> entries = [.. _table];
        entries.Sort((a, b) =>
        {
            int aBusy = a.Value.Capacity - a.Value.Sem.CurrentCount;
            int bBusy = b.Value.Capacity - b.Value.Sem.CurrentCount;
            int cmp = bBusy.CompareTo(aBusy);
            return cmp != 0 ? cmp : b.Value.QueueCount.CompareTo(a.Value.QueueCount);
        });

        long totalAttempts = Interlocked.Read(ref _totalAcquired) + Interlocked.Read(ref _totalRejected);
        double rejectionRate = totalAttempts > 0 ? (Interlocked.Read(ref _totalRejected) * 100.0 / totalAttempts) : 0.0;

        writer.WriteStartObject();
        writer.WriteString("UtcNow", DateTime.UtcNow);
        writer.WriteNumber("CleanupIntervalMinutes", _options.CleanupIntervalMinutes);
        writer.WriteNumber("MinIdleAgeMinutes", _options.MinIdleAgeMinutes);
        writer.WriteNumber("TrackedOpcodes", _table.Count);
        writer.WriteNumber("TotalAcquired", Interlocked.Read(ref _totalAcquired));
        writer.WriteNumber("TotalRejected", Interlocked.Read(ref _totalRejected));
        writer.WriteNumber("TotalQueued", Interlocked.Read(ref _totalQueued));
        writer.WriteNumber("TotalCleaned", Interlocked.Read(ref _totalCleanedEntries));
        writer.WriteNumber("RejectionRate", rejectionRate);

        writer.WriteStartObject("CircuitBreaker");
        writer.WriteBoolean("IsOpen", Volatile.Read(ref _circuitBreakerOpen) == 1);
        writer.WriteNumber("Trips", Interlocked.Read(ref _circuitBreakerTrips));
        writer.WriteEndObject();

        writer.WriteStartArray("Opcodes");
        int count = 0;
        foreach (KeyValuePair<ushort, Entry> kvp in entries)
        {
            if (count++ >= 50)
            {
                break;
            }

            ushort opcode = kvp.Key;
            Entry entry = kvp.Value;
            int available = entry.Sem.CurrentCount;
            int inUse = entry.Capacity - available;
            string queueMaxStr = entry.QueueMax == int.MaxValue ? "∞" : entry.QueueMax.ToString(CultureInfo.InvariantCulture);

            writer.WriteStartObject();
            writer.WriteString("Opcode", $"0x{opcode:X4}");
            writer.WriteNumber("Capacity", entry.Capacity);
            writer.WriteNumber("InUse", inUse);
            writer.WriteNumber("Available", available);
            writer.WriteBoolean("Queuing", entry.Queue);
            writer.WriteNumber("QueueCount", entry.QueueCount);
            writer.WriteString("QueueMax", queueMaxStr);
            writer.WriteBoolean("IsIdle", entry.IsIdle);
            writer.WriteString("LastUsedUtc", entry.LastUsedUtc);
            writer.WriteEndObject();
        }
        writer.WriteEndArray();

        writer.WriteEndObject();
    }

    private void APPEND_REPORT_HEADER(StringBuilder sb, double rejectionRate)
    {
        _ = sb.AppendLine(CultureInfo.InvariantCulture, $"[{DateTime.UtcNow:yyyy-MM-dd HH:mm:ss}] ConcurrencyGate Status:");
        _ = sb.AppendLine(CultureInfo.InvariantCulture, $"CleanupInterval    : {_options.CleanupIntervalMinutes:F1} min");
        _ = sb.AppendLine(CultureInfo.InvariantCulture, $"MinIdleAge         : {_options.MinIdleAgeMinutes:F1} min");
        _ = sb.AppendLine(CultureInfo.InvariantCulture, $"TrackedOpcodes     : {_table.Count}");
        _ = sb.AppendLine(CultureInfo.InvariantCulture, $"TotalAcquired      : {Interlocked.Read(ref _totalAcquired):N0}");
        _ = sb.AppendLine(CultureInfo.InvariantCulture, $"TotalRejected      : {Interlocked.Read(ref _totalRejected):N0}");
        _ = sb.AppendLine(CultureInfo.InvariantCulture, $"TotalQueued        : {Interlocked.Read(ref _totalQueued):N0}");
        _ = sb.AppendLine(CultureInfo.InvariantCulture, $"TotalCleaned       : {Interlocked.Read(ref _totalCleanedEntries):N0}");
        _ = sb.AppendLine(CultureInfo.InvariantCulture, $"RejectionRate      : {rejectionRate:F2}%");
        _ = sb.AppendLine(CultureInfo.InvariantCulture, $"CircuitBreaker     : {(Volatile.Read(ref _circuitBreakerOpen) == 1 ? "OPEN" : "Closed")} (trips={Interlocked.Read(ref _circuitBreakerTrips)})");
        _ = sb.AppendLine();
    }

    private static void APPEND_OPCODE_DETAILS(StringBuilder sb, List<KeyValuePair<ushort, Entry>> snapshot)
    {
        _ = sb.AppendLine("Top Opcodes by Load:");
        _ = sb.AppendLine("---------------------------------------------------------------------------------");
        _ = sb.AppendLine("Opcode | Capacity | InUse | Avail | Queue | QueueMax | Queuing | LastUsed");
        _ = sb.AppendLine("---------------------------------------------------------------------------------");

        if (snapshot.Count == 0)
        {
            _ = sb.AppendLine("(no tracked opcodes)");
        }
        else
        {
            APPEND_TOP_OPCODES(sb, snapshot, maxRows: 50);
        }

        _ = sb.AppendLine("---------------------------------------------------------------------------------");
    }

    private static void APPEND_TOP_OPCODES(StringBuilder sb, List<KeyValuePair<ushort, Entry>> snapshot, int maxRows)
    {
        int rows = 0;

        foreach (KeyValuePair<ushort, Entry> kvp in snapshot)
        {
            if (rows++ >= maxRows)
            {
                break;
            }

            ushort opcode = kvp.Key;
            Entry entry = kvp.Value;

            int available = entry.Sem.CurrentCount;
            int inUse = entry.Capacity - available;
            int queueCount = entry.QueueCount;
            string queueEnabled = entry.Queue ? "yes" : " no";
            string queueMaxStr = entry.QueueMax == int.MaxValue ? "∞" : entry.QueueMax.ToString(CultureInfo.InvariantCulture);
            DateTimeOffset lastUsed = entry.LastUsedUtc;

            _ = sb.AppendLine(
                CultureInfo.InvariantCulture,
                $"0x{opcode:X4} | {entry.Capacity,8} | {inUse,5} | {available,5} | {queueCount,5} | {queueMaxStr,8} | {queueEnabled,7} | {lastUsed:HH:mm:ss}");
        }
    }

    #endregion Report Generation
}

