// Copyright (c) 2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using Nalix.Common.Networking;
using Nalix.Network.Protocols;
using Nalix.Runtime.Dispatching;

namespace Nalix.Chat.Infrastructure.Protocols;

/// <summary>
/// Thin protocol that forwards inbound packets to the dispatch pipeline.
/// </summary>
public sealed class ChatPacketProtocol : Protocol
{
    private readonly IPacketDispatch _packetDispatch;

    /// <summary>
    /// Initializes the protocol with packet dispatch pipeline.
    /// </summary>
    public ChatPacketProtocol(IPacketDispatch packetDispatch)
    {
        _packetDispatch = packetDispatch ?? throw new ArgumentNullException(nameof(packetDispatch));
        this.IsAccepting = true;
        this.KeepConnectionOpen = true;
    }

    /// <inheritdoc/>
    public override void ProcessMessage(object? sender, IConnectEventArgs args)
    {
        ArgumentNullException.ThrowIfNull(args);

        if (args.Lease is null)
        {
            return;
        }

        _packetDispatch.HandlePacket(args.Lease, args.Connection);
    }

    /// <inheritdoc/>
    protected override bool ValidateConnection(IConnection connection)
    {
        ArgumentNullException.ThrowIfNull(connection);
        return true;
    }

    /// <inheritdoc/>
    protected override void OnPostProcess(IConnectEventArgs args)
    {
    }
}
