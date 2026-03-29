// Copyright (c) 2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Nalix.Common.Abstractions;
using Nalix.Common.Networking;
using Nalix.Framework.Injection;
using Nalix.Network.Hosting.Internal;
using Nalix.Runtime.Dispatching;

namespace Nalix.Network.Hosting;

/// <summary>
/// Represents a runnable host for Nalix TCP servers.
/// </summary>
/// <remarks>
/// Use <see cref="CreateBuilder"/> to configure a host instance, then call
/// <see cref="ActivateAsync(CancellationToken)"/>, <see cref="RunAsync(CancellationToken)"/>,
/// or the lifecycle methods inherited from <see cref="IActivatable"/> and
/// <see cref="IActivatableAsync"/>.
/// </remarks>
public sealed class NetworkApplication : IActivatableAsync
{
    #region Static Fields

    private static readonly Action<ILogger, string?, Exception?> s_startedTcpServerMessage =
        LoggerMessage.Define<string?>(
            LogLevel.Information,
            new EventId(1000, nameof(NetworkApplication)),
            "Started Nalix TCP server for protocol {ProtocolType}.");

    private static readonly Action<ILogger, string?, Exception?> s_startedUdpServerMessage =
        LoggerMessage.Define<string?>(
            LogLevel.Information,
            new EventId(1004, nameof(NetworkApplication)),
            "Started Nalix UDP server for protocol {ProtocolType}.");

    private static readonly Action<ILogger, Exception?> s_stopListenerFailedMessage =
        LoggerMessage.Define(
            LogLevel.Warning,
            new EventId(1001, nameof(NetworkApplication)),
            "Failed to stop Nalix listener cleanly.");

    private static readonly Action<ILogger, Exception?> s_disposeProtocolFailedMessage =
        LoggerMessage.Define(
            LogLevel.Warning,
            new EventId(1002, nameof(NetworkApplication)),
            "Failed to dispose Nalix protocol cleanly.");

    private static readonly Action<ILogger, Exception?> s_stopDispatcherFailedMessage =
        LoggerMessage.Define(
            LogLevel.Warning,
            new EventId(1003, nameof(NetworkApplication)),
            "Failed to stop the Nalix packet dispatcher cleanly.");

    #endregion Static Fields

    #region Fields

    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly ILogger _logger;
    private readonly Action _prepareCallbacks;
    private readonly Func<IPacketDispatch> _dispatchFactory;
    private readonly IReadOnlyList<Func<IPacketDispatch, ListenerBinding>> _serverFactories;

    private readonly List<IListener> _listeners = [];
    private readonly List<IProtocol> _protocols = [];
    private readonly IReadOnlyList<IActivatableAsync> _hostedServices;

    private bool _isStarted;
    private IPacketDispatch? _packetDispatch;

    #endregion Fields

    #region Constructors

    internal NetworkApplication(
        ILogger logger,
        Action prepareCallbacks,
        Func<IPacketDispatch> dispatchFactory,
        IReadOnlyList<Func<IPacketDispatch, ListenerBinding>> serverFactories,
        IReadOnlyList<IActivatableAsync> hostedServices)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _prepareCallbacks = prepareCallbacks ?? throw new ArgumentNullException(nameof(prepareCallbacks));
        _dispatchFactory = dispatchFactory ?? throw new ArgumentNullException(nameof(dispatchFactory));
        _serverFactories = serverFactories ?? throw new ArgumentNullException(nameof(serverFactories));
        _hostedServices = hostedServices ?? throw new ArgumentNullException(nameof(hostedServices));
    }

    #endregion Constructors

    #region APIs

    /// <summary>
    /// Creates a new builder for <see cref="NetworkApplication"/>.
    /// </summary>
    /// <returns>A new <see cref="NetworkApplicationBuilder"/> instance.</returns>
    public static NetworkApplicationBuilder CreateBuilder() => new(new HostingBuilderContext());

    /// <summary>
    /// Runs the host until cancellation is requested.
    /// </summary>
    /// <param name="cancellationToken">A token that stops the host when canceled.</param>
    /// <returns>A task that completes when the host has stopped.</returns>
    public async Task RunAsync(CancellationToken cancellationToken = default)
    {
        await this.ActivateAsync(cancellationToken).ConfigureAwait(false);

        try
        {
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
        }
        finally
        {
            await this.DeactivateAsync(CancellationToken.None).ConfigureAwait(false);
        }
    }

    /// <inheritdoc />
    public async Task ActivateAsync(CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (_isStarted)
            {
                return;
            }

            _prepareCallbacks();

            _packetDispatch = _dispatchFactory();

            try
            {
                InstanceManager.Instance.Register<IPacketDispatch>(_packetDispatch);
            }
            catch
            {
            }

            _packetDispatch.Activate(cancellationToken);

            for (int i = 0; i < _serverFactories.Count; i++)
            {
                ListenerBinding server = _serverFactories[i](_packetDispatch);

                _protocols.Add(server.Protocol);
                _listeners.Add(server.Listener);

                server.Listener.Activate(cancellationToken);

                if (server.IsUdp)
                {
                    s_startedUdpServerMessage(_logger, server.ProtocolType.FullName, null);
                }
                else
                {
                    s_startedTcpServerMessage(_logger, server.ProtocolType.FullName, null);
                }
            }

            for (int i = 0; i < _hostedServices.Count; i++)
            {
                await _hostedServices[i].ActivateAsync(cancellationToken).ConfigureAwait(false);
            }

            _isStarted = true;
        }
        finally
        {
            _ = _gate.Release();
        }
    }

    /// <inheritdoc />
    public async Task DeactivateAsync(CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (!_isStarted)
            {
                return;
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
                    s_stopListenerFailedMessage(_logger, ex);
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
                    s_disposeProtocolFailedMessage(_logger, ex);
                }
            }

            _protocols.Clear();

            for (int i = _hostedServices.Count - 1; i >= 0; i--)
            {
                try
                {
                    await _hostedServices[i].DeactivateAsync(cancellationToken).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    _logger.Warn("Failed to stop hosted service cleanly. {Ex}", ex);
                }
            }

            try
            {
                _packetDispatch?.Deactivate(cancellationToken);
            }
            catch (Exception ex)
            {
                s_stopDispatcherFailedMessage(_logger, ex);
            }

            _packetDispatch = null;
            _isStarted = false;
        }
        finally
        {
            _ = _gate.Release();
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        this.DeactivateAsync(CancellationToken.None).GetAwaiter().GetResult();
        GC.SuppressFinalize(this);
    }

    #endregion APIs
}
