// Copyright (c) 2025-2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using Nalix.Abstractions.Diagnostics;
using Nalix.Abstractions.Exceptions;
using Nalix.Abstractions.Identity;
using Nalix.Framework.Extensions;

namespace Nalix.Framework.Tasks;

public sealed partial class TaskManager
{
    #region IReportable

    /// <inheritdoc/>
    [StackTraceHidden]
    [MethodImpl(MethodImplOptions.NoInlining)]
    public string GenerateReport()
    {
        StringBuilder sb = new(2048);
        int runningWorkers = Volatile.Read(ref _runningWorkerCount);
        _ = sb.AppendLine(CultureInfo.InvariantCulture, $"[{DateTime.UtcNow:yyyy-MM-dd HH:mm:ss}] TaskManager:");
        _ = sb.AppendLine(CultureInfo.InvariantCulture, $"Recurring: {_recurring.Count} | Workers: {_workers.Count} (running={runningWorkers})");
        _ = sb.AppendLine();

        // ========== CPU Monitoring Section ==========
        _ = sb.AppendLine("---------------------------------------------------------------------");
        _ = sb.AppendLine("CPU Monitoring:");
        _ = sb.AppendLine(CultureInfo.InvariantCulture, $"Dynamic Adjustment Enabled        : {_options.DynamicAdjustmentEnabled}");
        _ = sb.AppendLine(CultureInfo.InvariantCulture, $"Current Concurrency Limit         : {_currentConcurrencyLimit}/{_options.MaxWorkers}");
        _ = sb.AppendLine(CultureInfo.InvariantCulture, $"High CPU Threshold                : {_options.ThresholdHighCpu:F1}%");
        _ = sb.AppendLine(CultureInfo.InvariantCulture, $"Low CPU Threshold                 : {_options.ThresholdLowCpu:F1}%");
        _ = sb.AppendLine(CultureInfo.InvariantCulture, $"Observing Interval                : {_options.ObservingInterval.TotalSeconds:F1}s");
        _ = sb.AppendLine(CultureInfo.InvariantCulture, $"Warmup Duration                   : {_options.CpuWarmupDuration.TotalSeconds:F1}s");
        _ = sb.AppendLine(CultureInfo.InvariantCulture, $"Adjustment Streak Required        : {_options.AdjustmentStreakRequired}");
        _ = sb.AppendLine("---------------------------------------------------------------------");
        _ = sb.AppendLine();

        try
        {
            Process proc = Process.GetCurrentProcess();
            proc.Refresh();

            long workingSetMB = proc.WorkingSet64 / (1024 * 1024);
            long privateMB = proc.PrivateMemorySize64 / (1024 * 1024);
            long virtualMB = proc.VirtualMemorySize64 / (1024 * 1024);

            _ = sb.AppendLine("---------------------------------------------------------------------");
            _ = sb.AppendLine("Memory Usage:");
            _ = sb.AppendLine(CultureInfo.InvariantCulture, $"Working Set                       : {workingSetMB,6:N0} MB");
            _ = sb.AppendLine(CultureInfo.InvariantCulture, $"Private Bytes                     : {privateMB,6:N0} MB");
            _ = sb.AppendLine(CultureInfo.InvariantCulture, $"Virtual Memory                    : {virtualMB,6:N0} MB");
            _ = sb.AppendLine("---------------------------------------------------------------------");
            _ = sb.AppendLine();
        }
        catch (Exception ex) when (ExceptionClassifier.IsNonFatal(ex))
        {
            if (DiagnosticsEvents.Source.IsEnabled(DiagnosticsEvents.Tasks.Failed))
            {
                DiagnosticsEvents.Write(DiagnosticsEvents.Tasks.Failed, new DiagnosticLog("FW.TaskManager:Internal", "memory-diagnostics-failed", ex));
            }
        }

        try
        {
            Process proc = Process.GetCurrentProcess();
            proc.Refresh();

            ThreadPool.GetMaxThreads(out int maxWorkerThreads, out int _);
            ThreadPool.GetAvailableThreads(out int availableWorkerThreads, out int _);

            int activeWorkerThreads = maxWorkerThreads - availableWorkerThreads;

            _ = sb.AppendLine("Process Health:");
            _ = sb.AppendLine("---------------------------------------------------------------------");
            _ = sb.AppendLine(CultureInfo.InvariantCulture, $"Threads                           : {ThreadPool.ThreadCount} (running: {activeWorkerThreads})");
            _ = sb.AppendLine(CultureInfo.InvariantCulture, $"Completed Work Items              : {ThreadPool.CompletedWorkItemCount:N0}");
            _ = sb.AppendLine(CultureInfo.InvariantCulture, $"Handles                           : {proc.HandleCount:N0}");
            _ = sb.AppendLine(CultureInfo.InvariantCulture, $"GC Collections                    : Gen0={GC.CollectionCount(0):N0} | Gen1={GC.CollectionCount(1):N0} | Gen2={GC.CollectionCount(2):N0}");
            _ = sb.AppendLine(CultureInfo.InvariantCulture, $"Managed Heap                      : {GC.GetTotalMemory(false) / 1048576:N0} MB");
            _ = sb.AppendLine(CultureInfo.InvariantCulture, $"Uptime                            : {(DateTimeOffset.UtcNow - proc.StartTime.ToUniversalTime()).TotalDays:F1} days ({proc.StartTime:yyyy-MM-dd HH:mm:ss} UTC)");
            _ = sb.AppendLine("---------------------------------------------------------------------");
            _ = sb.AppendLine();
        }
        catch (Exception ex) when (ExceptionClassifier.IsNonFatal(ex))
        {
            if (DiagnosticsEvents.Source.IsEnabled(DiagnosticsEvents.Tasks.Failed))
            {
                DiagnosticsEvents.Write(DiagnosticsEvents.Tasks.Failed, new DiagnosticLog("FW.TaskManager:Internal", "process-health-diagnostics-failed", ex));
            }
        }

        _ = sb.AppendLine("---------------------------------------------------------------------");
        _ = sb.AppendLine("Monitoring Statistics:");
        double uptimeSec = (DateTimeOffset.UtcNow - Process.GetCurrentProcess().StartTime.ToUniversalTime()).TotalSeconds;
        double workerTps = uptimeSec > 0 ? _workerCompletionCount / uptimeSec : 0;
        double recurringTps = uptimeSec > 0 ? _recurringExecutionCount / uptimeSec : 0;

        _ = sb.AppendLine(CultureInfo.InvariantCulture, $"Worker Completion Count           : {_workerCompletionCount} ({workerTps:F2} ops/s)");
        _ = sb.AppendLine(CultureInfo.InvariantCulture, $"Average Worker Uptime             : {this.AverageWorkerUptime:F2} ms");
        _ = sb.AppendLine(CultureInfo.InvariantCulture, $"P95 Worker Uptime                 : <{this.P95WorkerUptime:F2} ms");
        _ = sb.AppendLine(CultureInfo.InvariantCulture, $"P99 Worker Uptime                 : <{this.P99WorkerUptime:F2} ms");
        _ = sb.AppendLine(CultureInfo.InvariantCulture, $"Average Worker Wait Time          : {this.AverageWorkerWaitTime:F2} ms");
        _ = sb.AppendLine(CultureInfo.InvariantCulture, $"Peak Running Workers              : {this.PeakRunningWorkerCount}");
        _ = sb.AppendLine(CultureInfo.InvariantCulture, $"Worker Error Count                : {this.WorkerErrorCount}");
        _ = sb.AppendLine();
        _ = sb.AppendLine(CultureInfo.InvariantCulture, $"Recurring Execution Count         : {_recurringExecutionCount} ({recurringTps:F2} ops/s)");
        _ = sb.AppendLine(CultureInfo.InvariantCulture, $"Average Recurring Execution Time  : {this.AverageRecurringExecutionTime:F2} ms");
        _ = sb.AppendLine(CultureInfo.InvariantCulture, $"Recurring Error Count             : {this.RecurringErrorCount}");
        _ = sb.AppendLine("---------------------------------------------------------------------");
        _ = sb.AppendLine();

        // Recurring summary
        List<RecurringState> recurringSnapshot = new(_recurring.Count);
        foreach (KeyValuePair<string, RecurringState> kv in _recurring)
        {
            recurringSnapshot.Add(kv.Value);
        }

        recurringSnapshot.Sort(static (a, b) => b.ConsecutiveFailures.CompareTo(a.ConsecutiveFailures));

        _ = sb.AppendLine("Recurring (Dashboard):");
        _ = sb.AppendLine("-----------------------------------------------------------------------------------------------------");
        _ = sb.AppendLine("NAMING                       | RUNS (T/F)    | RUN | SCHEDULE (L/N)          | INTERVAL  | TAG       ");
        _ = sb.AppendLine("-----------------------------+---------------+-----+-------------------------+-----------+-----------");
        foreach (RecurringState s in recurringSnapshot)
        {
            string nm = ReportExtensions.FormatTypeName(s.Name, 28);
            string runsFails = $"{s.TotalRuns.FormatCompact()} / {s.ConsecutiveFailures}";
            string run = s.IsRunning ? "yes" : " no";

            string last = s.LastRunUtc?.ToString("HH:mm:ss", CultureInfo.InvariantCulture) ?? "--:--:--";
            string next = s.NextRunUtc?.ToString("HH:mm:ss", CultureInfo.InvariantCulture) ?? "--:--:--";
            string schedule = $"{last} / {next}";

            string iv = s.Interval.FormatTimeSpan();
            string tag = s.Options.Tag ?? "-";
            _ = sb.AppendLine(CultureInfo.InvariantCulture, $"{nm} | {runsFails,-13} | {run,3} | {schedule,-23} | {iv,9} | {tag}");
        }
        _ = sb.AppendLine("-----------------------------------------------------------------------------------------------------");
        _ = sb.AppendLine();
        _ = sb.AppendLine();

        // Workers summary by group
        _ = sb.AppendLine("Workers by Group:");
        _ = sb.AppendLine("------------------------------------------------------------");
        _ = sb.AppendLine("Group                        | Running | Total | Concurrency");
        _ = sb.AppendLine("-----------------------------+---------+-------+------------");
        Dictionary<string, (int running, int total)> perGroup = new(StringComparer.Ordinal);
        foreach (KeyValuePair<ISnowflake, WorkerState> kv in _workers)
        {
            WorkerState worker = kv.Value;
            if (perGroup.TryGetValue(worker.Group, out (int running, int total) stats))
            {
                perGroup[worker.Group] = (stats.running + (worker.IsRunning ? 1 : 0), stats.total + 1);
            }
            else
            {
                perGroup[worker.Group] = (worker.IsRunning ? 1 : 0, 1);
            }
        }

        List<string> groupNames = new(perGroup.Count);
        foreach (KeyValuePair<string, (int running, int total)> gkv in perGroup)
        {
            groupNames.Add(gkv.Key);
        }

        groupNames.Sort(StringComparer.Ordinal);

        foreach (string groupName in groupNames)
        {
            string gname = PadName(groupName, 28);
            (int running, int total) = perGroup[groupName];
            if (_groupGates.TryGetValue(groupName, out Gate? gate))
            {
                int capacity = gate.Capacity;
                int used = capacity - gate.SemaphoreSlim.CurrentCount;
                _ = sb.AppendLine(CultureInfo.InvariantCulture, $"{gname} | {running,7} | {total,5} | {used}/{capacity}");
            }
            else
            {
                _ = sb.AppendLine(CultureInfo.InvariantCulture, $"{gname} | {running,7} | {total,5} | -");
            }
        }
        _ = sb.AppendLine("------------------------------------------------------------");
        _ = sb.AppendLine();

        // Top N long-running workers
        _ = sb.AppendLine("Top Running Workers (by age):");
        _ = sb.AppendLine("--------------------------------------------------------------------------------------------------------------");
        _ = sb.AppendLine("Id               | Naming                       | Group                        | Age     | Progress | LastBeat");
        _ = sb.AppendLine("-----------------+------------------------------+------------------------------+---------+----------+---------");
        List<WorkerState> top = new(_workers.Count);
        foreach (WorkerState worker in _workers.Values)
        {
            top.Add(worker);
        }

        top.Sort(static (a, b) => a.StartedUtc.CompareTo(b.StartedUtc)); // oldest first
        int show = 0;
        foreach (WorkerState w in top)
        {
            if (!w.IsRunning)
            {
                continue;
            }

            _ = sb.AppendLine(CultureInfo.InvariantCulture, $"{w.Id} | {ReportExtensions.FormatTypeName(w.Name, 28)} | {ReportExtensions.FormatTypeName(w.Group, 28)} | {FormatAge(w.StartedUtc),7} | {w.Progress.FormatCompact(),8} | {w.LastHeartbeatUtc?.ToString("HH:mm:ss", CultureInfo.InvariantCulture) ?? "-"}");
            if (++show >= 50)
            {
                break;
            }
        }

        _ = sb.AppendLine("-------------------------------------------------------------------------------------------------------------");
        return sb.ToString();

        static string PadName(string s, int width)
            => s.Length > width ? $"{MemoryExtensions.AsSpan(s, 0, width - 1)}…" : s.PadRight(width);

        static string FormatAge(DateTimeOffset start)
        {
            TimeSpan ts = DateTimeOffset.UtcNow - start;
            if (ts.TotalHours >= 1)
            {
                return $"{(int)ts.TotalHours}h{ts.Minutes:D2}m";
            }
            else if (ts.TotalMinutes >= 1)
            {
                return $"{(int)ts.TotalMinutes}m{ts.Seconds:D2}s";
            }
            else
            {
                return $"{(int)ts.TotalSeconds}s";
            }
        }
    }

    /// <inheritdoc/>
    public void WriteReportData(System.Text.Json.Utf8JsonWriter writer)
    {
        ArgumentNullException.ThrowIfNull(writer);

        int runningWorkers = Volatile.Read(ref _runningWorkerCount);

        writer.WriteStartObject();
        writer.WriteString("UtcNow", DateTime.UtcNow);
        writer.WriteNumber("RecurringCount", _recurring.Count);
        writer.WriteNumber("WorkersTotal", _workers.Count);
        writer.WriteNumber("WorkersRunning", runningWorkers);

        writer.WriteBoolean("DynamicAdjustmentEnabled", _options.DynamicAdjustmentEnabled);
        writer.WriteNumber("CurrentConcurrencyLimit", _currentConcurrencyLimit);
        writer.WriteNumber("MaxWorkers", _options.MaxWorkers);
        writer.WriteNumber("HighCpuThreshold", _options.ThresholdHighCpu);
        writer.WriteNumber("LowCpuThreshold", _options.ThresholdLowCpu);
        writer.WriteNumber("ObservingIntervalSeconds", _options.ObservingInterval.TotalSeconds);
        writer.WriteNumber("WarmupDurationSeconds", _options.CpuWarmupDuration.TotalSeconds);
        writer.WriteNumber("AdjustmentStreakRequired", _options.AdjustmentStreakRequired);

        try
        {
            Process proc = Process.GetCurrentProcess();
            proc.Refresh();

            writer.WriteStartObject("Memory");
            writer.WriteNumber("WorkingSetMB", proc.WorkingSet64 / (1024 * 1024));
            writer.WriteNumber("PrivateMB", proc.PrivateMemorySize64 / (1024 * 1024));
            writer.WriteNumber("VirtualMB", proc.VirtualMemorySize64 / (1024 * 1024));
            writer.WriteEndObject();

            ThreadPool.GetMaxThreads(out int maxWorkerThreads, out int _);
            ThreadPool.GetAvailableThreads(out int availableWorkerThreads, out int _);
            int activeWorkerThreads = maxWorkerThreads - availableWorkerThreads;

            writer.WriteStartObject("Process");
            writer.WriteNumber("Threads", ThreadPool.ThreadCount);
            writer.WriteNumber("CompletedWorkItems", ThreadPool.CompletedWorkItemCount);
            writer.WriteNumber("ThreadsRunning", activeWorkerThreads);
            writer.WriteNumber("Handles", proc.HandleCount);
            writer.WriteNumber("GCGen0", GC.CollectionCount(0));
            writer.WriteNumber("GCGen1", GC.CollectionCount(1));
            writer.WriteNumber("GCGen2", GC.CollectionCount(2));
            writer.WriteNumber("ManagedHeapMB", GC.GetTotalMemory(false) / 1048576);
            writer.WriteNumber("UptimeDays", (DateTimeOffset.UtcNow - proc.StartTime.ToUniversalTime()).TotalDays);
            writer.WriteString("StartTimeUtc", proc.StartTime.ToUniversalTime());
            writer.WriteEndObject();
        }
        catch (Exception ex) when (ExceptionClassifier.IsNonFatal(ex))
        {
        }

        writer.WriteNumber("WorkerCompletionCount", _workerCompletionCount);
        writer.WriteNumber("AverageWorkerUptimeMs", this.AverageWorkerUptime);
        writer.WriteNumber("P95WorkerUptimeMs", this.P95WorkerUptime);
        writer.WriteNumber("P99WorkerUptimeMs", this.P99WorkerUptime);
        writer.WriteNumber("AverageWorkerWaitTimeMs", this.AverageWorkerWaitTime);
        writer.WriteNumber(nameof(this.PeakRunningWorkerCount), this.PeakRunningWorkerCount);
        writer.WriteNumber(nameof(this.WorkerErrorCount), this.WorkerErrorCount);
        writer.WriteNumber("RecurringExecutionCount", _recurringExecutionCount);
        writer.WriteNumber("AverageRecurringExecutionTimeMs", this.AverageRecurringExecutionTime);
        writer.WriteNumber(nameof(this.RecurringErrorCount), this.RecurringErrorCount);

        List<RecurringState> recurringSnapshot = new(_recurring.Count);
        foreach (RecurringState recurring in _recurring.Values)
        {
            recurringSnapshot.Add(recurring);
        }

        writer.WriteStartArray("Recurring");
        foreach (RecurringState s in recurringSnapshot)
        {
            writer.WriteStartObject();
            writer.WriteString("Name", s.Name);
            writer.WriteNumber("TotalRuns", s.TotalRuns);
            writer.WriteNumber("ConsecutiveFailures", s.ConsecutiveFailures);
            writer.WriteBoolean("IsRunning", s.IsRunning);
            writer.WriteString("LastRunUtc", s.LastRunUtc ?? DateTimeOffset.MinValue);
            writer.WriteString("NextRunUtc", s.NextRunUtc ?? DateTimeOffset.MinValue);
            writer.WriteNumber("IntervalMs", s.Interval.TotalMilliseconds);
            writer.WriteString("Tag", s.Options.Tag ?? "N/A");
            writer.WriteEndObject();
        }
        writer.WriteEndArray();

        recurringSnapshot.Sort(static (a, b) => b.ConsecutiveFailures.CompareTo(a.ConsecutiveFailures));
        int topRecurringCount = recurringSnapshot.Count < 5 ? recurringSnapshot.Count : 5;

        writer.WriteStartArray("TopRecurringByFailures");
        for (int i = 0; i < topRecurringCount; i++)
        {
            RecurringState r = recurringSnapshot[i];
            writer.WriteStartObject();
            writer.WriteString("Name", r.Name);
            writer.WriteNumber("ConsecutiveFailures", r.ConsecutiveFailures);
            writer.WriteString("LastRunUtc", r.LastRunUtc ?? DateTimeOffset.MinValue);
            writer.WriteString("Tag", r.Options.Tag ?? "N/A");
            writer.WriteEndObject();
        }
        writer.WriteEndArray();

        Dictionary<string, (int running, int total)> groupCounts = new(StringComparer.Ordinal);
        foreach (KeyValuePair<ISnowflake, WorkerState> kv in _workers)
        {
            WorkerState st = kv.Value;
            if (groupCounts.TryGetValue(st.Group, out (int running, int total) stats))
            {
                groupCounts[st.Group] = (stats.running + (st.IsRunning ? 1 : 0), stats.total + 1);
            }
            else
            {
                groupCounts[st.Group] = (st.IsRunning ? 1 : 0, 1);
            }
        }

        List<string> groupNames = new(groupCounts.Count);
        foreach (KeyValuePair<string, (int running, int total)> kv in groupCounts)
        {
            groupNames.Add(kv.Key);
        }

        groupNames.Sort(StringComparer.Ordinal);

        writer.WriteStartObject("WorkersByGroup");
        foreach (string groupName in groupNames)
        {
            (int running, int total) = groupCounts[groupName];
            string concurrency = _groupGates.TryGetValue(groupName, out Gate? gate)
                ? $"{gate.Capacity - gate.SemaphoreSlim.CurrentCount}/{gate.Capacity}"
                : "-";

            writer.WriteStartObject(groupName);
            writer.WriteNumber("Running", running);
            writer.WriteNumber("Total", total);
            writer.WriteString("Concurrency", concurrency);
            writer.WriteEndObject();
        }
        writer.WriteEndObject();

        List<WorkerState> runningSnapshot = new(_workers.Count);
        foreach (WorkerState worker in _workers.Values)
        {
            if (worker.IsRunning)
            {
                runningSnapshot.Add(worker);
            }
        }

        runningSnapshot.Sort(static (a, b) => a.StartedUtc.CompareTo(b.StartedUtc));
        int topRunningCount = runningSnapshot.Count < 50 ? runningSnapshot.Count : 50;

        writer.WriteStartArray("TopRunningWorkers");
        for (int i = 0; i < topRunningCount; i++)
        {
            WorkerState w = runningSnapshot[i];
            writer.WriteStartObject();
            writer.WriteString("Id", w.Id.ToString() ?? "N/A");
            writer.WriteString("Name", w.Name);
            writer.WriteString("Group", w.Group);
            writer.WriteString("StartedUtc", w.StartedUtc);
            writer.WriteNumber("Progress", w.Progress);
            writer.WriteString("LastHeartbeatUtc", w.LastHeartbeatUtc ?? DateTimeOffset.MinValue);
            writer.WriteEndObject();
        }
        writer.WriteEndArray();

        writer.WriteEndObject();
    }

    #endregion IReportable
}

