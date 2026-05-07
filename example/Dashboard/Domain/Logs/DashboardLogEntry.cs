namespace Dashboard.Domain.Logs;

public sealed record DashboardLogEntry(
    DateTimeOffset Timestamp,
    string Level,
    string Message);
