// Copyright (c) 2026 PPN Corporation. All rights reserved.

using Nalix.Common.Diagnostics.Abstractions;
using Nalix.Common.Networking.Abstractions;
using Nalix.Framework.Injection;
using Nalix.Network.Abstractions;
using Nalix.Network.Connections;
using Nalix.Network.Protocols;

namespace Nalix.Network.Examples.Protocols;

/// <summary>
/// Represents a custom protocol handler for the AutoX application.
/// Implements specific logic for inbound message processing and connection validation.
/// </summary>
public sealed class AutoXProtocol : Protocol
{
    private readonly IPacketDispatch s_Dispatch;

    /// <inheritdoc/>
    public AutoXProtocol(IPacketDispatch dispatch) : base()
    {
        s_Dispatch = dispatch;
        // Enable accepting connections by default
        IsAccepting = true;

        // Optionally keep connections open after processing messages (e.g., for persistent sessions)
        KeepConnectionOpen = true;
    }

    public override void OnAccept(IConnection connection, System.Threading.CancellationToken cancellationToken = default)
    {
        base.OnAccept(connection, cancellationToken);

        // Log new connection and register with ConnectionHub if available
        // Auto unregisters on disconnect via ConnectionHub's internal tracking
        InstanceManager.Instance.GetExistingInstance<ConnectionHub>()?
                                .RegisterConnection(connection);
    }

    /// <summary>
    /// Processes a received message from an active connection.
    /// Handles application-specific parsing and validation logic.
    /// </summary>
    /// <param name="sender">The source of the event.</param>
    /// <param name="args">Event arguments containing the connection and message information.</param>
    public override void ProcessMessage(System.Object sender, IConnectEventArgs args)
    {
        // Validate arguments and protocol state
        System.ArgumentNullException.ThrowIfNull(args);

        // TODO: Parse message and implement business logic here
        System.Console.WriteLine($"[AutoX.{nameof(AutoXProtocol)}:{nameof(ProcessMessage)}] Received message from connection id={args.Connection.ID}");
        // Auto dispose the incoming packet after processing
        s_Dispatch.HandlePacket(args.Lease, args.Connection);

        args.Dispose();
    }

    /// <summary>
    /// Validates an incoming connection prior to accepting.
    /// Override to implement custom validation (e.g., IP filter, authentication handshake).
    /// </summary>
    /// <param name="connection">The connection to validate.</param>
    /// <returns>True if accepted; false otherwise.</returns>
    protected override System.Boolean ValidateConnection(IConnection connection)
    {
        // TODO: Add custom validation logic, e.g. check IP, token, etc.
        InstanceManager.Instance.GetExistingInstance<ILogger>()?
                                .Debug($"[AutoX.{nameof(AutoXProtocol)}:{nameof(ValidateConnection)}] Validating connection id={connection.ID}");

        return true; // Accept all for now
    }

    /// <summary>
    /// Custom post-processing after a message is handled.
    /// Called from base.PostProcessMessage().
    /// </summary>
    /// <param name="args">Event arguments containing connection and processing results.</param>
    protected override void OnPostProcess(IConnectEventArgs args)
    {
        // TODO: Add post-processing logic if needed (audit, cleanup, stats)
    }

    /// <summary>
    /// Handles protocol-level errors occurring on a connection.
    /// Increments error count and logs details.
    /// </summary>
    /// <param name="connection">The connection where the error occurred.</param>
    /// <param name="exception">The exception thrown.</param>
    protected override void OnConnectionError(IConnection connection, System.Exception exception)
    {
        base.OnConnectionError(connection, exception);

        InstanceManager.Instance.GetExistingInstance<ILogger>()?
                                .Error($"[AutoX.{nameof(AutoXProtocol)}:{nameof(OnConnectionError)}] Protocol error id={connection.ID}: {exception.Message}");
    }
}