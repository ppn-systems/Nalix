// Copyright (c) 2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Reflection;
using Microsoft.Extensions.Logging;
using Nalix.Abstractions.Networking;
using Nalix.Abstractions.Networking.Packets;
using Nalix.Environment.Configuration.Binding;
using Nalix.Framework.Memory.Buffers;
using Nalix.Framework.Memory.Objects;
using Nalix.Network.Routing;
using Nalix.Runtime.Dispatching;

namespace Nalix.Hosting;

/// <summary>
/// Configures a <see cref="NetworkApplication"/> using a fluent builder API.
/// </summary>
[System.Diagnostics.CodeAnalysis.SuppressMessage("Naming", "CA1716:Identifiers should not match keywords", Justification = "<Pending>")]
public interface INetworkApplicationBuilder
{
    /// <summary>
    /// Builds a <see cref="NetworkApplication"/> from the current configuration.
    /// </summary>
    /// <returns>The configured <see cref="NetworkApplication"/> instance.</returns>
    NetworkApplication Build();

    /// <summary>
    /// Configures a Nalix options object before the host starts.
    /// </summary>
    /// <typeparam name="TOptions">The configuration type to mutate.</typeparam>
    /// <param name="configure">The callback used to configure the options instance.</param>
    /// <returns>The current builder instance.</returns>
    INetworkApplicationBuilder Configure<TOptions>(Action<TOptions> configure) where TOptions : ConfigurationLoader, new();

    /// <summary>
    /// Sets the logger instance used by the hosted Nalix runtime.
    /// </summary>
    /// <param name="logger">The logger to register into the Nalix runtime.</param>
    /// <returns>The current builder instance.</returns>
    INetworkApplicationBuilder ConfigureLogging(ILogger logger);

    /// <summary>
    /// Sets the <see cref="IConnectionHub"/> instance used by the hosted Nalix runtime.
    /// </summary>
    /// <param name="connectionHub">The connection hub to register into the Nalix runtime.</param>
    /// <returns>The current builder instance.</returns>
    INetworkApplicationBuilder ConfigureConnectionHub(IConnectionHub connectionHub);

    /// <summary>
    /// Explicitly registers a <see cref="BufferPoolManager"/> instance to be used by the application.
    /// </summary>
    /// <param name="manager">The manager instance to use.</param>
    /// <returns>The current builder instance.</returns>
    INetworkApplicationBuilder ConfigureBufferPoolManager(BufferPoolManager manager);

    /// <summary>
    /// Explicitly registers a <see cref="ObjectPoolManager"/> instance to be used by the application.
    /// </summary>
    /// <param name="manager">The manager instance to use.</param>
    /// <returns>The current builder instance.</returns>
    INetworkApplicationBuilder ConfigureObjectPoolManager(ObjectPoolManager manager);

    /// <summary>
    /// Configures the server identity certificate path.
    /// </summary>
    /// <param name="certificatePath">The absolute path to the certificate file.</param>
    /// <returns>The current builder instance.</returns>
    INetworkApplicationBuilder ConfigureCertificate(string certificatePath);

    /// <summary>
    /// Configures a pre-built packet registry instead of hosting auto-discovery.
    /// </summary>
    /// <param name="packetRegistry">The packet registry to use.</param>
    /// <returns>The current builder instance.</returns>
    INetworkApplicationBuilder ConfigurePacketRegistry(IPacketRegistry packetRegistry);

    /// <summary>
    /// Configures the packet dispatcher used by the host.
    /// </summary>
    /// <param name="configure">The callback used to configure dispatcher options.</param>
    /// <returns>The current builder instance.</returns>
    INetworkApplicationBuilder ConfigureDispatch(Action<PacketDispatchOptions<IPacket>> configure);

    /// <summary>
    /// Adds packet types discovered from the specified assembly.
    /// </summary>
    /// <param name="assembly">The assembly to scan for packet types.</param>
    /// <param name="requirePacketAttribute">
    /// <see langword="true"/> to include only packet types marked with <see cref="PacketAttribute"/>;
    /// otherwise, all concrete packet types are considered.
    /// </param>
    /// <returns>The current builder instance.</returns>
    INetworkApplicationBuilder AddPacket(Assembly assembly, bool requirePacketAttribute = false);

    /// <summary>
    /// Adds packet types discovered from the specified assembly path.
    /// </summary>
    /// <param name="assemblyPath">The .dll path to scan for packet types.</param>
    /// <param name="requirePacketAttribute">
    /// <see langword="true"/> to include only packet types marked with <see cref="PacketAttribute"/>;
    /// otherwise, all concrete packet types are considered.
    /// </param>
    /// <returns>The current builder instance.</returns>
    INetworkApplicationBuilder AddPacket(string assemblyPath, bool requirePacketAttribute = false);

    /// <summary>
    /// Adds packet types discovered from the assembly that contains <typeparamref name="TMarker"/>.
    /// </summary>
    /// <typeparam name="TMarker">A marker type used to resolve the target assembly.</typeparam>
    /// <param name="requirePacketAttribute">
    /// <see langword="true"/> to include only packet types marked with <see cref="PacketAttribute"/>;
    /// otherwise, all concrete packet types are considered.
    /// </param>
    /// <returns>The current builder instance.</returns>
    INetworkApplicationBuilder AddPacket<TMarker>(bool requirePacketAttribute = false);

    /// <summary>
    /// Adds packet types discovered by matching packet namespaces in the current AppDomain.
    /// </summary>
    /// <param name="packetNamespace">The namespace to include.</param>
    /// <param name="recursive">
    /// <see langword="true"/> to include child namespaces; otherwise exact namespace only.
    /// </param>
    /// <returns>The current builder instance.</returns>
    INetworkApplicationBuilder AddPacketNamespace(string packetNamespace, bool recursive = true);

    /// <summary>
    /// Adds packet types discovered by matching packet namespaces from one assembly path.
    /// </summary>
    /// <param name="assemblyPath">The .dll path to scan.</param>
    /// <param name="packetNamespace">The namespace to include.</param>
    /// <param name="recursive">
    /// <see langword="true"/> to include child namespaces; otherwise exact namespace only.
    /// </param>
    /// <returns>The current builder instance.</returns>
    INetworkApplicationBuilder AddPacketNamespace(string assemblyPath, string packetNamespace, bool recursive = true);

    /// <summary>
    /// Adds packet controller types discovered from the specified assembly.
    /// </summary>
    /// <param name="assembly">The assembly to scan for packet controllers.</param>
    /// <returns>The current builder instance.</returns>
    INetworkApplicationBuilder AddHandlers(Assembly assembly);

    /// <summary>
    /// Adds packet controller types discovered from the assembly that contains <typeparamref name="TMarker"/>.
    /// </summary>
    /// <typeparam name="TMarker">A marker type used to resolve the target assembly.</typeparam>
    /// <returns>The current builder instance.</returns>
    INetworkApplicationBuilder AddHandlers<TMarker>();

    /// <summary>
    /// Adds a packet controller type using the default Nalix activator.
    /// </summary>
    /// <typeparam name="THandler">The packet controller type to register.</typeparam>
    /// <returns>The current builder instance.</returns>
    INetworkApplicationBuilder AddHandler<THandler>() where THandler : class;

    /// <summary>
    /// Adds a packet controller type using an explicit factory.
    /// </summary>
    /// <typeparam name="THandler">The packet controller type to register.</typeparam>
    /// <param name="factory">The factory used to create controller instances.</param>
    /// <returns>The current builder instance.</returns>
    INetworkApplicationBuilder AddHandler<THandler>(Func<THandler> factory) where THandler : class;

    /// <summary>
    /// Adds a packet metadata provider using the default Nalix activator.
    /// </summary>
    /// <typeparam name="TProvider">The metadata provider type to register.</typeparam>
    /// <returns>The current builder instance.</returns>
    INetworkApplicationBuilder AddMetadataProvider<TProvider>() where TProvider : class, IPacketMetadataProvider;

    /// <summary>
    /// Adds a packet metadata provider using an explicit factory.
    /// </summary>
    /// <typeparam name="TProvider">The metadata provider type to register.</typeparam>
    /// <param name="factory">The factory used to create provider instances.</param>
    /// <returns>The current builder instance.</returns>
    INetworkApplicationBuilder AddMetadataProvider<TProvider>(Func<TProvider> factory) where TProvider : class, IPacketMetadataProvider;

    /// <summary>
    /// Adds a TCP protocol using the default Nalix activator.
    /// </summary>
    /// <typeparam name="TProtocol">The protocol type to host.</typeparam>
    /// <returns>The current builder instance.</returns>
    INetworkApplicationBuilder AddTcp<TProtocol>() where TProtocol : class, IProtocol;

    /// <summary>
    /// Adds a TCP protocol using an explicit factory.
    /// </summary>
    /// <typeparam name="TProtocol">The protocol type to host.</typeparam>
    /// <param name="factory">The factory used to create the protocol instance.</param>
    /// <returns>The current builder instance.</returns>
    INetworkApplicationBuilder AddTcp<TProtocol>(Func<IPacketDispatch, TProtocol> factory) where TProtocol : class, IProtocol;

    /// <summary>
    /// Adds a TCP protocol using the default Nalix activator on a specific port.
    /// </summary>
    /// <typeparam name="TProtocol">The protocol type to host.</typeparam>
    /// <param name="port">The port to listen on.</param>
    /// <returns>The current builder instance.</returns>
    INetworkApplicationBuilder AddTcp<TProtocol>(ushort port) where TProtocol : class, IProtocol;

    /// <summary>
    /// Adds a TCP protocol using an explicit factory on a specific port.
    /// </summary>
    /// <typeparam name="TProtocol">The protocol type to host.</typeparam>
    /// <param name="port">The port to listen on.</param>
    /// <param name="factory">The factory used to create the protocol instance.</param>
    /// <returns>The current builder instance.</returns>
    INetworkApplicationBuilder AddTcp<TProtocol>(ushort port, Func<IPacketDispatch, TProtocol> factory) where TProtocol : class, IProtocol;

    /// <summary>
    /// Adds a UDP protocol using the default Nalix activator.
    /// </summary>
    /// <typeparam name="TProtocol">The protocol type to host.</typeparam>
    /// <returns>The current builder instance.</returns>
    INetworkApplicationBuilder AddUdp<TProtocol>() where TProtocol : class, IProtocol;

    /// <summary>
    /// Adds a UDP protocol using the default Nalix activator and a custom authentication predicate.
    /// </summary>
    /// <typeparam name="TProtocol">The protocol type to host.</typeparam>
    /// <param name="authen">The authentication predicate used to validate incoming datagrams.</param>
    /// <returns>The current builder instance.</returns>
    INetworkApplicationBuilder AddUdp<TProtocol>(Func<IConnection, System.Net.EndPoint, ReadOnlySpan<byte>, bool> authen) where TProtocol : class, IProtocol;

    /// <summary>
    /// Adds a UDP protocol using an explicit factory.
    /// </summary>
    /// <typeparam name="TProtocol">The protocol type to host.</typeparam>
    /// <param name="factory">The factory used to create the protocol instance.</param>
    /// <returns>The current builder instance.</returns>
    INetworkApplicationBuilder AddUdp<TProtocol>(Func<IPacketDispatch, TProtocol> factory) where TProtocol : class, IProtocol;

    /// <summary>
    /// Adds a UDP protocol using an explicit factory and a custom authentication predicate.
    /// </summary>
    /// <typeparam name="TProtocol">The protocol type to host.</typeparam>
    /// <param name="factory">The factory used to create the protocol instance.</param>
    /// <param name="authen">The authentication predicate used to validate incoming datagrams.</param>
    /// <returns>The current builder instance.</returns>
    INetworkApplicationBuilder AddUdp<TProtocol>(Func<IPacketDispatch, TProtocol> factory, Func<IConnection, System.Net.EndPoint, ReadOnlySpan<byte>, bool> authen) where TProtocol : class, IProtocol;

    /// <summary>
    /// Adds a UDP protocol using the default Nalix activator on a specific port.
    /// </summary>
    /// <typeparam name="TProtocol">The protocol type to host.</typeparam>
    /// <param name="port">The port to listen on.</param>
    /// <returns>The current builder instance.</returns>
    INetworkApplicationBuilder AddUdp<TProtocol>(ushort port) where TProtocol : class, IProtocol;

    /// <summary>
    /// Adds a UDP protocol using the default Nalix activator on a specific port with a custom authentication predicate.
    /// </summary>
    /// <typeparam name="TProtocol">The protocol type to host.</typeparam>
    /// <param name="port">The port to listen on.</param>
    /// <param name="authen">The authentication predicate used to validate incoming datagrams.</param>
    /// <returns>The current builder instance.</returns>
    INetworkApplicationBuilder AddUdp<TProtocol>(ushort port, Func<IConnection, System.Net.EndPoint, ReadOnlySpan<byte>, bool> authen) where TProtocol : class, IProtocol;

    /// <summary>
    /// Adds a UDP protocol using an explicit factory on a specific port.
    /// </summary>
    /// <typeparam name="TProtocol">The protocol type to host.</typeparam>
    /// <param name="port">The port to listen on.</param>
    /// <param name="factory">The factory used to create the protocol instance.</param>
    /// <returns>The current builder instance.</returns>
    INetworkApplicationBuilder AddUdp<TProtocol>(ushort port, Func<IPacketDispatch, TProtocol> factory) where TProtocol : class, IProtocol;

    /// <summary>
    /// Adds a UDP protocol using an explicit factory on a specific port with a custom authentication predicate.
    /// </summary>
    /// <typeparam name="TProtocol">The protocol type to host.</typeparam>
    /// <param name="port">The port to listen on.</param>
    /// <param name="factory">The factory used to create the protocol instance.</param>
    /// <param name="authen">The authentication predicate used to validate incoming datagrams.</param>
    /// <returns>The current builder instance.</returns>
    INetworkApplicationBuilder AddUdp<TProtocol>(ushort port, Func<IPacketDispatch, TProtocol> factory, Func<IConnection, System.Net.EndPoint, ReadOnlySpan<byte>, bool> authen) where TProtocol : class, IProtocol;
}
