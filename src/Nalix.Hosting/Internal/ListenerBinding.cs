// Copyright (c) 2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System;
using Nalix.Abstractions.Networking;

#if DEBUG
[assembly: System.Runtime.CompilerServices.InternalsVisibleTo("Nalix.Network.Tests")]
[assembly: System.Runtime.CompilerServices.InternalsVisibleTo("Nalix.Network.Benchmarks")]
#endif

namespace Nalix.Hosting.Internal;

/// <summary>
/// Represents a binding between a network <see cref="IListener"/> and
/// its associated <see cref="IProtocol"/> implementation.
/// </summary>
/// <remarks>
/// This structure is used internally by the hosting infrastructure to
/// describe how incoming connections or datagrams are handled,
/// including the transport type and protocol metadata.
/// </remarks>
internal readonly struct ListenerBinding
{
    /// <summary>
    /// Gets a value indicating whether the listener uses UDP transport.
    /// </summary>
    public bool IsUdp { get; }

    /// <summary>
    /// Gets the network listener responsible for accepting connections
    /// or receiving datagrams.
    /// </summary>
    public IListener Listener { get; }

    /// <summary>
    /// Gets the protocol instance used to process incoming data.
    /// </summary>
    public IProtocol Protocol { get; }

    /// <summary>
    /// Gets the runtime type of the associated protocol.
    /// </summary>
    /// <remarks>
    /// This value is cached to avoid repeated reflection calls when
    /// resolving protocol handlers.
    /// </remarks>
    public Type ProtocolType { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="ListenerBinding"/> struct.
    /// </summary>
    /// <param name="listener">
    /// The listener responsible for receiving network traffic.
    /// </param>
    /// <param name="protocol">
    /// The protocol used to handle received messages.
    /// </param>
    /// <param name="protocolType">
    /// The concrete runtime type of the protocol.
    /// </param>
    /// <param name="isUdp">
    /// <see langword="true"/> if the listener uses UDP transport;
    /// otherwise, <see langword="false"/>.
    /// </param>
    public ListenerBinding(IListener listener, IProtocol protocol, Type protocolType, bool isUdp)
    {
        this.IsUdp = isUdp;
        this.Listener = listener;
        this.Protocol = protocol;
        this.ProtocolType = protocolType;
    }
}

