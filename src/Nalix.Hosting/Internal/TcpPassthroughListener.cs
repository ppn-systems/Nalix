// Copyright (c) 2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System;
using Nalix.Abstractions.Networking;
using Nalix.Network.Listeners.Tcp;

namespace Nalix.Hosting.Internal;

/// <summary>
/// Provides a TCP listener that bypasses Nalix frame decoding and dispatches
/// received data directly to the configured protocol.
/// </summary>
/// <remarks>
/// This listener is intended for pass-through protocols such as Minecraft or
/// custom TCP protocols that perform their own framing, validation, or decoding.
/// </remarks>
internal sealed class TcpPassthroughListener : TcpListenerBase
{
    /// <summary>
    /// Initializes a new instance of the <see cref="TcpPassthroughListener"/> class.
    /// </summary>
    /// <param name="protocol">The protocol used to process incoming connection data.</param>
    /// <param name="hub">The connection hub used to manage active connections.</param>
    public TcpPassthroughListener(IProtocol protocol, IConnectionHub hub) : base(protocol, hub) { }

    /// <summary>
    /// Initializes a new instance of the <see cref="TcpPassthroughListener"/> class.
    /// </summary>
    /// <param name="port">The TCP port on which the listener accepts connections.</param>
    /// <param name="protocol">The protocol used to process incoming connection data.</param>
    /// <param name="hub">The connection hub used to manage active connections.</param>
    public TcpPassthroughListener(ushort port, IProtocol protocol, IConnectionHub hub) : base(port, protocol, hub) { }

    /// <summary>
    /// Dispatches received connection data directly to the configured protocol.
    /// </summary>
    /// <param name="sender">The event source that raised the connection event.</param>
    /// <param name="args">The connection event data to process.</param>
    /// <remarks>
    /// No Nalix frame decoding, sequence validation, or security-layer processing
    /// is performed by this listener.
    /// </remarks>
    public override void ProcessFrame(object? sender, IConnectEventArgs args)
    {
        ArgumentNullException.ThrowIfNull(args);

        this.Protocol.ProcessMessage(sender, args);
    }
}
