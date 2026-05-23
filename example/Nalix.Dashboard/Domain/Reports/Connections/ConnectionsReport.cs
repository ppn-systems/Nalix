namespace Nalix.Dashboard.Domain.Reports.Connections;

public sealed record ConnectionsReport(
    string? UtcNow,
    long TotalConnections,
    int ShardCount,
    long TotalBytesSent,
    long TotalBytesReceived,
    long IngressBytesPerSecond,
    long EgressBytesPerSecond);
