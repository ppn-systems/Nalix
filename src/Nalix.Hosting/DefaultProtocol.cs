// Copyright (c) 2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System;
using Nalix.Abstractions.Networking;
using Nalix.Network.Protocols;
using Nalix.Runtime.Dispatching;

namespace Nalix.Hosting;

/// <summary>
/// A ready-to-use protocol that forwards all inbound packets to the dispatch pipeline.
/// Use this when your server does not need custom protocol-level logic.
/// </summary>
/// <remarks>
/// <para>
/// Most Nalix servers only need to route packets into the dispatcher.
/// <see cref="DefaultProtocol"/> eliminates the boilerplate of creating a protocol class
/// that simply calls <c>IPacketDispatch.HandlePacket</c>.
/// </para>
/// <para>Usage with the hosting builder:</para>
/// <code>
/// using var app = NetworkApplication.CreateBuilder()
///     .BindTcp&lt;DefaultProtocol&gt;().Bind()
///     .AddHandler&lt;MyHandler&gt;()
///     .Build();
/// </code>
/// </remarks>
public sealed class DefaultProtocol : Protocol
{
    private readonly IPacketDispatch _dispatch;

    /// <summary>
    /// Creates a new <see cref="DefaultProtocol"/> that routes packets into the given dispatch pipeline.
    /// </summary>
    /// <param name="dispatch">The packet dispatcher responsible for routing and handling packets.</param>
    /// <exception cref="ArgumentNullException"><paramref name="dispatch"/> is <see langword="null"/>.</exception>
    public DefaultProtocol(IPacketDispatch dispatch)
    {
        _dispatch = dispatch ?? throw new ArgumentNullException(nameof(dispatch));
        this.IsAccepting = true;
        this.KeepConnectionOpen = true;
    }

    /// <inheritdoc />
    public override void ProcessMessage(object? sender, IConnectEventArgs args)
    {
        ArgumentNullException.ThrowIfNull(args);

        if (args.Lease is null)
        {
            return;
        }

        _dispatch.HandlePacket(args.Lease, args.Connection);
    }

    /// <inheritdoc />
    protected override bool ValidateConnection(IConnection connection) => true;
}
