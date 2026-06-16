// Copyright (c) 2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Reflection;
using Microsoft.Extensions.Logging;
using Nalix.Abstractions;
using Nalix.Abstractions.Networking;
using Nalix.Abstractions.Networking.Packets;
using Nalix.Abstractions.Networking.Sessions;
using Nalix.Codec.DataFrames;
using Nalix.Environment.Configuration;
using Nalix.Environment.Configuration.Binding;
using Nalix.Environment.Memory;
using Nalix.Framework.Injection;
using Nalix.Framework.Memory.Buffers;
using Nalix.Framework.Memory.Objects;
using Nalix.Hosting.Internal;
using Nalix.Network.Connections;
using Nalix.Network.Listeners.Udp;
using Nalix.Runtime.Dispatching;
using Nalix.Runtime.Routing;

namespace Nalix.Hosting;

/// <summary>
/// Builds a <see cref="NetworkApplication"/> using Microsoft-style fluent configuration.
/// </summary>
public sealed class NetworkApplicationBuilder : INetworkApplicationBuilder
{
    #region Fields

    internal readonly HostingBuilderContext _state;

    #endregion Fields

    #region Constructors

    internal NetworkApplicationBuilder(HostingBuilderContext state) => _state = state ?? throw new ArgumentNullException(nameof(state));

    #endregion Constructors

    #region Configuration Methods

    /// <inheritdoc />
    public INetworkApplicationBuilder Configure<TOptions>(Action<TOptions> configure)
        where TOptions : ConfigurationLoader, new()
    {
        ArgumentNullException.ThrowIfNull(configure);

        _state.Options.Add(new OptionsConfiguration(
            typeof(TOptions),
            apply: () =>
            {
                TOptions options = ConfigurationManager.Instance.Get<TOptions>();
                configure(options);

                if (options is IValidatableConfiguration validatable)
                {
                    validatable.Validate();
                }
            }));

        return this;
    }

    /// <inheritdoc />
    public INetworkApplicationBuilder ConfigureLogging(ILogger logger)
    {
        ArgumentNullException.ThrowIfNull(logger);

        InstanceManager.Instance.Register<ILogger>(logger);
        _state.Logger = logger;

        Bootstrap.DiagnosticChannel = new DiagnosticChannel(logger);
        Bootstrap.DiagnosticChannel.Subscribe();

        return this;
    }

    /// <inheritdoc />
    public INetworkApplicationBuilder ConfigureConnectionHub(IConnectionHub connectionHub)
    {
        ArgumentNullException.ThrowIfNull(connectionHub);

        _state.HasCustomConnectionHub = true;
        InstanceManager.Instance.Register<IConnectionHub>(connectionHub);

        if (connectionHub is ConnectionHub concrete)
        {
            InstanceManager.Instance.Register<ConnectionHub>(concrete);
        }

        return this;
    }

    /// <inheritdoc />
    public INetworkApplicationBuilder ConfigureSessionService(ISessionService sessionService)
    {
        ArgumentNullException.ThrowIfNull(sessionService);
        InstanceManager.Instance.Register<ISessionService>(sessionService);
        return this;
    }

    /// <inheritdoc />
    public INetworkApplicationBuilder ConfigureSessionStore(ISessionStore sessionStore)
    {
        ArgumentNullException.ThrowIfNull(sessionStore);
        InstanceManager.Instance.Register<ISessionStore>(sessionStore);
        return this;
    }

    /// <inheritdoc />
    public INetworkApplicationBuilder ConfigureSessionFactory(ISessionFactory sessionFactory)
    {
        ArgumentNullException.ThrowIfNull(sessionFactory);
        InstanceManager.Instance.Register<ISessionFactory>(sessionFactory);
        return this;
    }


    /// <inheritdoc />
    public INetworkApplicationBuilder ConfigureBufferPoolManager(IBufferPoolManager manager)
    {
        ArgumentNullException.ThrowIfNull(manager);

        _state.HasCustomBufferPoolManager = true;
        InstanceManager.Instance.Register<IBufferPoolManager>(manager);

        if (manager is BufferPoolManager concrete)
        {
            InstanceManager.Instance.Register<BufferPoolManager>(concrete);
        }

        BufferLease.ByteArrayPool.Configure(manager);

        return this;
    }

    /// <summary>
    /// Explicitly registers a <see cref="IObjectPoolManager"/> instance to be used by the application.
    /// </summary>
    /// <param name="manager">The manager instance to use.</param>
    /// <returns>The current builder instance.</returns>
    public INetworkApplicationBuilder ConfigureObjectPoolManager(IObjectPoolManager manager)
    {
        ArgumentNullException.ThrowIfNull(manager);
        InstanceManager.Instance.Register<IObjectPoolManager>(manager);

        if (manager is ObjectPoolManager concrete)
        {
            InstanceManager.Instance.Register<ObjectPoolManager>(concrete);
        }

        BufferLease.Configure(manager);
        PacketRegistry.Configure(manager);

        return this;
    }

    /// <inheritdoc />
    public INetworkApplicationBuilder ConfigureCertificate(string certificatePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(certificatePath);
        _state.IdentityCertificatePath = certificatePath;
        return this;
    }

    /// <inheritdoc />
    public INetworkApplicationBuilder ConfigureDispatchOptions(Action<PacketDispatchOptions<IPacket>> configure)
    {
        ArgumentNullException.ThrowIfNull(configure);

        _state.PacketDispatchOptionsConfigurators.Add(configure);
        return this;
    }

    /// <inheritdoc />
    public INetworkApplicationBuilder ConfigureDispatch(Func<Action<PacketDispatchOptions<IPacket>>, IPacketDispatch> factory)
    {
        ArgumentNullException.ThrowIfNull(factory);
        _state.CustomDispatchFactory = factory;
        return this;
    }

    #endregion Configuration Methods

    #region APIs

    /// <inheritdoc />
    public INetworkApplicationBuilder MapHandlers<
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors | DynamicallyAccessedMemberTypes.PublicMethods)] THandler>()
        where THandler : class
    {
#pragma warning disable CA2263 // Factory is Func<object>; generic overload not applicable
        _state.Handlers.Add(new HandlerDescriptor(
            typeof(THandler),
            () => InstanceManager.Instance.CreateInstanceWithInjection(typeof(THandler))));
#pragma warning restore CA2263

        return this;
    }

    /// <inheritdoc />
    public INetworkApplicationBuilder MapHandlers<
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicMethods)] THandler>(Func<THandler> factory) where THandler : class
    {
        ArgumentNullException.ThrowIfNull(factory);

        _state.Handlers.Add(new HandlerDescriptor(
            typeof(THandler),
            () => factory()));

        return this;
    }

    /// <inheritdoc />
    public INetworkApplicationBuilder MapHandlers(
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors | DynamicallyAccessedMemberTypes.PublicMethods)] Type controllerType)
    {
        ArgumentNullException.ThrowIfNull(controllerType);

        _state.Handlers.Add(new HandlerDescriptor(
            controllerType,
            () => InstanceManager.Instance.CreateInstanceWithInjection(controllerType)));

        return this;
    }

    /// <inheritdoc />
    /// <inheritdoc />
    public IProtocolBindingBuilder ListenTcp<
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] TProtocol>()
        where TProtocol : class, IProtocol
    {
        ProtocolBindingBuilder builder = new(this);

        _state.TcpBindings.Add(new TcpProtocolBinding(
            typeof(TProtocol),
            dispatch => builder.Factory is not null
                ? builder.Factory(dispatch)
                : CreateProtocol<TProtocol>(dispatch),
            Port: null,
            BindingBuilder: builder));

        return builder;
    }

    /// <inheritdoc />
    public IProtocolBindingBuilder ListenUdp<
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] TProtocol>()
        where TProtocol : class, IProtocol
    {
        ProtocolBindingBuilder builder = new(this);

        _state.UdpBindings.Add(new UdpProtocolBinding(
            typeof(TProtocol),
            dispatch => builder.Factory is not null
                ? builder.Factory(dispatch)
                : CreateProtocol<TProtocol>(dispatch),
            Port: null,
            Authentication: null,
            BindingBuilder: builder));

        return builder;
    }

    /// <inheritdoc />
    public IWebSocketBindingBuilder ListenWebSocket<
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] TProtocol>()
        where TProtocol : class, IProtocol
    {
        WebSocketBindingBuilder builder = new(this);

        _state.WebSocketBindings.Add(new WebSocketProtocolBinding(
            typeof(TProtocol),
            dispatch => builder.Factory is not null
                ? builder.Factory(dispatch)
                : CreateProtocol<TProtocol>(dispatch),
            Port: null,
            Path: null,
            BindingBuilder: builder));

        return builder;
    }

    /// <inheritdoc />
    public NetworkApplication Build()
    {
        ServiceRegistrar.RegisterPacketRegistry();

        // Apply options eagerly so that EnsureSessionServiceRegistered
        // can read SessionStoreOptions.Enabled before handler factories
        // are captured in the dispatch closure.
        ApplyOptions(_state);

        IPacketDispatch DispatchFactory() => CreatePacketDispatch(_state);

        List<Func<IPacketDispatch, ListenerBinding>> serverFactories = [];

        foreach (TcpProtocolBinding registration in _state.TcpBindings)
        {
            serverFactories.Add(dispatch =>
            {
                IConnectionHub hub = InstanceManager.Instance.GetExistingInstance<IConnectionHub>()
                    ?? throw new InvalidOperationException("IConnectionHub is not registered. Call ConfigureConnectionHub or ensure Build() is invoked.");

                IProtocol protocol = registration.Factory(dispatch);

                ushort? port = registration.Port;

                if (registration.BindingBuilder is ProtocolBindingBuilder tcpBuilder)
                {
                    port = tcpBuilder.Port ?? port;
                }

                IListener listener;

                listener = port.HasValue
                    ? new TcpServerListener(port.Value, protocol, hub)
                    : new TcpServerListener(protocol, hub);

                return new ListenerBinding(listener, protocol, registration.ProtocolType, NetworkTransport.TCP);
            });
        }

        foreach (UdpProtocolBinding registration in _state.UdpBindings)
        {
            serverFactories.Add(dispatch =>
            {
                IConnectionHub hub = InstanceManager.Instance.GetExistingInstance<IConnectionHub>()
                    ?? throw new InvalidOperationException("IConnectionHub is not registered. Call ConfigureConnectionHub or ensure Build() is invoked.");
                IProtocol protocol = registration.Factory(dispatch);

                ushort? port = registration.Port;
                OperatingMode mode = OperatingMode.Server;
                Func<IConnection, System.Net.EndPoint, ReadOnlySpan<byte>, bool>? authen = registration.Authentication;

                if (registration.BindingBuilder is ProtocolBindingBuilder udpBuilder)
                {
                    mode = udpBuilder.Mode;
                    port = udpBuilder.Port ?? port;
                    authen = udpBuilder.Authen ?? authen;
                }

                IListener listener;

                if (mode == OperatingMode.Passthrough)
                {
                    if (authen is not null)
                    {
                        throw new InvalidOperationException(
                            "UDP passthrough framing (TransportFraming.None) does not support " +
                            "Nalix authentication hooks. The protocol layer is responsible for " +
                            "its own authentication when using passthrough mode.");
                    }

                    listener = port.HasValue
                        ? new UdpPassthroughListener(port.Value, protocol, hub)
                        : new UdpPassthroughListener(protocol, hub);
                }
                else
                {
                    listener = authen is not null
                        ? (port.HasValue
                            ? new UdpServerListener(port.Value, protocol, hub, authen)
                            : new UdpServerListener(protocol, hub, authen))
                        : (port.HasValue
                            ? new UdpServerListener(port.Value, protocol, hub)
                            : new UdpServerListener(protocol, hub));
                }

                return new ListenerBinding(listener, protocol, registration.ProtocolType, NetworkTransport.UDP);
            });
        }

        foreach (WebSocketProtocolBinding registration in _state.WebSocketBindings)
        {
            serverFactories.Add(dispatch =>
            {
                IConnectionHub hub = InstanceManager.Instance.GetExistingInstance<IConnectionHub>()
                    ?? throw new InvalidOperationException("IConnectionHub is not registered. Call ConfigureConnectionHub or ensure Build() is invoked.");
                IProtocol protocol = registration.Factory(dispatch);

                ushort? port = registration.Port;
                string? path = registration.Path;

                if (registration.BindingBuilder is WebSocketBindingBuilder wsBuilder)
                {
                    port = wsBuilder.Port ?? port;
                    path = wsBuilder.Path ?? path;
                }

                WebSocketServerListener listener = (port.HasValue && path is not null)
                    ? new(port.Value, path, protocol, hub)
                    : new(protocol, hub);

                return new ListenerBinding(listener, protocol, registration.ProtocolType, NetworkTransport.WEBSOCKET);
            });
        }

        return new NetworkApplication(_state.Logger, PrepareCallbacks, DispatchFactory, serverFactories);

        void PrepareCallbacks()
        {
            // Options already applied above; re-apply is idempotent.
            ApplyOptions(_state);

            ServiceRegistrar.RegisterLogger(_state);
            ServiceRegistrar.RegistererConnectionHub(_state);
            ServiceRegistrar.RegistererBufferPoolManager(_state);
        }
    }

    #endregion APIs

    #region Factory Methods

    /// <summary>
    /// Creates a protocol instance using the most appropriate constructor.
    /// If the protocol type has a constructor accepting <see cref="IPacketDispatch"/>,
    /// the provided dispatch instance is injected. Otherwise, the parameterless constructor is used.
    /// </summary>
    /// <typeparam name="TProtocol">The protocol type to instantiate.</typeparam>
    /// <param name="dispatch">The packet dispatch instance to inject if the protocol supports it.</param>
    /// <returns>A new protocol instance.</returns>
    private static IProtocol CreateProtocol<
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] TProtocol>(
        IPacketDispatch dispatch)
        where TProtocol : class, IProtocol
    {
        ArgumentNullException.ThrowIfNull(dispatch);

        Type protocolType = typeof(TProtocol);

        ConstructorInfo? dispatchConstructor = protocolType
            .GetConstructors(BindingFlags.Instance | BindingFlags.Public)
            .FirstOrDefault(static constructor =>
            {
                ParameterInfo[] parameters = constructor.GetParameters();
                return parameters.Length == 1 && typeof(IPacketDispatch).IsAssignableFrom(parameters[0].ParameterType);
            });

        if (dispatchConstructor is not null)
        {
            return (IProtocol)InstanceManager.Instance.CreateInstance(protocolType, dispatch);
        }

        return (IProtocol)InstanceManager.Instance.CreateInstance(protocolType);
    }

    [System.Diagnostics.CodeAnalysis.UnconditionalSuppressMessage("Trimming", "IL2072",
        Justification = "HandlerDescriptor.HandlerType carries DynamicallyAccessedMembers(PublicMethods) on its record parameter. " +
            "The trimmer cannot always propagate DAM through record property getters.")]
    private static IPacketDispatch CreatePacketDispatch(HostingBuilderContext state)
    {
        ArgumentNullException.ThrowIfNull(state);

        void ConfigureOptions(PacketDispatchOptions<IPacket> dispatchOptions)
        {
            for (int i = 0; i < state.PacketDispatchOptionsConfigurators.Count; i++)
            {
                state.PacketDispatchOptionsConfigurators[i](dispatchOptions);
            }

            foreach (HandlerDescriptor registration in ResolveHandlerRegistrations(state))
            {
                // IL2072: HandlerDescriptor.HandlerType carries DAM(PublicMethods) on its record
                // parameter, but the trimmer cannot always propagate the annotation through
                // record property getters. The annotation is correct and the type is preserved.
                ServiceRegistrar.RegisterHandler(dispatchOptions, registration.HandlerType, registration.Factory);
            }
        }

        if (state.CustomDispatchFactory != null)
        {
            return state.CustomDispatchFactory(ConfigureOptions);
        }

        return new PacketDispatchChannel(ConfigureOptions);
    }

    // AOT-safe: assembly scanning (Assembly.GetTypes) has been removed.
    // Handlers are registered explicitly via MapHandlers<T>() / MapHandlers(Type)
    // or discovered at compile time via source-generated PacketHandlerRegistry.
    private static IEnumerable<HandlerDescriptor> ResolveHandlerRegistrations(HostingBuilderContext state) => state.Handlers;

    private static void ApplyOptions(HostingBuilderContext state)
    {
        for (int i = 0; i < state.Options.Count; i++)
        {
            state.Options[i].Apply();
        }
    }

    #endregion Factory Methods
}
