// Copyright (c) 2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Reflection;
using Microsoft.Extensions.Logging;
using Nalix.Common.Networking;
using Nalix.Common.Networking.Packets;
using Nalix.Framework.Configuration.Binding;
using Nalix.Network.Routing;

namespace Nalix.Network.Hosting;

/// <summary>
/// Configures a <see cref="NetworkHost"/> using a fluent builder API.
/// </summary>
public interface INetworkBuilder
{
    /// <summary>
    /// Sets the logger instance used by the hosted Nalix runtime.
    /// </summary>
    /// <param name="logger">The logger to register into the Nalix runtime.</param>
    /// <returns>The current builder instance.</returns>
    INetworkBuilder UseLogger(ILogger logger);

    /// <summary>
    /// Configures a Nalix options object before the host starts.
    /// </summary>
    /// <typeparam name="TOptions">The configuration type to mutate.</typeparam>
    /// <param name="configure">The callback used to configure the options instance.</param>
    /// <returns>The current builder instance.</returns>
    INetworkBuilder Configure<TOptions>(Action<TOptions> configure)
        where TOptions : ConfigurationLoader, new();

    /// <summary>
    /// Adds packet types discovered from the specified assembly.
    /// </summary>
    /// <param name="assembly">The assembly to scan for packet types.</param>
    /// <param name="requirePacketAttribute">
    /// <see langword="true"/> to include only packet types marked with <see cref="PacketAttribute"/>;
    /// otherwise, all concrete packet types are considered.
    /// </param>
    /// <returns>The current builder instance.</returns>
    INetworkBuilder AddPacketsFromAssembly(Assembly assembly, bool requirePacketAttribute = false);

    /// <summary>
    /// Adds packet types discovered from the assembly that contains <typeparamref name="TMarker"/>.
    /// </summary>
    /// <typeparam name="TMarker">A marker type used to resolve the target assembly.</typeparam>
    /// <param name="requirePacketAttribute">
    /// <see langword="true"/> to include only packet types marked with <see cref="PacketAttribute"/>;
    /// otherwise, all concrete packet types are considered.
    /// </param>
    /// <returns>The current builder instance.</returns>
    INetworkBuilder AddPacketsFromAssemblyContaining<TMarker>(bool requirePacketAttribute = false);

    /// <summary>
    /// Adds packet controller types discovered from the specified assembly.
    /// </summary>
    /// <param name="assembly">The assembly to scan for packet controllers.</param>
    /// <returns>The current builder instance.</returns>
    INetworkBuilder AddPacketHandlersFromAssembly(Assembly assembly);

    /// <summary>
    /// Adds packet controller types discovered from the assembly that contains <typeparamref name="TMarker"/>.
    /// </summary>
    /// <typeparam name="TMarker">A marker type used to resolve the target assembly.</typeparam>
    /// <returns>The current builder instance.</returns>
    INetworkBuilder AddPacketHandlersFromAssemblyContaining<TMarker>();

    /// <summary>
    /// Adds a packet controller type using the default Nalix activator.
    /// </summary>
    /// <typeparam name="THandler">The packet controller type to register.</typeparam>
    /// <returns>The current builder instance.</returns>
    INetworkBuilder AddPacketHandler<THandler>() where THandler : class;

    /// <summary>
    /// Adds a packet controller type using an explicit factory.
    /// </summary>
    /// <typeparam name="THandler">The packet controller type to register.</typeparam>
    /// <param name="factory">The factory used to create controller instances.</param>
    /// <returns>The current builder instance.</returns>
    INetworkBuilder AddPacketHandler<THandler>(Func<THandler> factory) where THandler : class;

    /// <summary>
    /// Adds a packet metadata provider using the default Nalix activator.
    /// </summary>
    /// <typeparam name="TProvider">The metadata provider type to register.</typeparam>
    /// <returns>The current builder instance.</returns>
    INetworkBuilder AddPacketMetadataProvider<TProvider>()
        where TProvider : class, IPacketMetadataProvider;

    /// <summary>
    /// Adds a packet metadata provider using an explicit factory.
    /// </summary>
    /// <typeparam name="TProvider">The metadata provider type to register.</typeparam>
    /// <param name="factory">The factory used to create provider instances.</param>
    /// <returns>The current builder instance.</returns>
    INetworkBuilder AddPacketMetadataProvider<TProvider>(Func<TProvider> factory)
        where TProvider : class, IPacketMetadataProvider;

    /// <summary>
    /// Configures the packet dispatcher used by the host.
    /// </summary>
    /// <param name="configure">The callback used to configure dispatcher options.</param>
    /// <returns>The current builder instance.</returns>
    INetworkBuilder ConfigurePacketDispatcher(Action<PacketDispatchOptions<IPacket>> configure);

    /// <summary>
    /// Adds a TCP server protocol using the default Nalix activator.
    /// </summary>
    /// <typeparam name="TProtocol">The protocol type to host.</typeparam>
    /// <returns>The current builder instance.</returns>
    INetworkBuilder AddTcpServer<TProtocol>() where TProtocol : class, IProtocol;

    /// <summary>
    /// Adds a TCP server protocol using an explicit factory.
    /// </summary>
    /// <typeparam name="TProtocol">The protocol type to host.</typeparam>
    /// <param name="factory">The factory used to create the protocol instance.</param>
    /// <returns>The current builder instance.</returns>
    INetworkBuilder AddTcpServer<TProtocol>(Func<IPacketDispatch, TProtocol> factory)
        where TProtocol : class, IProtocol;

    /// <summary>
    /// Builds a <see cref="NetworkHost"/> from the current configuration.
    /// </summary>
    /// <returns>The configured <see cref="NetworkHost"/> instance.</returns>
    NetworkHost Build();
}
