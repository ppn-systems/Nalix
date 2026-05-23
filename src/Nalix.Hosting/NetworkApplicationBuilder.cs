// Copyright (c) 2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Reflection;
using Microsoft.Extensions.Logging;
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
using Nalix.Network.Routing;
using Nalix.Runtime.Dispatching;
using Nalix.Runtime.Handlers;
using Nalix.Runtime.Sessions;

namespace Nalix.Hosting;

/// <summary>
/// Builds a <see cref="NetworkApplication"/> using Microsoft-style fluent configuration.
/// </summary>
public sealed class NetworkApplicationBuilder : INetworkApplicationBuilder
{
    #region Fields

    private static readonly MethodInfo s_applyOptionsMethod;
    private static readonly MethodInfo s_registerHandlerMethod;

    private readonly HostingBuilderContext _state;

    #endregion Fields

    #region Constructors

    static NetworkApplicationBuilder()
    {
        s_applyOptionsMethod = typeof(NetworkApplicationBuilder).GetMethod(nameof(ApplyOptionsCore), BindingFlags.NonPublic | BindingFlags.Static)
            ?? throw new MissingMethodException(typeof(NetworkApplicationBuilder).FullName, nameof(ApplyOptionsCore));

        s_registerHandlerMethod = typeof(NetworkApplicationBuilder).GetMethod(nameof(RegisterHandlerCore), BindingFlags.NonPublic | BindingFlags.Static)
            ?? throw new MissingMethodException(typeof(NetworkApplicationBuilder).FullName, nameof(RegisterHandlerCore));
    }

    internal NetworkApplicationBuilder(HostingBuilderContext state)
    {
        _state = state ?? throw new ArgumentNullException(nameof(state));
        _ = this.AddHandler<SessionHandlers>()
                .AddHandler<HandshakeHandlers>()
                .AddHandler<KeyExchangeHandlers>()
                .AddHandler<SystemControlHandlers>();
    }

    #endregion Constructors

    #region Configuration Methods

    /// <inheritdoc />
    public INetworkApplicationBuilder Configure<TOptions>(Action<TOptions> configure)
        where TOptions : ConfigurationLoader, new()
    {
        ArgumentNullException.ThrowIfNull(configure);

        _state.Options.Add(new OptionsConfiguration(
            typeof(TOptions),
            options => configure((TOptions)options)));

        return this;
    }

    /// <inheritdoc />
    public INetworkApplicationBuilder ConfigureLogging(ILogger logger)
    {
        ArgumentNullException.ThrowIfNull(logger);

        InstanceManager.Instance.Register<ILogger>(logger);
        _state.Logger = logger;

        return this;
    }

    /// <inheritdoc />
    public INetworkApplicationBuilder ConfigureConnectionHub(IConnectionHub connectionHub)
    {
        ArgumentNullException.ThrowIfNull(connectionHub);

        _state.HasCustomConnectionHub = true;
        InstanceManager.Instance.Register<IConnectionHub>(connectionHub);
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
    public INetworkApplicationBuilder ConfigureBufferPoolManager(BufferPoolManager manager)
    {
        ArgumentNullException.ThrowIfNull(manager);

        _state.HasCustomBufferPoolManager = true;
        InstanceManager.Instance.Register<BufferPoolManager>(manager);
        BufferLease.ByteArrayPool.Configure(manager);

        return this;
    }

    /// <summary>
    /// Explicitly registers a <see cref="ObjectPoolManager"/> instance to be used by the application.
    /// </summary>
    /// <param name="manager">The manager instance to use.</param>
    /// <returns>The current builder instance.</returns>
    public INetworkApplicationBuilder ConfigureObjectPoolManager(ObjectPoolManager manager)
    {
        ArgumentNullException.ThrowIfNull(manager);
        InstanceManager.Instance.Register<ObjectPoolManager>(manager);

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

    /// <inheritdoc />
    public INetworkApplicationBuilder ScanHandlers(Assembly assembly)
    {
        ArgumentNullException.ThrowIfNull(assembly);

        _ = _state.HandlerAssemblies.Add(assembly);
        return this;
    }

    /// <inheritdoc />
    public INetworkApplicationBuilder ScanHandlers<TMarker>() => this.ScanHandlers(typeof(TMarker).Assembly);

    /// <inheritdoc />
    public INetworkApplicationBuilder AddHandler<THandler>() where THandler : class
    {
        _state.Handlers.Add(new HandlerDescriptor(
            typeof(THandler),
            () => InstanceManager.Instance.CreateInstance(typeof(THandler))));

        return this;
    }

    /// <inheritdoc />
    public INetworkApplicationBuilder AddHandler<THandler>(Func<THandler> factory) where THandler : class
    {
        ArgumentNullException.ThrowIfNull(factory);

        _state.Handlers.Add(new HandlerDescriptor(
            typeof(THandler),
            () => factory()));

        return this;
    }

    /// <inheritdoc />
    public INetworkApplicationBuilder AddMetadataProvider<TProvider>()
        where TProvider : class, IPacketMetadataProvider
    {
        _state.MetadataProviders.Add(new PacketMetadataProviderDescriptor(
            typeof(TProvider),
            () => (IPacketMetadataProvider)InstanceManager.Instance.CreateInstance(typeof(TProvider))));

        return this;
    }

    /// <inheritdoc />
    public INetworkApplicationBuilder AddMetadataProvider<TProvider>(Func<TProvider> factory)
        where TProvider : class, IPacketMetadataProvider
    {
        ArgumentNullException.ThrowIfNull(factory);

        _state.MetadataProviders.Add(new PacketMetadataProviderDescriptor(
            typeof(TProvider),
            () => factory()));

        return this;
    }

    /// <inheritdoc />
    public IProtocolBindingBuilder BindTcp<TProtocol>() where TProtocol : class, IProtocol
    {
        ProtocolBindingBuilder builder = new(this);

        _state.TcpBindings.Add(new TcpProtocolBinding(
            typeof(TProtocol),
            dispatch => builder.Factory is not null
                ? builder.Factory(dispatch)
                : CreateProtocol(typeof(TProtocol), dispatch),
            Port: null,
            BindingBuilder: builder));

        return builder;
    }

    /// <inheritdoc />
    public IProtocolBindingBuilder BindUdp<TProtocol>() where TProtocol : class, IProtocol
    {
        ProtocolBindingBuilder builder = new(this);

        _state.UdpBindings.Add(new UdpProtocolBinding(
            typeof(TProtocol),
            dispatch => builder.Factory is not null
                ? builder.Factory(dispatch)
                : CreateProtocol(typeof(TProtocol), dispatch),
            Port: null,
            Authentication: null,
            BindingBuilder: builder));

        return builder;
    }

    /// <inheritdoc />
    public IWebSocketBindingBuilder BindWebSocket<TProtocol>() where TProtocol : class, IProtocol
    {
        WebSocketBindingBuilder builder = new(this);

        _state.WebSocketBindings.Add(new WebSocketProtocolBinding(
            typeof(TProtocol),
            dispatch => builder.Factory is not null
                ? builder.Factory(dispatch)
                : CreateProtocol(typeof(TProtocol), dispatch),
            Port: null,
            Path: null,
            BindingBuilder: builder));

        return builder;
    }

    /// <inheritdoc />
    public NetworkApplication Build()
    {
        RegisterPacketRegistry();

        void PrepareCallbacks()
        {
            ApplyOptions(_state);
            RegisterLogger(_state);

            this.EnsureConnectionHubRegistered();
            this.EnsureSessionServiceRegistered();
            this.EnsureBufferPoolManagerRegistered();

            if (_state.IdentityCertificatePath is not null)
            {
                HandshakeHandlers.SetCertificatePath(_state.IdentityCertificatePath);
            }
            else
            {
                HandshakeHandlers.Initialize();
            }

            RegisterMetadataProviders(_state);
        }

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

                TcpServerListener listener = port.HasValue
                    ? new(port.Value, protocol, hub)
                    : new(protocol, hub);

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
                Func<IConnection, System.Net.EndPoint, ReadOnlySpan<byte>, bool>? authen = registration.Authentication;

                if (registration.BindingBuilder is ProtocolBindingBuilder udpBuilder)
                {
                    port = udpBuilder.Port ?? port;
                    authen = udpBuilder.Authen ?? authen;
                }

                UdpServerListener listener = authen != null
                    ? (port.HasValue
                        ? new(port.Value, protocol, hub, authen)
                        : new(protocol, hub, authen))
                    : (port.HasValue
                        ? new(port.Value, protocol, hub)
                        : new(protocol, hub));

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
    }

    #region Factory Methods

    internal static IProtocol CreateProtocol(Type protocolType, IPacketDispatch dispatch)
    {
        ArgumentNullException.ThrowIfNull(protocolType);
        ArgumentNullException.ThrowIfNull(dispatch);

        ConstructorInfo? dispatchConstructor = protocolType
            .GetConstructors(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
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

    internal static IPacketDispatch CreatePacketDispatch(HostingBuilderContext state)
    {
        ArgumentNullException.ThrowIfNull(state);

        void ConfigureOptions(PacketDispatchOptions<IPacket> dispatchOptions)
        {
            _ = dispatchOptions.WithLogging(state.Logger);

            for (int i = 0; i < state.PacketDispatchOptionsConfigurators.Count; i++)
            {
                state.PacketDispatchOptionsConfigurators[i](dispatchOptions);
            }

            foreach (HandlerDescriptor registration in ResolveHandlerRegistrations(state))
            {
                _ = s_registerHandlerMethod.MakeGenericMethod(registration.HandlerType)
                                           .Invoke(obj: null, parameters: [dispatchOptions, registration.Factory]);
            }
        }

        if (state.CustomDispatchFactory != null)
        {
            return state.CustomDispatchFactory(ConfigureOptions);
        }

        return new PacketDispatchChannel(ConfigureOptions);
    }

    private static IEnumerable<HandlerDescriptor> ResolveHandlerRegistrations(HostingBuilderContext state)
    {
        Dictionary<Type, HandlerDescriptor> handlers = [];

        for (int i = 0; i < state.Handlers.Count; i++)
        {
            HandlerDescriptor registration = state.Handlers[i];
            handlers[registration.HandlerType] = registration;
        }

        foreach (Assembly assembly in state.HandlerAssemblies)
        {
            Type[] types;
            try
            {
                types = assembly.GetTypes();
            }
            catch (ReflectionTypeLoadException ex)
            {
                types = [.. ex.Types.Where(static type => type is not null).Cast<Type>()];
            }

            for (int i = 0; i < types.Length; i++)
            {
                Type type = types[i];
                if (!type.IsClass || type.IsAbstract)
                {
                    continue;
                }

                if (type.GetCustomAttribute<PacketControllerAttribute>(inherit: false) is null)
                {
                    continue;
                }

                _ = handlers.TryAdd(type, new HandlerDescriptor(
                    type,
                    () => InstanceManager.Instance.CreateInstance(type)));
            }
        }

        return handlers.Values;
    }

    [SuppressMessage("Reliability", "CA2000:Dispose objects before losing scope",
        Justification = "On successful registration InstanceManager owns the ConnectionHub lifetime; registration failure disposes the local instance.")]
    private void EnsureConnectionHubRegistered()
    {
        if (_state.HasCustomConnectionHub)
        {
            return;
        }

        ConnectionHub hub = new(logger: _state.Logger);
        try
        {
            InstanceManager.Instance.Register<IConnectionHub>(hub);
        }
        catch
        {
            hub.Dispose();
            throw;
        }
    }

    private void EnsureSessionServiceRegistered()
    {
        ISessionService? service = InstanceManager.Instance.GetExistingInstance<ISessionService>();

        if (service == null)
        {
            ISessionFactory? factory = InstanceManager.Instance.GetExistingInstance<ISessionFactory>();
            ISessionStore? store = InstanceManager.Instance.GetExistingInstance<ISessionStore>();

#pragma warning disable CA2000 // Dispose objects before losing scope
            service = new SessionService(factory, store);
#pragma warning restore CA2000 // Dispose objects before losing scope
            try
            {
                InstanceManager.Instance.Register<ISessionService>(service);
            }
            catch
            {
                if (service is IDisposable disposable)
                {
                    disposable.Dispose();
                }
                throw;
            }
        }

        if (InstanceManager.Instance.GetExistingInstance<SessionPersistenceObserver>() == null)
        {
            IConnectionHub? hub = InstanceManager.Instance.GetExistingInstance<IConnectionHub>();
            if (hub is not null)
            {
#pragma warning disable CA2000 // Dispose objects before losing scope
                SessionPersistenceObserver observer = new(hub, service);
#pragma warning restore CA2000 // Dispose objects before losing scope
                InstanceManager.Instance.Register<SessionPersistenceObserver>(observer);
            }
        }
    }

    [SuppressMessage("Reliability", "CA2000:Dispose objects before losing scope",
        Justification = "On successful registration InstanceManager and BufferLease.ByteArrayPool own the manager lifetime; failure disposes the local instance.")]
    private void EnsureBufferPoolManagerRegistered()
    {
        if (_state.HasCustomBufferPoolManager)
        {
            return;
        }

        BufferPoolManager manager = new();
        try
        {
            InstanceManager.Instance.Register<BufferPoolManager>(manager);
            BufferLease.ByteArrayPool.Configure(manager);
        }
        catch
        {
            manager.Dispose();
            throw;
        }
    }


    private static void RegisterLogger(HostingBuilderContext state) => InstanceManager.Instance.Register<ILogger>(state.Logger);

    private static void ApplyOptions(HostingBuilderContext state)
    {
        for (int i = 0; i < state.Options.Count; i++)
        {
            OptionsConfiguration registration = state.Options[i];
            _ = s_applyOptionsMethod.MakeGenericMethod(registration.OptionsType)
                                    .Invoke(obj: null, parameters: [registration]);
        }
    }

    private static void ApplyOptionsCore<TOptions>(OptionsConfiguration registration)
        where TOptions : ConfigurationLoader, new()
    {
        TOptions options = ConfigurationManager.Instance.Get<TOptions>();
        registration.Apply(options);

        MethodInfo? validateMethod = typeof(TOptions).GetMethod("Validate", BindingFlags.Instance | BindingFlags.Public);
        _ = (validateMethod?.Invoke(options, parameters: null));
    }

    private static void RegisterPacketRegistry()
    {
        if (PacketRegistry.IsBuilt)
        {
            return;
        }

        PacketRegistry.Build();
    }

    private static void RegisterMetadataProviders(HostingBuilderContext state)
    {
        for (int i = 0; i < state.MetadataProviders.Count; i++)
        {
            PacketMetadataProviderDescriptor registration = state.MetadataProviders[i];
            PacketMetadataProviders.Register(registration.Factory());
        }
    }


    private static void RegisterHandlerCore<THandler>(PacketDispatchOptions<IPacket> dispatchOptions, Func<object> factory)
        where THandler : class
    {
        ArgumentNullException.ThrowIfNull(dispatchOptions);
        ArgumentNullException.ThrowIfNull(factory);

        _ = dispatchOptions.WithHandler(() => (THandler)factory());
    }

    #endregion
}
