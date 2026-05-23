namespace Nalix.Dashboard.Domain.Reports.ConnectionGuard;

public sealed record ConnectionGuardReport(
    string? UtcNow,
    int MaxPerEndpoint,
    double CleanupIntervalSeconds,
    double InactivityThresholdSeconds,
    int TrackedEndpoints,
    long TotalConcurrent,
    long TotalAttempts,
    long TotalRejections,
    long TotalCleaned,
    double RejectionRate,
    IReadOnlyList<TopEndpointEntry>? TopEndpoints);

public sealed record TopEndpointEntry(
    string? Address,
    int CurrentConnections,
    long TotalConnectionsToday,
    string? LastConnectionUtc);
