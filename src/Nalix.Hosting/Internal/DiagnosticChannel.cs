// Copyright (c) 2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using Microsoft.Extensions.Logging;
using Nalix.Framework.Injection;

namespace Nalix.Hosting.Internal;

/// <summary>
/// Bridges <see cref="DiagnosticListener"/> events from Nalix.Environment and Nalix.Framework
/// into <see cref="ILogger"/> for centralized observability.
/// </summary>
internal sealed class DiagnosticChannel :
    IObserver<DiagnosticListener>,
    IObserver<KeyValuePair<string, object?>>,
    IDisposable
{
    #region Fields

    private static readonly HashSet<string> s_targetListeners = new(StringComparer.Ordinal)
    {
        Network.DiagnosticsEvents.ListenerName,
        Framework.DiagnosticsEvents.ListenerName,
        Environment.DiagnosticsEvents.ListenerName,
    };

    private static readonly Dictionary<string, LogLevel> s_eventLevels = new(StringComparer.Ordinal)
    {
        // Framework.Tasks
        [Framework.DiagnosticsEvents.Tasks.Started] = LogLevel.Debug,
        [Framework.DiagnosticsEvents.Tasks.Failed] = LogLevel.Warning,
        [Framework.DiagnosticsEvents.Tasks.Completed] = LogLevel.Debug,
        [Framework.DiagnosticsEvents.Tasks.Disposed] = LogLevel.Information,
        [Framework.DiagnosticsEvents.Tasks.Cancelled] = LogLevel.Information,
        [Framework.DiagnosticsEvents.Tasks.Dispatcher] = LogLevel.Information,
        [Framework.DiagnosticsEvents.Tasks.RecurringExecuted] = LogLevel.Debug,

        // Framework.Memory
        [Framework.DiagnosticsEvents.Memory.PoolTrimmed] = LogLevel.Debug,
        [Framework.DiagnosticsEvents.Memory.PoolReturned] = LogLevel.Debug,
        [Framework.DiagnosticsEvents.Memory.PoolExpanded] = LogLevel.Debug,
        [Framework.DiagnosticsEvents.Memory.BufferReleased] = LogLevel.Debug,
        [Framework.DiagnosticsEvents.Memory.BufferAllocated] = LogLevel.Debug,
        [Framework.DiagnosticsEvents.Memory.PoolFailure] = LogLevel.Warning,
        [Framework.DiagnosticsEvents.Memory.SentinelWarning] = LogLevel.Warning,

        // Framework.Injection
        [Framework.DiagnosticsEvents.Injection.Resolved] = LogLevel.Debug,
        [Framework.DiagnosticsEvents.Injection.Failure] = LogLevel.Warning,
        [Framework.DiagnosticsEvents.Injection.Registered] = LogLevel.Debug,

        // Environment.Configuration
        [Environment.DiagnosticsEvents.Configuration.Flush] = LogLevel.Debug,
        [Environment.DiagnosticsEvents.Configuration.Cache] = LogLevel.Debug,
        [Environment.DiagnosticsEvents.Configuration.Container] = LogLevel.Debug,
        [Environment.DiagnosticsEvents.Configuration.Directory] = LogLevel.Debug,
        [Environment.DiagnosticsEvents.Configuration.Failure] = LogLevel.Warning,
        [Environment.DiagnosticsEvents.Configuration.Reload] = LogLevel.Information,
        [Environment.DiagnosticsEvents.Configuration.PathChanged] = LogLevel.Information,

        // Environment.IO
        [Environment.DiagnosticsEvents.IO.Cleanup] = LogLevel.Debug,
        [Environment.DiagnosticsEvents.IO.Directory] = LogLevel.Debug,

        // Environment.Random
        [Environment.DiagnosticsEvents.Random.Init] = LogLevel.Information,

        // Environment.Time
        [Environment.DiagnosticsEvents.Time.Reset] = LogLevel.Information,
        [Environment.DiagnosticsEvents.Time.Synchronized] = LogLevel.Information,

        // Network.Listeners
        [Network.DiagnosticsEvents.Listeners.Started] = LogLevel.Information,
        [Network.DiagnosticsEvents.Listeners.Stopped] = LogLevel.Information,
        [Network.DiagnosticsEvents.Listeners.BindFailed] = LogLevel.Warning,
        [Network.DiagnosticsEvents.Listeners.AcceptFailed] = LogLevel.Warning,

        // Network.Connections
        [Network.DiagnosticsEvents.Connections.Opened] = LogLevel.Debug,
        [Network.DiagnosticsEvents.Connections.Closed] = LogLevel.Debug,
        [Network.DiagnosticsEvents.Connections.Rejected] = LogLevel.Warning,
        [Network.DiagnosticsEvents.Connections.Timeout] = LogLevel.Debug,

        // Network.Transport
        [Network.DiagnosticsEvents.Transport.ReceiveFailed] = LogLevel.Warning,
        [Network.DiagnosticsEvents.Transport.SendFailed] = LogLevel.Warning,
        [Network.DiagnosticsEvents.Transport.SocketError] = LogLevel.Warning,
        [Network.DiagnosticsEvents.Transport.Disconnected] = LogLevel.Debug,
        [Network.DiagnosticsEvents.Transport.MalformedFrame] = LogLevel.Warning,
        [Network.DiagnosticsEvents.Transport.OversizedFrame] = LogLevel.Warning,

        // Network.Security
        [Network.DiagnosticsEvents.Security.RateLimited] = LogLevel.Debug,
        [Network.DiagnosticsEvents.Security.Blacklisted] = LogLevel.Information,
        [Network.DiagnosticsEvents.Security.Banned] = LogLevel.Information,
        [Network.DiagnosticsEvents.Security.SuspiciousPacket] = LogLevel.Warning,
        [Network.DiagnosticsEvents.Security.DdosDetected] = LogLevel.Warning,
        [Network.DiagnosticsEvents.Security.LimitDriftCorrected] = LogLevel.Warning,
        [Network.DiagnosticsEvents.Security.CleanupError] = LogLevel.Warning,

        // Network.Internal
        [Network.DiagnosticsEvents.Internal.Trace] = LogLevel.Trace,
        [Network.DiagnosticsEvents.Internal.Debug] = LogLevel.Debug,
        [Network.DiagnosticsEvents.Internal.Information] = LogLevel.Information,
        [Network.DiagnosticsEvents.Internal.Warning] = LogLevel.Warning,
        [Network.DiagnosticsEvents.Internal.Error] = LogLevel.Error,
        [Network.DiagnosticsEvents.Internal.Critical] = LogLevel.Critical,
        [Network.DiagnosticsEvents.Internal.LoopFaulted] = LogLevel.Error,
        [Network.DiagnosticsEvents.Internal.ResourceExhausted] = LogLevel.Warning,
    };

    private readonly ILogger? _logger = InstanceManager.Instance.GetExistingInstance<ILogger>();

    private readonly Dictionary<string, IDisposable> _listenerSubscriptions = new(StringComparer.Ordinal);
    private IDisposable? _allListenersSubscription;

    private readonly LogLevel _minLevel;

    #endregion Fields

    #region Constructor

    public DiagnosticChannel(LogLevel minLevel) => _minLevel = minLevel;

    #endregion Constructor

    #region API

    public void Subscribe()
    {
        _allListenersSubscription?.Dispose();
        this.DisposeListenerSubscriptions();

        _allListenersSubscription = DiagnosticListener.AllListeners.Subscribe(this);
    }

    #endregion API

    #region IObserver<DiagnosticListener>

    void IObserver<DiagnosticListener>.OnNext(DiagnosticListener listener)
    {
        if (!s_targetListeners.Contains(listener.Name))
        {
            return;
        }

        if (_listenerSubscriptions.ContainsKey(listener.Name))
        {
            return;
        }

        _listenerSubscriptions[listener.Name] = listener.Subscribe(this, this.IsEventEnabled);
    }

    void IObserver<DiagnosticListener>.OnError(Exception error) { }

    void IObserver<DiagnosticListener>.OnCompleted() { }

    #endregion IObserver<DiagnosticListener>

    #region IObserver<KeyValuePair<string, object?>>

    void IObserver<KeyValuePair<string, object?>>.OnNext(KeyValuePair<string, object?> value)
    {
        if (_logger is null)
        {
            return;
        }

        LogLevel level = MapLogLevel(value.Key);

        if (level < _minLevel || !_logger.IsEnabled(level))
        {
            return;
        }

        _logger.Log(level, "[DIAG] {EventName} {@Payload}", value.Key, value.Value);
    }

    void IObserver<KeyValuePair<string, object?>>.OnError(Exception error)
    {
        if (_logger is null || !_logger.IsEnabled(LogLevel.Error))
        {
            return;
        }

        _logger?.LogError(error, "[DIAG] DiagnosticListener error");
    }

    void IObserver<KeyValuePair<string, object?>>.OnCompleted() { }

    #endregion IObserver<KeyValuePair<string, object?>>

    #region Private Helpers

    private bool IsEventEnabled(string eventName)
    {
        if (_logger is null)
        {
            return false;
        }

        LogLevel level = MapLogLevel(eventName);
        return level >= _minLevel && _logger.IsEnabled(level);
    }

    private static LogLevel MapLogLevel(string eventName) => s_eventLevels.TryGetValue(eventName, out LogLevel level) ? level : LogLevel.Debug;

    #endregion Private Helpers

    #region Disposal

    private void DisposeListenerSubscriptions()
    {
        foreach (IDisposable subscription in _listenerSubscriptions.Values)
        {
            subscription.Dispose();
        }

        _listenerSubscriptions.Clear();
    }

    public void Dispose()
    {
        _allListenersSubscription?.Dispose();
        this.DisposeListenerSubscriptions();
    }

    #endregion Disposal
}
