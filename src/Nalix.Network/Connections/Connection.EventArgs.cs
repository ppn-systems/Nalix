// Copyright (c) 2025 PPN Corporation. All rights reserved.

using Nalix.Common.Connection;
using Nalix.Common.Infrastructure.Connection;

namespace Nalix.Network.Connections;

/// <summary>
/// Provides event data for connection-related events.
/// </summary>
/// <remarks>
/// This class is sealed to prevent derivation and ensure consistent behavior for connection event arguments.
/// </remarks>
public sealed class ConnectionEventArgs : System.EventArgs, IConnectEventArgs
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ConnectionEventArgs"/> class with the specified connection.
    /// </summary>
    /// <param name="connection">The connection associated with the event.</param>
    /// <exception cref="System.ArgumentNullException">Thrown if <paramref name="connection"/> is null.</exception>
    [System.Diagnostics.CodeAnalysis.SuppressMessage(
        "Style", "IDE0290:Use primary constructor", Justification = "<Pending>")]
    public ConnectionEventArgs(IConnection connection) => 
        Connection = connection ?? throw new System.ArgumentNullException(
            nameof(connection), 
            "Connection cannot be null when creating ConnectionEventArgs");

    /// <inheritdoc />
    public IConnection Connection { get; }
}