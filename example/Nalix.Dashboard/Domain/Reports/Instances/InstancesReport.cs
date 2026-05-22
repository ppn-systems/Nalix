namespace Nalix.Dashboard.Domain.Reports.Instances;

public sealed record InstancesReport(
    string? UtcNow,
    int CachedInstanceCount,
    long InstanceCreationCount,
    long InstanceCacheHitCount,
    int SignatureInstanceCount,
    int ActivatorFactoryCount,
    int DisposableCount,
    long SlotsInvalidated,
    long TotalGetOrCreateCalls,
    long HitRatePermille,
    IReadOnlyList<InstanceEntry>? Instances);

public sealed record InstanceEntry(string? Type, bool IsDisposable, string? Source);
