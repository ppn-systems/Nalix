// Copyright (c) 2025-2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using Nalix.Common.Networking;
using Nalix.Network.Protocols;
using Nalix.Runtime.Dispatching;

namespace Nalix.Network.Examples.Protocols;

/// <summary>
/// Example protocol that shows the minimum responsibilities for a custom server protocol.
/// It acts as the bridge between the TCP listener and the packet dispatch pipeline.
/// </summary>
public sealed class ExamplePacketProtocol : Protocol
{
    private readonly IPacketDispatch _packetDispatch;

    /// <summary>
    /// Creates a protocol instance that forwards packets into the supplied dispatch pipeline.
    /// </summary>
    public ExamplePacketProtocol(IPacketDispatch packetDispatch)
    {
        _packetDispatch = packetDispatch ?? throw new ArgumentNullException(nameof(packetDispatch));
        // The sample keeps accepting new connections and lets packet handlers decide
        // how long a session should stay open.
        this.IsAccepting = true;
        this.KeepConnectionOpen = true;
    }

    /// <summary>
    /// Runs when the listener accepts a new connection.
    /// </summary>
    public override void OnAccept(IConnection connection, CancellationToken cancellationToken = default) => base.OnAccept(connection, cancellationToken);

    /// <summary>
    /// Processes an inbound packet for a live connection.
    /// </summary>
    public override void ProcessMessage(object? sender, IConnectEventArgs args)
    {
        ArgumentNullException.ThrowIfNull(args);

        if (args.Lease is null)
        {
            return;
        }

        // This log line helps readers see when the protocol hands control over to packet routing.
        Console.WriteLine($"[ExamplePacketProtocol] Packet received from connection {args.Connection.ID}.");

        // The dispatch pipeline owns validation, middleware, and routing concerns.
        // The protocol should stay thin and only coordinate the flow.
        _packetDispatch.HandlePacket(args.Lease, args.Connection);

        // The protocol event args are disposable and should be released after processing.
    }

    /// <summary>
    /// Validates a connection before it is fully accepted.
    /// </summary>
    protected override bool ValidateConnection(IConnection connection) => true;

    /// <summary>
    /// Hook for post-processing after a packet has been handled.
    /// </summary>
    protected override void OnPostProcess(IConnectEventArgs args)
    {
        // Intentionally empty. Keep the sample focused on the main flow.
    }

    /// <summary>
    /// Handles protocol-level errors.
    /// </summary>
    protected override void OnConnectionError(IConnection connection, Exception exception) => base.OnConnectionError(connection, exception);
}
