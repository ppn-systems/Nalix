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
    private static readonly HashSet<string> s_targetListeners = new(StringComparer.Ordinal)
    {
        Environment.DiagnosticsEvents.ListenerName,
        Framework.DiagnosticsEvents.ListenerName
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
    };

    private IDisposable? _listenerSubscription;
    private IDisposable? _allListenersSubscription;
    private readonly LogLevel _minLevel;

    public DiagnosticChannel(LogLevel minLevel) => _minLevel = minLevel;

    public void Subscribe()
    {
        _allListenersSubscription?.Dispose();
        _allListenersSubscription = DiagnosticListener.AllListeners.Subscribe(this);
    }

    #region IObserver<DiagnosticListener>

    void IObserver<DiagnosticListener>.OnNext(DiagnosticListener listener)
    {
        if (s_targetListeners.Contains(listener.Name))
        {
            _listenerSubscription?.Dispose();
            _listenerSubscription = listener.Subscribe(this);
        }
    }

    void IObserver<DiagnosticListener>.OnError(Exception error) { }

    void IObserver<DiagnosticListener>.OnCompleted() { }

    #endregion IObserver<DiagnosticListener>

    #region IObserver<KeyValuePair<string, object?>>

    void IObserver<KeyValuePair<string, object?>>.OnNext(KeyValuePair<string, object?> value)
    {
        ILogger? logger = InstanceManager.Instance.GetExistingInstance<ILogger>();
        if (logger is null)
        {
            return;
        }

        LogLevel level = MapLogLevel(value.Key);

        if (level < _minLevel)
        {
            return;
        }

        if (!logger.IsEnabled(level))
        {
            return;
        }

        logger.Log(level, "[DIAG] {EventName} {@Payload}", value.Key, value.Value);
    }

    void IObserver<KeyValuePair<string, object?>>.OnError(Exception error)
    {
        ILogger? logger = InstanceManager.Instance.GetExistingInstance<ILogger>();
        logger?.LogError(error, "[DIAG] DiagnosticListener error");
    }

    void IObserver<KeyValuePair<string, object?>>.OnCompleted() { }

    #endregion IObserver<KeyValuePair<string, object?>>

    private static LogLevel MapLogLevel(string eventName) => s_eventLevels.TryGetValue(eventName, out LogLevel level) ? level : LogLevel.Debug;

    public void Dispose()
    {
        _allListenersSubscription?.Dispose();
        _listenerSubscription?.Dispose();
    }
}
