using Nalix.Common.Connection;

namespace Nalix.Network.Connection;

/// <inheritdoc />
public sealed class ConnectionEventArgs(IConnection connection) : System.EventArgs, IConnectEventArgs
{
    /// <inheritdoc />
    public IConnection Connection { get; } = connection;
}
