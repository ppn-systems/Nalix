namespace Nalix.Examples.Dashboard.Services;

internal sealed record DashboardLogEntry(
    DateTimeOffset Timestamp,
    string Level,
    string Message);
