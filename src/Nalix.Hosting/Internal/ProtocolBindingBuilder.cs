// Copyright (c) 2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Net;
using Nalix.Abstractions.Networking;
using Nalix.Runtime.Dispatching;

namespace Nalix.Hosting.Internal;

internal sealed class ProtocolBindingBuilder : IProtocolBindingBuilder
{
    private readonly INetworkApplicationBuilder _parent;

    internal ushort? Port { get; private set; }
    internal TransportFraming? Framing { get; private set; }
    internal Func<IPacketDispatch, IProtocol>? Factory { get; private set; }
    internal Func<IConnection, EndPoint, ReadOnlySpan<byte>, bool>? Authen { get; private set; }

    internal ProtocolBindingBuilder(INetworkApplicationBuilder parent) => _parent = parent;

    public IProtocolBindingBuilder OnPort(ushort port)
    {
        this.Port = port;
        return this;
    }

    public IProtocolBindingBuilder WithFraming(TransportFraming framing)
    {
        this.Framing = framing;
        return this;
    }

    public IProtocolBindingBuilder WithFactory(Func<IPacketDispatch, IProtocol> factory)
    {
        ArgumentNullException.ThrowIfNull(factory);
        this.Factory = factory;
        return this;
    }

    public IProtocolBindingBuilder WithAuthentication(Func<IConnection, EndPoint, ReadOnlySpan<byte>, bool> authen)
    {
        ArgumentNullException.ThrowIfNull(authen);
        this.Authen = authen;
        return this;
    }

    public INetworkApplicationBuilder Bind() => _parent;
}
