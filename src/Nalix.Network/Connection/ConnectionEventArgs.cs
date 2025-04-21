namespace Nalix.Network.Connection;

/// <inheritdoc />
public sealed class ConnectionEventArgs(Connection connection)
    : System.EventArgs, Common.Connection.IConnectEventArgs
{
    /// <inheritdoc />
    public Common.Connection.IConnection Connection { get; } = connection;
}
