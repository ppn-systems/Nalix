// Copyright (c) 2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Reflection;
using Microsoft.Extensions.Logging;
using Nalix.Common.Networking;
using Nalix.Framework.Configuration.Binding;
using Nalix.Framework.Injection;
using Nalix.Network.Hosting.Internal;
using Nalix.Network.Routing;

namespace Nalix.Network.Hosting;

/// <summary>
/// Builds a <see cref="NetworkHost"/> using Microsoft-style fluent configuration.
/// </summary>
public sealed class NetworkBuilder : INetworkBuilder
{
    private readonly NetworkBuilderState _state;

    internal NetworkBuilder(NetworkBuilderState state)
        => _state = state ?? throw new ArgumentNullException(nameof(state));

    /// <inheritdoc />
    public INetworkBuilder UseLogger(ILogger logger)
    {
        _state.Logger = logger ?? throw new ArgumentNullException(nameof(logger));
        return this;
    }

    /// <inheritdoc />
    public INetworkBuilder Configure<TOptions>(Action<TOptions> configure)
        where TOptions : ConfigurationLoader, new()
    {
        ArgumentNullException.ThrowIfNull(configure);

        _state.OptionRegistrations.Add(new OptionRegistration(
            typeof(TOptions),
            options => configure((TOptions)options)));

        return this;
    }

    /// <inheritdoc />
    public INetworkBuilder AddPackets(Assembly assembly, bool requirePacketAttribute = false)
    {
        ArgumentNullException.ThrowIfNull(assembly);

        _state.PacketAssemblies.Add(new PacketAssemblyRegistration(assembly, requirePacketAttribute));
        return this;
    }

    /// <inheritdoc />
    public INetworkBuilder AddPackets<TMarker>(bool requirePacketAttribute = false)
        => this.AddPackets(typeof(TMarker).Assembly, requirePacketAttribute);

    /// <inheritdoc />
    public INetworkBuilder AddHandlers(Assembly assembly)
    {
        ArgumentNullException.ThrowIfNull(assembly);

        _ = _state.HandlerAssemblies.Add(assembly);
        return this;
    }

    /// <inheritdoc />
    public INetworkBuilder AddHandlers<TMarker>()
        => this.AddHandlers(typeof(TMarker).Assembly);

    /// <inheritdoc />
    public INetworkBuilder AddHandler<THandler>() where THandler : class
    {
        _state.HandlerRegistrations.Add(new HandlerRegistration(
            typeof(THandler),
            () => InstanceManager.Instance.CreateInstance(typeof(THandler))));

        return this;
    }

    /// <inheritdoc />
    public INetworkBuilder AddHandler<THandler>(Func<THandler> factory) where THandler : class
    {
        ArgumentNullException.ThrowIfNull(factory);

        _state.HandlerRegistrations.Add(new HandlerRegistration(
            typeof(THandler),
            () => factory()));

        return this;
    }

    /// <inheritdoc />
    public INetworkBuilder AddMetadataProvider<TProvider>()
        where TProvider : class, IPacketMetadataProvider
    {
        _state.MetadataProviderRegistrations.Add(new MetadataProviderRegistration(
            typeof(TProvider),
            () => (IPacketMetadataProvider)InstanceManager.Instance.CreateInstance(typeof(TProvider))));

        return this;
    }

    /// <inheritdoc />
    public INetworkBuilder AddMetadataProvider<TProvider>(Func<TProvider> factory)
        where TProvider : class, IPacketMetadataProvider
    {
        ArgumentNullException.ThrowIfNull(factory);

        _state.MetadataProviderRegistrations.Add(new MetadataProviderRegistration(
            typeof(TProvider),
            () => factory()));

        return this;
    }

    /// <inheritdoc />
    public INetworkBuilder ConfigureDispatcher(Action<PacketDispatchOptions<Nalix.Common.Networking.Packets.IPacket>> configure)
    {
        ArgumentNullException.ThrowIfNull(configure);

        _state.PacketDispatchConfigurations.Add(configure);
        return this;
    }

    /// <inheritdoc />
    public INetworkBuilder AddTcp<TProtocol>() where TProtocol : class, IProtocol
    {
        _state.TcpServerRegistrations.Add(new TcpServerRegistration(
            typeof(TProtocol),
            dispatch => NetworkHost.CreateProtocol(typeof(TProtocol), dispatch)));

        return this;
    }

    /// <inheritdoc />
    public INetworkBuilder AddTcp<TProtocol>(Func<IPacketDispatch, TProtocol> factory)
        where TProtocol : class, IProtocol
    {
        ArgumentNullException.ThrowIfNull(factory);

        _state.TcpServerRegistrations.Add(new TcpServerRegistration(
            typeof(TProtocol),
            dispatch => factory(dispatch)));

        return this;
    }

    /// <inheritdoc />
    public INetworkBuilder AddUdp<TProtocol>() where TProtocol : class, IProtocol
    {
        _state.UdpServerRegistrations.Add(new UdpServerRegistration(
            typeof(TProtocol),
            dispatch => NetworkHost.CreateProtocol(typeof(TProtocol), dispatch)));

        return this;
    }

    /// <inheritdoc />
    public INetworkBuilder AddUdp<TProtocol>(Func<IPacketDispatch, TProtocol> factory)
        where TProtocol : class, IProtocol
    {
        ArgumentNullException.ThrowIfNull(factory);

        _state.UdpServerRegistrations.Add(new UdpServerRegistration(
            typeof(TProtocol),
            dispatch => factory(dispatch)));

        return this;
    }

    /// <inheritdoc />
    public NetworkHost Build() => new(_state);
}
