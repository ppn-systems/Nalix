// Copyright (c) 2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using Microsoft.Extensions.Logging;
using Nalix.Abstractions.Diagnostics;
using Nalix.Framework.Injection;

namespace Nalix.Hosting.Internal;

/// <summary>
/// Bridges <see cref="DiagnosticListener"/> events from Nalix.Environment, Nalix.Framework,
/// Nalix.Network, and Nalix.Runtime into <see cref="ILogger"/> for centralized observability.
/// </summary>
internal sealed class DiagnosticChannel :
    IObserver<DiagnosticListener>,
    IObserver<KeyValuePair<string, object?>>,
    IDisposable
{
    #region Fields

    private static readonly HashSet<string> s_targetListeners = new(StringComparer.Ordinal)
    {
        Codec.DiagnosticsEvents.ListenerName,
        Network.DiagnosticsEvents.ListenerName,
        Framework.DiagnosticsEvents.ListenerName,
        Environment.DiagnosticsEvents.ListenerName,
        Runtime.DiagnosticsEvents.ListenerName,
    };

    // CA2244: Runtime and Network Internal event names intentionally share the same
    // constant values (e.g. "Internal.Trace") and identical log level mappings.
    // The duplicate entries are harmless because both modules use the same levels.
#pragma warning disable CA2244
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

        // Codec.Serialization
        [Codec.DiagnosticsEvents.Serialization.FormatterRegistered] = LogLevel.Debug,
        [Codec.DiagnosticsEvents.Serialization.Failure] = LogLevel.Error,
        [Codec.DiagnosticsEvents.Serialization.Initialization] = LogLevel.Debug,

        [Codec.DiagnosticsEvents.Packet.Malformed] = LogLevel.Trace,
        [Codec.DiagnosticsEvents.Serialization.Poisoned] = LogLevel.Trace,

        // Environment.Random Failure
        [Environment.DiagnosticsEvents.Random.Failure] = LogLevel.Critical,

        // Runtime.Internal
        [Runtime.DiagnosticsEvents.Internal.Trace] = LogLevel.Trace,
        [Runtime.DiagnosticsEvents.Internal.Debug] = LogLevel.Debug,
        [Runtime.DiagnosticsEvents.Internal.Information] = LogLevel.Information,
        [Runtime.DiagnosticsEvents.Internal.Warning] = LogLevel.Warning,
        [Runtime.DiagnosticsEvents.Internal.Error] = LogLevel.Error,
        [Runtime.DiagnosticsEvents.Internal.Critical] = LogLevel.Critical,
    };
#pragma warning restore CA2244

    private readonly ILogger? _logger = InstanceManager.Instance.GetExistingInstance<ILogger>();

    private readonly Dictionary<string, IDisposable> _listenerSubscriptions = new(StringComparer.Ordinal);
    private IDisposable? _allListenersSubscription;

    #endregion Fields

    #region Constructor

    public DiagnosticChannel(ILogger? logger) => _logger = logger;

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
        if (_logger is null || value.Value is null)
        {
            return;
        }

        LogLevel level = MapLogLevel(value.Key);

        if (!_logger.IsEnabled(level))
        {
            return;
        }

        if (value.Value is DiagnosticLog log)
        {
            if (log.Exception is null)
            {
                _logger.Log(level, "[{Tag}] {Message}", log.Tag, log.Message);
            }
            else
            {
                _logger.Log(level, log.Exception, "[{Tag}] {Message}", log.Tag, log.Message);
            }
            return;
        }

        Type type = value.Value.GetType();
        ObjectAccessor accessor = s_accessors.GetOrAdd(type, t => new ObjectAccessor(t));

        // NALIX078: Intentional diagnostic payload inspection — bridges DiagnosticListener events
        // to ILogger. Observational/non-hot-path, not serialization or packet dispatch.
        // ObjectAccessor cache is populated once per type, annotated with
        // [UnconditionalSuppressMessage("Trimming", "IL2070")].
#pragma warning disable NALIX078
        string? message = accessor.MessageProperty?.GetValue(value.Value) as string;
        Exception? exception = accessor.ExceptionProperty?.GetValue(value.Value) as Exception;
#pragma warning restore NALIX078

        System.Text.StringBuilder sb = new();
        _ = sb.Append('[').Append(GetCategory(value.Key)).Append("] ");

        if (message is not null)
        {
            _ = sb.Append(message);
        }

        if (accessor.OtherProperties.Length > 0)
        {
            _ = sb.Append(" [");
            for (int i = 0; i < accessor.OtherProperties.Length; i++)
            {
                if (i > 0)
                {
                    _ = sb.Append(", ");
                }

                _ = sb.Append(accessor.OtherProperties[i].Name).Append('=');
                // NALIX078: Intentional diagnostic property enumeration — see above.
#pragma warning disable NALIX078
                object? propVal = accessor.OtherProperties[i].GetValue(value.Value);
#pragma warning restore NALIX078
                _ = sb.Append(propVal);
            }

            _ = sb.Append(']');
        }

        message = sb.ToString();
        _logger.Log(level, default, exception, "{Message}", message);
    }

    void IObserver<KeyValuePair<string, object?>>.OnError(Exception error)
    {
        if (_logger is null || !_logger.IsEnabled(LogLevel.Error))
        {
            return;
        }

        EventId eventId = new(0, "DiagnosticChannel");
        _logger.Log(LogLevel.Error, eventId, error, "DiagnosticListener error");
    }

    void IObserver<KeyValuePair<string, object?>>.OnCompleted() { }

    #endregion IObserver<KeyValuePair<string, object?>>

    #region Private Helpers

    private static string GetCategory(string eventName) => eventName.Replace(".DiagnosticsEvents", "", StringComparison.Ordinal);

    private bool IsEventEnabled(string eventName)
    {
        if (_logger is null)
        {
            return false;
        }

        return _logger.IsEnabled(MapLogLevel(eventName));
    }

    private static LogLevel MapLogLevel(string eventName) => s_eventLevels.TryGetValue(eventName, out LogLevel level) ? level : LogLevel.Trace;

    #endregion Private Helpers

    #region Reflection Cache

    private static readonly System.Collections.Concurrent.ConcurrentDictionary<Type, ObjectAccessor> s_accessors = new();

    private sealed class ObjectAccessor
    {
        public readonly System.Reflection.PropertyInfo? MessageProperty;
        public readonly System.Reflection.PropertyInfo? ExceptionProperty;
        public readonly System.Reflection.PropertyInfo[] OtherProperties;

        [System.Diagnostics.CodeAnalysis.UnconditionalSuppressMessage("Trimming", "IL2070",
            Justification = "Diagnostic payload property scanning is observational only; trimming does not affect runtime behavior.")]
        public ObjectAccessor(Type type)
        {
            System.Reflection.PropertyInfo[] props = type.GetProperties(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
            List<System.Reflection.PropertyInfo> others = new(props.Length);

            foreach (System.Reflection.PropertyInfo p in props)
            {
                if ((p.Name == "Message" || p.Name == "Action") && p.PropertyType == typeof(string))
                {
                    MessageProperty ??= p;
                }
                else if (p.Name == "Exception" && typeof(Exception).IsAssignableFrom(p.PropertyType))
                {
                    ExceptionProperty = p;
                }
                else
                {
                    others.Add(p);
                }
            }

            OtherProperties = [.. others];
        }
    }

    #endregion Reflection Cache

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
