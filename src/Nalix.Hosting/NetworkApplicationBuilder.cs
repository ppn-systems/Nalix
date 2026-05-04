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
using Nalix.Codec.DataFrames;
using Nalix.Codec.Memory;
using Nalix.Environment.Configuration;
using Nalix.Environment.Configuration.Binding;
using Nalix.Framework.Injection;
using Nalix.Framework.Memory.Buffers;
using Nalix.Framework.Memory.Objects;
using Nalix.Hosting.Internal;
using Nalix.Network.Connections;
using Nalix.Network.Routing;
using Nalix.Runtime.Dispatching;
using Nalix.Runtime.Handlers;

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
        InstanceManager.Instance.Register<ILogger>(logger);
        _state.Logger = logger ?? throw new ArgumentNullException(nameof(logger));

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
    public INetworkApplicationBuilder ConfigurePacketRegistry(IPacketRegistry packetRegistry)
    {
        ArgumentNullException.ThrowIfNull(packetRegistry);

        _state.PacketRegistryOverride = packetRegistry;
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
    public INetworkApplicationBuilder ConfigureDispatch(Action<PacketDispatchOptions<IPacket>> configure)
    {
        ArgumentNullException.ThrowIfNull(configure);

        _state.PacketDispatchConfigurators.Add(configure);
        return this;
    }

    #endregion Configuration Methods

    /// <inheritdoc />
    public INetworkApplicationBuilder AddPacket(Assembly assembly, bool requirePacketAttribute = false)
    {
        ArgumentNullException.ThrowIfNull(assembly);

        _state.PacketAssemblies.Add(new PacketAssemblyDescriptor(assembly, requirePacketAttribute));
        return this;
    }

    /// <inheritdoc />
    public INetworkApplicationBuilder AddPacket(string assemblyPath, bool requirePacketAttribute = false)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(assemblyPath);

        _state.PacketAssemblyPaths.Add(new PacketAssemblyPathDescriptor(assemblyPath, requirePacketAttribute));
        return this;
    }

    /// <inheritdoc />
    public INetworkApplicationBuilder AddPacket<TMarker>(bool requirePacketAttribute = false)
        => this.AddPacket(typeof(TMarker).Assembly, requirePacketAttribute);

    /// <inheritdoc />
    public INetworkApplicationBuilder AddPacketNamespace(string packetNamespace, bool recursive = true)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(packetNamespace);

        _state.PacketNamespaces.Add(new PacketNamespaceDescriptor(packetNamespace, recursive));
        return this;
    }

    /// <inheritdoc />
    public INetworkApplicationBuilder AddPacketNamespace(string assemblyPath, string packetNamespace, bool recursive = true)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(assemblyPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(packetNamespace);

        _state.PacketNamespaces.Add(new PacketNamespaceDescriptor(packetNamespace, recursive, assemblyPath));
        return this;
    }

    /// <inheritdoc />
    public INetworkApplicationBuilder AddHandlers(Assembly assembly)
    {
        ArgumentNullException.ThrowIfNull(assembly);

        _ = _state.HandlerAssemblies.Add(assembly);
        return this;
    }

    /// <inheritdoc />
    public INetworkApplicationBuilder AddHandlers<TMarker>() => this.AddHandlers(typeof(TMarker).Assembly);

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
    public INetworkApplicationBuilder AddTcp<TProtocol>() where TProtocol : class, IProtocol
    {
        _state.TcpBindings.Add(new TcpProtocolBinding(
            typeof(TProtocol),
            dispatch => CreateProtocol(typeof(TProtocol), dispatch)));

        return this;
    }

    /// <inheritdoc />
    public INetworkApplicationBuilder AddTcp<TProtocol>(Func<IPacketDispatch, TProtocol> factory)
        where TProtocol : class, IProtocol
    {
        ArgumentNullException.ThrowIfNull(factory);

        _state.TcpBindings.Add(new TcpProtocolBinding(
            typeof(TProtocol),
            dispatch => factory(dispatch)));

        return this;
    }

    /// <inheritdoc />
    public INetworkApplicationBuilder AddUdp<TProtocol>() where TProtocol : class, IProtocol
    {
        _state.UdpBindings.Add(new UdpProtocolBinding(
            typeof(TProtocol),
            dispatch => CreateProtocol(typeof(TProtocol), dispatch)));

        return this;
    }

    /// <inheritdoc />
    public INetworkApplicationBuilder AddUdp<TProtocol>(Func<IConnection, System.Net.EndPoint, ReadOnlySpan<byte>, bool> authen)
        where TProtocol : class, IProtocol
    {
        _state.UdpBindings.Add(new UdpProtocolBinding(
            typeof(TProtocol),
            dispatch => CreateProtocol(typeof(TProtocol), dispatch),
            Authentication: authen));

        return this;
    }

    /// <inheritdoc />
    public INetworkApplicationBuilder AddUdp<TProtocol>(Func<IPacketDispatch, TProtocol> factory)
        where TProtocol : class, IProtocol
    {
        ArgumentNullException.ThrowIfNull(factory);

        _state.UdpBindings.Add(new UdpProtocolBinding(
            typeof(TProtocol),
            dispatch => factory(dispatch)));

        return this;
    }

    /// <inheritdoc />
    public INetworkApplicationBuilder AddUdp<TProtocol>(Func<IPacketDispatch, TProtocol> factory, Func<IConnection, System.Net.EndPoint, ReadOnlySpan<byte>, bool> authen)
        where TProtocol : class, IProtocol
    {
        ArgumentNullException.ThrowIfNull(factory);

        _state.UdpBindings.Add(new UdpProtocolBinding(
            typeof(TProtocol),
            dispatch => factory(dispatch),
            Authentication: authen));

        return this;
    }

    /// <inheritdoc />
    public INetworkApplicationBuilder AddTcp<TProtocol>(ushort port) where TProtocol : class, IProtocol
    {
        _state.TcpBindings.Add(new TcpProtocolBinding(
            typeof(TProtocol),
            dispatch => CreateProtocol(typeof(TProtocol), dispatch),
            port));

        return this;
    }

    /// <inheritdoc />
    public INetworkApplicationBuilder AddTcp<TProtocol>(ushort port, Func<IPacketDispatch, TProtocol> factory)
        where TProtocol : class, IProtocol
    {
        ArgumentNullException.ThrowIfNull(factory);

        _state.TcpBindings.Add(new TcpProtocolBinding(
            typeof(TProtocol),
            dispatch => factory(dispatch),
            port));

        return this;
    }

    /// <inheritdoc />
    public INetworkApplicationBuilder AddUdp<TProtocol>(ushort port) where TProtocol : class, IProtocol
    {
        _state.UdpBindings.Add(new UdpProtocolBinding(
            typeof(TProtocol),
            dispatch => CreateProtocol(typeof(TProtocol), dispatch),
            port));

        return this;
    }

    /// <inheritdoc />
    public INetworkApplicationBuilder AddUdp<TProtocol>(ushort port, Func<IConnection, System.Net.EndPoint, ReadOnlySpan<byte>, bool> authen)
        where TProtocol : class, IProtocol
    {
        _state.UdpBindings.Add(new UdpProtocolBinding(
            typeof(TProtocol),
            dispatch => CreateProtocol(typeof(TProtocol), dispatch),
            port,
            authen));

        return this;
    }

    /// <inheritdoc />
    public INetworkApplicationBuilder AddUdp<TProtocol>(ushort port, Func<IPacketDispatch, TProtocol> factory)
        where TProtocol : class, IProtocol
    {
        ArgumentNullException.ThrowIfNull(factory);

        _state.UdpBindings.Add(new UdpProtocolBinding(
            typeof(TProtocol),
            dispatch => factory(dispatch),
            port));

        return this;
    }

    /// <inheritdoc />
    public INetworkApplicationBuilder AddUdp<TProtocol>(ushort port, Func<IPacketDispatch, TProtocol> factory, Func<IConnection, System.Net.EndPoint, ReadOnlySpan<byte>, bool> authen)
        where TProtocol : class, IProtocol
    {
        ArgumentNullException.ThrowIfNull(factory);

        _state.UdpBindings.Add(new UdpProtocolBinding(
            typeof(TProtocol),
            dispatch => factory(dispatch),
            port,
            authen));

        return this;
    }

    /// <inheritdoc />
    public NetworkApplication Build()
    {
        bool metadataRegistered = false;

        void PrepareCallbacks()
        {
            RegisterLogger(_state);
            ApplyOptions(_state);
            RegisterPacketRegistry(_state);

            this.EnsureConnectionHubRegistered();
            this.EnsureBufferPoolManagerRegistered();

            if (_state.IdentityCertificatePath is not null)
            {
                HandshakeHandlers.SetCertificatePath(_state.IdentityCertificatePath);
            }
            else
            {
                HandshakeHandlers.Initialize();
            }

            if (!metadataRegistered)
            {
                RegisterMetadataProviders(_state);
                metadataRegistered = true;
            }
        }

        IPacketDispatch DispatchFactory() => CreatePacketDispatch(_state);

        List<Func<IPacketDispatch, ListenerBinding>> serverFactories = [];

        foreach (TcpProtocolBinding registration in _state.TcpBindings)
        {
            serverFactories.Add(dispatch =>
            {
                IConnectionHub hub = InstanceManager.Instance.GetExistingInstance<IConnectionHub>()!;
                IProtocol protocol = registration.Factory(dispatch);
                TcpServerListener listener = registration.Port.HasValue
                    ? new(registration.Port.Value, protocol, hub)
                    : new(protocol, hub);

                return new ListenerBinding(listener, protocol, registration.ProtocolType, isUdp: false);
            });
        }

        foreach (UdpProtocolBinding registration in _state.UdpBindings)
        {
            serverFactories.Add(dispatch =>
            {
                IConnectionHub hub = InstanceManager.Instance.GetExistingInstance<IConnectionHub>()!;
                IProtocol protocol = registration.Factory(dispatch);
                UdpServerListener listener = registration.Authentication != null
                    ? (registration.Port.HasValue
                        ? new(registration.Port.Value, protocol, hub, registration.Authentication)
                        : new(protocol, hub, registration.Authentication))
                    : (registration.Port.HasValue
                        ? new(registration.Port.Value, protocol, hub)
                        : new(protocol, hub));

                return new ListenerBinding(listener, protocol, registration.ProtocolType, isUdp: true);
            });
        }

        List<IActivatableAsync> hostedServices = [];
        foreach (Func<IActivatableAsync> factory in _state.HostedServices)
        {
            hostedServices.Add(factory());
        }

        return new NetworkApplication(_state.Logger, PrepareCallbacks, DispatchFactory, serverFactories, hostedServices);
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

    internal static PacketDispatchChannel CreatePacketDispatch(HostingBuilderContext state)
    {
        ArgumentNullException.ThrowIfNull(state);

        return new PacketDispatchChannel(dispatchOptions =>
        {
            _ = dispatchOptions.WithLogging(state.Logger);

            for (int i = 0; i < state.PacketDispatchConfigurators.Count; i++)
            {
                state.PacketDispatchConfigurators[i](dispatchOptions);
            }

            foreach (HandlerDescriptor registration in ResolveHandlerRegistrations(state))
            {
                _ = s_registerHandlerMethod.MakeGenericMethod(registration.HandlerType)
                                           .Invoke(obj: null, parameters: [dispatchOptions, registration.Factory]);
            }
        });
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

    private static void RegisterPacketRegistry(HostingBuilderContext state)
        => InstanceManager.Instance.Register(CreatePacketRegistry(state));

    private static void RegisterMetadataProviders(HostingBuilderContext state)
    {
        for (int i = 0; i < state.MetadataProviders.Count; i++)
        {
            PacketMetadataProviderDescriptor registration = state.MetadataProviders[i];
            PacketMetadataProviders.Register(registration.Factory());
        }
    }

    internal static IPacketRegistry CreatePacketRegistry(HostingBuilderContext state)
    {
        ArgumentNullException.ThrowIfNull(state);

        if (state.PacketRegistryOverride is not null)
        {
            return state.PacketRegistryOverride;
        }

        PacketRegistryFactory factory = new();

        for (int i = 0; i < state.PacketAssemblies.Count; i++)
        {
            PacketAssemblyDescriptor registration = state.PacketAssemblies[i];
            _ = factory.RegisterAllPackets(registration.Assembly, registration.RequirePacketAttribute);
        }

        for (int i = 0; i < state.PacketAssemblyPaths.Count; i++)
        {
            PacketAssemblyPathDescriptor registration = state.PacketAssemblyPaths[i];
            _ = factory.RegisterPacketAssembly(registration.AssemblyPath, registration.RequirePacketAttribute);
        }

        bool includeCurrentDomain = false;
        for (int i = 0; i < state.PacketNamespaces.Count; i++)
        {
            PacketNamespaceDescriptor registration = state.PacketNamespaces[i];
            if (string.IsNullOrWhiteSpace(registration.AssemblyPath))
            {
                includeCurrentDomain = true;
                continue;
            }

            _ = factory.IncludeAssembly(registration.AssemblyPath);
        }

        if (includeCurrentDomain)
        {
            _ = factory.IncludeCurrentDomain();
        }

        for (int i = 0; i < state.PacketNamespaces.Count; i++)
        {
            PacketNamespaceDescriptor registration = state.PacketNamespaces[i];
            _ = registration.Recursive
                ? factory.IncludeNamespaceRecursive(registration.PacketNamespace)
                : factory.IncludeNamespace(registration.PacketNamespace);
        }

        foreach (Assembly assembly in state.HandlerAssemblies)
        {
            _ = factory.RegisterAllPackets(assembly, requireAttribute: false);
        }

        return factory.CreateCatalog();
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
