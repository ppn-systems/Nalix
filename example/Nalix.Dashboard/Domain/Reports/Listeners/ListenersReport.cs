namespace Nalix.Dashboard.Domain.Reports.Listeners;

public sealed record ListenersReport(
    ListenerInfo? TCP,
    ListenerInfo? UDP,
    ListenerInfo? WEBSOCKET);

public sealed record ListenerInfo(
    string? UtcNow,
    int Port,
    string? State,
    bool Disposed,
    ListenerConfiguration? Configuration,
    ListenerMetrics? Metrics,
    ListenerProtocol? Protocol,
    ListenerConnections? Connections,
    ListenerThreading? Threading);

public sealed record ListenerConfiguration(
    bool EnableTimeout,
    int MaxParallelAccepts,
    int BufferSize,
    bool KeepAlive,
    bool ReuseAddress,
    bool EnableIPv6,
    int Backlog);

public sealed record ListenerMetrics(
    long TotalAccepted,
    long TotalRejected,
    long TotalErrors);

public sealed record ListenerProtocol(
    string? BoundProtocol);

public sealed record ListenerConnections(
    int ActiveConnections,
    bool LimiterEnabled);

public sealed record ListenerThreading(
    int ThreadPoolMinWorker,
    int ThreadPoolMinIOCP);
