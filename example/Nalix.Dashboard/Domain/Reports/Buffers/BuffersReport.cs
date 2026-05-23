namespace Nalix.Dashboard.Domain.Reports.Buffers;

public sealed record BuffersReport(
    string? UtcNow,
    bool Initialized,
    int TotalBuffersConfigured,
    int PoolCount,
    int MinBufferSize,
    int MaxBufferSize,
    bool EnableTrimming,
    bool EnableAnalytics,
    bool FallbackToArrayPool,
    int TrimIntervalMinutes,
    int DeepTrimIntervalMinutes,
    long TrimCycleCount,
    long FallbackCount,
    long BucketCacheHits,
    long BucketCacheMisses,
    long PeakMemoryUsageBytes,
    double ThroughputMBps,
    ShrinkSafetyPolicy? ShrinkSafetyPolicy,
    IReadOnlyList<BufferPoolEntry>? Pools,
    long TotalHits,
    long TotalMisses,
    long TotalExpands,
    long TotalShrinks,
    double HitRate);

public sealed record ShrinkSafetyPolicy(
    double MinimumRetentionPercent,
    double MaxSingleShrinkStep,
    double MaxShrinkPercentPerCycle,
    int AbsoluteMinimum);

public sealed record BufferPoolEntry(
    int BufferSize,
    int Initial,
    int Total,
    int Free,
    int InUse,
    long Hits,
    long Expands,
    long Shrinks,
    double UsageRatio,
    double MissRate,
    long ShrinkSkipped,
    string? BytesReturned);
