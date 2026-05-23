namespace Nalix.Dashboard.Domain.Reports.Sessions;

public sealed record SessionsReport(
    string? Type,
    int MinAttributesForPersistence,
    string? SessionTtl,
    long TotalStoresAttempted,
    long TotalStoresSucceeded,
    long TotalStoresFailed,
    long TotalStoresRejectedByPolicy,
    long TotalConsumesAttempted,
    long TotalConsumesSucceeded,
    long TotalConsumesFailed,
    SessionStoreInfo? Store,
    string? StoreType);

public sealed record SessionStoreInfo(
    string? Type,
    int ActiveSessions,
    long TotalStored,
    long TotalConsumed,
    long TotalExpired);
