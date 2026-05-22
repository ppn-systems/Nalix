namespace Nalix.Dashboard.Domain.Reports.Tasks;

public sealed record TasksReport(
    string? UtcNow,
    int RecurringCount,
    int WorkersTotal,
    int WorkersRunning,
    bool DynamicAdjustmentEnabled,
    int CurrentConcurrencyLimit,
    int MaxWorkers,
    double HighCpuThreshold,
    double LowCpuThreshold,
    int ObservingIntervalSeconds,
    int WarmupDurationSeconds,
    int AdjustmentStreakRequired,
    TaskMemoryInfo? Memory,
    TaskProcessInfo? Process,
    long WorkerExecutionCount,
    double AverageWorkerExecutionTimeMs,
    double P95WorkerExecutionTimeMs,
    double P99WorkerExecutionTimeMs,
    double AverageWorkerWaitTimeMs,
    int PeakRunningWorkerCount,
    long WorkerErrorCount,
    long RecurringExecutionCount,
    double AverageRecurringExecutionTimeMs,
    long RecurringErrorCount,
    IReadOnlyList<RecurringTaskEntry>? Recurring,
    IReadOnlyList<RecurringTaskEntry>? TopRecurringByFailures,
    IReadOnlyDictionary<string, WorkerGroupEntry>? WorkersByGroup,
    IReadOnlyList<RunningWorkerEntry>? TopRunningWorkers);

public sealed record TaskMemoryInfo(double WorkingSetMB, double PrivateMB, double VirtualMB);

public sealed record TaskProcessInfo(
    int Threads,
    long CompletedWorkItems,
    int ThreadsRunning,
    int Handles,
    long GCGen0,
    long GCGen1,
    long GCGen2,
    double ManagedHeapMB,
    double UptimeDays,
    string? StartTimeUtc);

public sealed record RecurringTaskEntry(
    string? Name,
    long TotalRuns,
    long ConsecutiveFailures,
    bool IsRunning,
    string? LastRunUtc,
    string? NextRunUtc,
    double IntervalMs,
    string? Tag);

public sealed record RunningWorkerEntry(
    string? Id,
    string? Name,
    string? Group,
    string? StartedUtc,
    double Progress,
    string? LastHeartbeatUtc);

public sealed record WorkerGroupEntry(int Running, int Total, string? Concurrency);
