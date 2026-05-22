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
    IReadOnlyList<PendingConnectionEntry>? PendingByConnection);

public sealed record PendingConnectionEntry(string? EndPoint, long Pending);
