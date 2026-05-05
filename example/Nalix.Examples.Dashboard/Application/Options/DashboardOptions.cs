namespace Nalix.Examples.Dashboard.Application.Options;

internal sealed class DashboardOptions
{
    public string BackendAddress { get; set; } = "127.0.0.1";

    public ushort BackendPort { get; set; } = 57206;

    public string? ServerPublicKey { get; set; }

    public string ServerPublicKeyPath { get; set; } = "shared/certificate.public";

    public int PollIntervalMilliseconds { get; set; } = 2000;

    public int PingIntervalMilliseconds { get; set; } = 2000;

    public int RequestTimeoutMilliseconds { get; set; } = 5000;
}
