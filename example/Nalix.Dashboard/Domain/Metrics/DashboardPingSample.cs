namespace Nalix.Dashboard.Domain.Metrics;

public sealed record DashboardPingSample(
    DateTimeOffset Timestamp,
    double Milliseconds);

