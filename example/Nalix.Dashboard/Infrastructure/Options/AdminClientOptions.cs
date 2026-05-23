namespace Nalix.Dashboard.Infrastructure.Options;

internal sealed class AdminClientOptions
{
    public string BackendHost { get; set; } = "localhost";

    public ushort BackendPort { get; set; } = 57206;

    public string? ServerPublicKey { get; set; }
}
