namespace Nalix.SDK.Tools.Models;

/// <summary>
/// Represents the remote TCP endpoint configuration used by the tool.
/// </summary>
public sealed class ConnectionSettings
{
    /// <summary>
    /// Gets or sets the target host name or IP address.
    /// </summary>
    public string Host { get; set; } = "127.0.0.1";

    /// <summary>
    /// Gets or sets the target TCP port.
    /// </summary>
    public ushort Port { get; set; } = 57206;
}
