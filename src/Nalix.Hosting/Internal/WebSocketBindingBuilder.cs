// Copyright (c) 2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System;
using Nalix.Abstractions.Networking;
using Nalix.Runtime.Dispatching;

namespace Nalix.Hosting.Internal;

internal sealed class WebSocketBindingBuilder : IWebSocketBindingBuilder
{
    private readonly INetworkApplicationBuilder _parent;

    internal ushort? Port { get; private set; }
    internal string? Path { get; private set; }
    internal Func<IPacketDispatch, IProtocol>? Factory { get; private set; }

    internal WebSocketBindingBuilder(INetworkApplicationBuilder parent) => _parent = parent;

    public IWebSocketBindingBuilder OnPort(ushort port)
    {
        this.Port = port;
        return this;
    }

    public IWebSocketBindingBuilder WithPath(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        this.Path = path;
        return this;
    }

    public IWebSocketBindingBuilder WithFactory(Func<IPacketDispatch, IProtocol> factory)
    {
        ArgumentNullException.ThrowIfNull(factory);
        this.Factory = factory;
        return this;
    }

    public INetworkApplicationBuilder Bind() => _parent;
}
