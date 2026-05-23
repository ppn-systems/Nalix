namespace Nalix.Dashboard.Domain.Reports.ConcurrencyGate;

public sealed record ConcurrencyGateReport(
    string? UtcNow,
    double CleanupIntervalMinutes,
    double MinIdleAgeMinutes,
    int TrackedOpcodes,
    long TotalAcquired,
    long TotalRejected,
    long TotalQueued,
    long TotalCleaned,
    double RejectionRate,
    CircuitBreakerInfo? CircuitBreaker,
    IReadOnlyList<ConcurrencyGateOpcode>? Opcodes);

public sealed record CircuitBreakerInfo(
    bool IsOpen,
    long Trips);

public sealed record ConcurrencyGateOpcode(
    string? Opcode,
    int Capacity,
    int InUse,
    int Available,
    bool Queuing,
    int QueueCount,
    string? QueueMax,
    bool IsIdle,
    string? LastUsedUtc);
