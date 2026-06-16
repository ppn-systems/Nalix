// Copyright (c) 2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Diagnostics.CodeAnalysis;
using Microsoft.Extensions.Logging;
using Nalix.Abstractions;
using Nalix.Abstractions.Networking;
using Nalix.Abstractions.Networking.Packets;
using Nalix.Abstractions.Networking.Sessions;
using Nalix.Environment.Configuration.Binding;
using Nalix.Framework.Memory.Objects;
using Nalix.Runtime.Dispatching;
using Nalix.Runtime.Routing;

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
    /// Configures the options for the packet dispatcher.
    /// </summary>
    /// <param name="configure">The callback used to configure dispatcher options.</param>
    /// <returns>The current builder instance.</returns>
    INetworkApplicationBuilder ConfigureDispatchOptions(Action<PacketDispatchOptions<IPacket>> configure);

    /// <summary>
    /// Configures the host to use a custom packet dispatcher.
    /// </summary>
    /// <param name="factory">A factory delegate that receives the compiled dispatch options configuration and returns an <see cref="IPacketDispatch"/>.</param>
    /// <returns>The current builder instance.</returns>
    INetworkApplicationBuilder ConfigureDispatch(Func<Action<PacketDispatchOptions<IPacket>>, IPacketDispatch> factory);

    /// <summary>
    /// Sets the logger instance used by the hosted Nalix runtime.
    /// </summary>
    /// <param name="logger">The logger to register into the Nalix runtime.</param>
    /// <returns>The current builder instance.</returns>
    INetworkApplicationBuilder UseLogger(ILogger logger);

    /// <summary>
    /// Sets the <see cref="IConnectionHub"/> instance used by the hosted Nalix runtime.
    /// </summary>
    /// <param name="connectionHub">The connection hub to register into the Nalix runtime.</param>
    /// <returns>The current builder instance.</returns>
    INetworkApplicationBuilder UseConnectionHub(IConnectionHub connectionHub);

    /// <summary>
    /// Explicitly registers a <see cref="IBufferPoolManager"/> instance to be used by the application.
    /// </summary>
    /// <param name="manager">The manager instance to use.</param>
    /// <returns>The current builder instance.</returns>
    INetworkApplicationBuilder UseBufferPoolManager(IBufferPoolManager manager);

    /// <summary>
    /// Explicitly registers a <see cref="ObjectPoolManager"/> instance to be used by the application.
    /// </summary>
    /// <param name="manager">The manager instance to use.</param>
    /// <returns>The current builder instance.</returns>
    INetworkApplicationBuilder UseObjectPoolManager(IObjectPoolManager manager);

    /// <summary>
    /// Adds a packet controller type using the default Nalix activator.
    /// </summary>
    /// <typeparam name="THandler">The packet controller type to register.</typeparam>
    /// <returns>The current builder instance.</returns>
    INetworkApplicationBuilder MapHandlers<
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors | DynamicallyAccessedMemberTypes.PublicMethods)] THandler>()
        where THandler : class;

    /// <summary>
    /// Adds a packet controller type using an explicit factory.
    /// </summary>
    /// <typeparam name="THandler">The packet controller type to register.</typeparam>
    /// <param name="factory">The factory used to create controller instances.</param>
    /// <returns>The current builder instance.</returns>
    INetworkApplicationBuilder MapHandlers<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicMethods)] THandler>(Func<THandler> factory) where THandler : class;

    /// <summary>
    /// Adds a packet controller type directly (primarily for static classes).
    /// </summary>
    /// <param name="controllerType">The type of the controller to register.</param>
    /// <returns>The current builder instance.</returns>
    INetworkApplicationBuilder MapHandlers(
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors | DynamicallyAccessedMemberTypes.PublicMethods)] Type controllerType);

    /// <summary>
    /// Binds a TCP protocol using a fluent builder for port and factory configuration.
    /// </summary>
    /// <typeparam name="TProtocol">The protocol type to host.</typeparam>
    /// <returns>A fluent builder to configure the binding.</returns>
    IProtocolBindingBuilder ListenTcp<
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] TProtocol>()
        where TProtocol : class, IProtocol;

    /// <summary>
    /// Binds a UDP protocol using a fluent builder for port, factory, and authentication configuration.
    /// </summary>
    /// <typeparam name="TProtocol">The protocol type to host.</typeparam>
    /// <returns>A fluent builder to configure the binding.</returns>
    IProtocolBindingBuilder ListenUdp<
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] TProtocol>()
        where TProtocol : class, IProtocol;

    /// <summary>
    /// Binds a WebSocket protocol using a fluent builder for port, path, and factory configuration.
    /// </summary>
    /// <typeparam name="TProtocol">The protocol type to host.</typeparam>
    /// <returns>A fluent builder to configure the WebSocket binding.</returns>
    IWebSocketBindingBuilder ListenWebSocket<
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] TProtocol>()
        where TProtocol : class, IProtocol;
}
