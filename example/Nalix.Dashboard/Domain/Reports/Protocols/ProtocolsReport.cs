namespace Nalix.Dashboard.Domain.Reports.Protocols;

public sealed record ProtocolsReport(
    ProtocolInfo? TCP,
    ProtocolInfo? UDP,
    ProtocolInfo? WEBSOCKET);

public sealed record ProtocolInfo(
    string? UtcNow,
    bool IsDisposed,
    long TotalMessages,
    long TotalErrors,
    bool IsAccepting,
    bool KeepConnectionOpen);
