namespace Nalix.Dashboard.Domain.Reports.ObjectPools;

public sealed record ObjectPoolsReport(
    string? UtcNow,
    double UptimeSeconds,
    int PoolCount,
    int PeakPoolCount,
    int UnhealthyPoolCount,
    int DefaultMaxPoolSize,
    string? StartTime,
    long LastHealthCheckTicks,
    long TotalGetOperations,
    long TotalReturnOperations,
    long TotalCacheHits,
    long TotalCacheMisses,
    long TotalCreated,
    long TotalDisposed,
    long TotalLeaked,
    double CacheHitRate,
    double Throughput,
    double CreationRate,
    long NetObjects,
    IReadOnlyList<ObjectPoolEntry>? Pools,
    IReadOnlyList<ObjectPoolEntry>? UnhealthyPools);

public sealed record ObjectPoolEntry(
    string? Type,
    int Available,
    int MaxCapacity,
    bool IsActive,
    long Gets,
    long Hits,
    long Misses,
    double HitRate,
    string? LastAccessUtc,
    string? LastAccessType,
    int Outstanding,
    int ConsecutiveFailures,
    string? Status);
