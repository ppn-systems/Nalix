// Copyright (c) 2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Nalix.Common.Abstractions;
using Nalix.Common.Networking;
using Nalix.Common.Networking.Packets;
using Nalix.Framework.Configuration;
using Nalix.Framework.Configuration.Binding;
using Nalix.Framework.DataFrames;
using Nalix.Framework.Injection;
using Nalix.Network.Hosting.Internal;
using Nalix.Network.Routing;

namespace Nalix.Network.Hosting;

/// <summary>
/// Represents a runnable host for Nalix TCP servers.
/// </summary>
/// <remarks>
/// Use <see cref="CreateBuilder"/> to configure a host instance, then call
/// <see cref="StartAsync(CancellationToken)"/>, <see cref="RunAsync(CancellationToken)"/>,
/// or the lifecycle methods inherited from <see cref="IActivatable"/> and
/// <see cref="IActivatableAsync"/>.
/// </remarks>
public sealed class NetworkHost : IActivatable, IActivatableAsync
{
    #region Static Fields

    private static readonly Action<ILogger, string?, Exception?> s_startedTcpServerMessage =
        LoggerMessage.Define<string?>(
            LogLevel.Information,
            new EventId(1000, nameof(NetworkHost)),
            "Started Nalix TCP server for protocol {ProtocolType}.");

    private static readonly Action<ILogger, string?, Exception?> s_startedUdpServerMessage =
        LoggerMessage.Define<string?>(
            LogLevel.Information,
            new EventId(1004, nameof(NetworkHost)),
            "Started Nalix UDP server for protocol {ProtocolType}.");

    private static readonly Action<ILogger, Exception?> s_stopListenerFailedMessage =
        LoggerMessage.Define(
            LogLevel.Warning,
            new EventId(1001, nameof(NetworkHost)),
            "Failed to stop Nalix listener cleanly.");

    private static readonly Action<ILogger, Exception?> s_disposeProtocolFailedMessage =
        LoggerMessage.Define(
            LogLevel.Warning,
            new EventId(1002, nameof(NetworkHost)),
            "Failed to dispose Nalix protocol cleanly.");

    private static readonly Action<ILogger, Exception?> s_stopDispatcherFailedMessage =
        LoggerMessage.Define(
            LogLevel.Warning,
            new EventId(1003, nameof(NetworkHost)),
            "Failed to stop the Nalix packet dispatcher cleanly.");

    private static readonly MethodInfo s_applyOptionsMethod = typeof(NetworkHost).GetMethod(nameof(ApplyOptionsCore), BindingFlags.NonPublic | BindingFlags.Static)
        ?? throw new MissingMethodException(typeof(NetworkHost).FullName, nameof(ApplyOptionsCore));

    private static readonly MethodInfo s_registerHandlerMethod = typeof(NetworkHost).GetMethod(nameof(RegisterHandlerCore), BindingFlags.NonPublic | BindingFlags.Static)
        ?? throw new MissingMethodException(typeof(NetworkHost).FullName, nameof(RegisterHandlerCore));

    #endregion Static Fields

    #region Fields

    private readonly Lock _gate = new();
    private readonly NetworkBuilderState _state;
    private readonly List<IListener> _listeners = [];
    private readonly List<IProtocol> _protocols = [];

    private bool _isStarted;
    private bool _metadataRegistered;
    private IPacketDispatch? _packetDispatch;

    #endregion Fields

    #region Constructors

    internal NetworkHost(NetworkBuilderState state)
        => _state = state ?? throw new ArgumentNullException(nameof(state));

    #endregion Constructors

    #region APIs

    /// <summary>
    /// Creates a new builder for <see cref="NetworkHost"/>.
    /// </summary>
    /// <returns>A new <see cref="NetworkBuilder"/> instance.</returns>
    public static NetworkBuilder CreateBuilder() => new(new NetworkBuilderState());

    /// <summary>
    /// Starts the host.
    /// </summary>
    /// <param name="cancellationToken">A token that can cancel startup.</param>
    /// <returns>A task that completes when startup has finished.</returns>
    public Task StartAsync(CancellationToken cancellationToken = default)
    {
        lock (_gate)
        {
            if (_isStarted)
            {
                return Task.CompletedTask;
            }

            this.RegisterLogger();
            this.ApplyOptions();
            this.RegisterPacketRegistry();
            this.RegisterMetadataProviders();

            _packetDispatch = CreatePacketDispatch(_state);
            _packetDispatch.Activate(cancellationToken);

            for (int i = 0; i < _state.TcpServerRegistrations.Count; i++)
            {
                TcpServerRegistration registration = _state.TcpServerRegistrations[i];
                IProtocol protocol = registration.Factory(_packetDispatch);
                TcpListenerHost listener = new(protocol);

                _protocols.Add(protocol);
                _listeners.Add(listener);

                listener.Activate(cancellationToken);
                s_startedTcpServerMessage(_state.Logger, registration.ProtocolType.FullName, null);
            }

            for (int i = 0; i < _state.UdpServerRegistrations.Count; i++)
            {
                UdpServerRegistration registration = _state.UdpServerRegistrations[i];
                IProtocol protocol = registration.Factory(_packetDispatch);
                UdpListenerHost listener = new(protocol);

                _protocols.Add(protocol);
                _listeners.Add(listener);

                listener.Activate(cancellationToken);
                s_startedUdpServerMessage(_state.Logger, registration.ProtocolType.FullName, null);
            }

            _isStarted = true;
        }

        return Task.CompletedTask;
    }

    /// <summary>
    /// Stops the host.
    /// </summary>
    /// <param name="cancellationToken">A token that can cancel shutdown.</param>
    /// <returns>A task that completes when shutdown has finished.</returns>
    public Task StopAsync(CancellationToken cancellationToken = default)
    {
        lock (_gate)
        {
            if (!_isStarted)
            {
                return Task.CompletedTask;
            }

            for (int i = _listeners.Count - 1; i >= 0; i--)
            {
                try
                {
                    _listeners[i].Deactivate(cancellationToken);
                    _listeners[i].Dispose();
                }
                catch (Exception ex)
                {
                    s_stopListenerFailedMessage(_state.Logger, ex);
                }
            }

            _listeners.Clear();

            for (int i = _protocols.Count - 1; i >= 0; i--)
            {
                try
                {
                    _protocols[i].Dispose();
                }
                catch (Exception ex)
                {
                    s_disposeProtocolFailedMessage(_state.Logger, ex);
                }
            }

            _protocols.Clear();

            try
            {
                _packetDispatch?.Deactivate(cancellationToken);
            }
            catch (Exception ex)
            {
                s_stopDispatcherFailedMessage(_state.Logger, ex);
            }

            _packetDispatch = null;
            _isStarted = false;
        }

        return Task.CompletedTask;
    }

    /// <summary>
    /// Runs the host until cancellation is requested.
    /// </summary>
    /// <param name="cancellationToken">A token that stops the host when canceled.</param>
    /// <returns>A task that completes when the host has stopped.</returns>
    public async Task RunAsync(CancellationToken cancellationToken = default)
    {
        await this.StartAsync(cancellationToken).ConfigureAwait(false);

        try
        {
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
        }
        finally
        {
            await this.StopAsync(CancellationToken.None).ConfigureAwait(false);
        }
    }

    /// <inheritdoc />
    public Task ActivateAsync(CancellationToken cancellationToken = default) => this.StartAsync(cancellationToken);

    /// <inheritdoc />
    public Task DeactivateAsync(CancellationToken cancellationToken = default) => this.StopAsync(cancellationToken);

    /// <inheritdoc />
    public void Activate(CancellationToken cancellationToken = default) => this.StartAsync(cancellationToken).GetAwaiter().GetResult();

    /// <inheritdoc />
    public void Deactivate(CancellationToken cancellationToken = default) => this.StopAsync(cancellationToken).GetAwaiter().GetResult();

    /// <inheritdoc />
    public void Dispose()
    {
        this.Deactivate(CancellationToken.None);
        GC.SuppressFinalize(this);
    }

    #endregion APIs

    #region Private Methods

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

    internal static PacketDispatchChannel CreatePacketDispatch(NetworkBuilderState state)
    {
        ArgumentNullException.ThrowIfNull(state);

        return new PacketDispatchChannel(dispatchOptions =>
        {
            _ = dispatchOptions.WithLogging(state.Logger);

            for (int i = 0; i < state.PacketDispatchConfigurations.Count; i++)
            {
                state.PacketDispatchConfigurations[i](dispatchOptions);
            }

            foreach (HandlerRegistration registration in ResolveHandlerRegistrations(state))
            {
                _ = s_registerHandlerMethod.MakeGenericMethod(registration.HandlerType)
                                           .Invoke(obj: null, parameters: [dispatchOptions, registration.Factory]);
            }
        });
    }

    private static IEnumerable<HandlerRegistration> ResolveHandlerRegistrations(NetworkBuilderState state)
    {
        Dictionary<Type, HandlerRegistration> handlers = [];

        for (int i = 0; i < state.HandlerRegistrations.Count; i++)
        {
            HandlerRegistration registration = state.HandlerRegistrations[i];
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

                _ = handlers.TryAdd(type, new HandlerRegistration(
                    type,
                    () => InstanceManager.Instance.CreateInstance(type)));
            }
        }

        return handlers.Values;
    }

    private void RegisterLogger() => InstanceManager.Instance.Register<ILogger>(_state.Logger);

    private void ApplyOptions()
    {
        for (int i = 0; i < _state.OptionRegistrations.Count; i++)
        {
            OptionRegistration registration = _state.OptionRegistrations[i];
            _ = s_applyOptionsMethod.MakeGenericMethod(registration.OptionsType)
                                    .Invoke(obj: null, parameters: [registration]);
        }
    }

    private static void ApplyOptionsCore<TOptions>(OptionRegistration registration)
        where TOptions : ConfigurationLoader, new()
    {
        TOptions options = ConfigurationManager.Instance.Get<TOptions>();
        registration.Apply(options);

        MethodInfo? validateMethod = typeof(TOptions).GetMethod("Validate", BindingFlags.Instance | BindingFlags.Public);
        _ = (validateMethod?.Invoke(options, parameters: null));
    }

    private void RegisterPacketRegistry() => InstanceManager.Instance.Register(CreatePacketRegistry(_state));

    private void RegisterMetadataProviders()
    {
        if (_metadataRegistered)
        {
            return;
        }

        for (int i = 0; i < _state.MetadataProviderRegistrations.Count; i++)
        {
            MetadataProviderRegistration registration = _state.MetadataProviderRegistrations[i];
            PacketMetadataProviders.Register(registration.Factory());
        }

        _metadataRegistered = true;
    }

    internal static IPacketRegistry CreatePacketRegistry(NetworkBuilderState state)
    {
        ArgumentNullException.ThrowIfNull(state);

        PacketRegistryFactory factory = new();

        for (int i = 0; i < state.PacketAssemblies.Count; i++)
        {
            PacketAssemblyRegistration registration = state.PacketAssemblies[i];
            _ = factory.RegisterAllPackets(registration.Assembly, registration.RequirePacketAttribute);
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

    #endregion Private Methods
}
