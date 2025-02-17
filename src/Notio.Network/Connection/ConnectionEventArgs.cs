using Notio.Common.Connection;
using System;

namespace Notio.Network.Connection;

/// <summary>
/// Represents connection events and provides event data.
/// </summary>
public sealed class ConnectionEventArgs(Connection connection)
    : EventArgs, IConnectEventArgs
{
    /// <inheritdoc />
    public IConnection Connection { get; } = connection;
}
