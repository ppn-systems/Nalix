namespace Nalix.Dashboard.Domain.Reports.Dispatch;

public sealed record DispatchReport(
    string? UtcNow,
    bool Running,
    long DispatchLoops,
    long TotalPackets,
    long TotalConnections,
    long ReadyConnections,
    long WakeSignals,
    long WakeReads,
    bool WakeRequested,
    string? PacketRegistryType,
    IReadOnlyDictionary<string, long>? PendingPerPriority,
    IReadOnlyList<PendingConnectionEntry>? PendingByConnection,
    PipelineMetricsReport? PipelineMetrics,
    IReadOnlyList<MiddlewareMetricEntry>? MiddlewareMetrics);

public sealed record PendingConnectionEntry(string? EndPoint, long Pending);

public sealed record PipelineMetricsReport(
    long ActiveExecutions,
    long TotalExecutions,
    long TotalErrors,
    long DeserializationErrors,
    double AverageTimeMs);

public sealed record MiddlewareMetricEntry(
    string? Type,
    long TotalExecutions,
    long TotalErrors,
    double AverageTimeMs);
