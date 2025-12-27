// Copyright (c) 2025-2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Diagnostics.Contracts;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using Nalix.Common.Diagnostics;
using Nalix.Framework.Configuration;
using Nalix.Logging.Configuration;
using Nalix.Logging.Sinks;

namespace Nalix.Logging.Engine;

/// <summary>
/// Abstract class that provides a high-performance logging engine to process log entries.
/// </summary>
[ExcludeFromCodeCoverage]
[DebuggerDisplay("{GetType().Name,nq}")]
[EditorBrowsable(EditorBrowsableState.Never)]
public abstract class NLogixEngine : IDisposable
{
    #region Fields

    private readonly NLogixOptions _logOptions;
    private readonly NLogixDistributor _distributor;

    private LogLevel _minLevel;
    private int _isDisposed;

    private static readonly System.Collections.Concurrent.ConcurrentDictionary<string, CompositeFormat> s_formatCache;

    #endregion Fields

    #region Constructors

    static NLogixEngine() => s_formatCache = new(StringComparer.Ordinal);

    /// <summary>
    /// Initializes a new instance of the <see cref="NLogixEngine"/> class.
    /// </summary>
    /// <param name="configureOptions">
    /// An action that allows configuring the logging options.
    /// This action is used to set up logging options such as the minimum logging level and file options.
    /// </param>
    protected NLogixEngine(Action<NLogixOptions>? configureOptions = null)
    {
        _distributor = new NLogixDistributor();
        _logOptions = ConfigurationManager.Instance.Get<NLogixOptions>();

        _ = _logOptions.SetPublisher(_distributor);

        // Apply configuration if provided
        if (configureOptions != null)
        {
            configureOptions.Invoke(_logOptions);
        }
        else
        {
            // Apply default configuration
            _ = _logOptions.ConfigureDefaults(cfg =>
            {
                _ = cfg.RegisterTarget(new BatchFileLogTarget());
                _ = cfg.RegisterTarget(new BatchConsoleLogTarget());
                return cfg;
            });
        }

        // Cache min level for faster checks
        _minLevel = _logOptions.MinLevel;
    }

    #endregion Constructors

    #region Logging Methods

    /// <summary>
    /// Reconfigure logging options after initialization.
    /// </summary>
    /// <param name="configureOptions">
    /// An action that allows configuring the logging options.
    /// This action is used to set up logging options such as the minimum logging level and file options.
    /// </param>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    protected void ConfigureOptions(Action<NLogixOptions> configureOptions)
    {
        configureOptions?.Invoke(_logOptions);

        LogLevel newLevel = _logOptions.MinLevel;
        _ = Interlocked.Exchange(ref Unsafe
                                    .As<LogLevel, int>(ref _minLevel), (int)newLevel);
    }

    /// <summary>
    /// Checks if the log level meets the minimum required level for logging.
    /// </summary>
    /// <param name="level">The log level to check.</param>
    /// <returns><c>true</c> if the log level is enabled for logging.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public bool IsLevelEnabled(LogLevel level) => level >= _minLevel;

    /// <summary>
    /// Creates and publishes a log entry if the log level is enabled.
    /// </summary>
    /// <param name="level">The severity level of the log entry.</param>
    /// <param name="eventId">The event identifier associated with the log entry.</param>
    /// <param name="message">The log message.</param>
    /// <param name="error">Optional exception information.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    [SuppressMessage(
        "Reliability", "CA2012:Use ValueTasks correctly", Justification = "<Pending>")]
    [SuppressMessage(
        "CodeQuality", "IDE0079:Remove unnecessary suppression", Justification = "<Pending>")]
    protected void Publish(
        LogLevel level,
        EventId eventId,
        string message,
        [MaybeNull] Exception? error = null)
    {
        if (_isDisposed != 0 || !this.IsLevelEnabled(level))
        {
            return;
        }

        LogEntry entry = new(level, eventId, message, error);
        _distributor.Publish(entry);
    }

    /// <summary>
    /// Creates and publishes a log entry with a formatted message if the log level is enabled.
    /// </summary>
    /// <param name="level">The severity level of the log entry.</param>
    /// <param name="eventId">The event identifier associated with the log entry.</param>
    /// <param name="format">The message format string with placeholders.</param>
    /// <param name="args">The argument values for the format string.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    protected void Publish(LogLevel level, EventId eventId, string format, params object[] args)
    {
        // Skip expensive string formatting if the log level is disabled
        if (_isDisposed != 0 || !this.IsLevelEnabled(level))
        {
            return;
        }

        // Format the message only if we're going to use it
        this.Publish(level, eventId, FormatMessage(format, args));
    }

    /// <summary>
    /// Releases managed and unmanaged resources used by the logging engine.
    /// </summary>
    /// <param name="disposing">True if called from Dispose(), false if called from finalizer.</param>
    public virtual void Dispose(bool disposing)
    {
        // Thread-safe disposal check using Interlocked
        if (Interlocked.Exchange(ref _isDisposed, 1) != 0)
        {
            return;
        }

        if (disposing)
        {
            _logOptions.Dispose();
        }
    }

    #endregion Logging Methods

    #region Disposable

    /// <summary>
    /// Disposes the logging engine and all related resources.
    /// </summary>
    public void Dispose()
    {
        this.Dispose(true);
        GC.SuppressFinalize(this);
    }

    #endregion Disposable

    #region Private Methods

    [Pure]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static string FormatMessage(string format, object[]? args)
    {
        if (string.IsNullOrEmpty(format) || args == null || args.Length == 0)
        {
            return format;
        }

        // Fast path: single argument with "{0}" or "{0:...}" pattern.
        if (args.Length == 1 && TryParseSimplePlaceholder(format, out ReadOnlySpan<char> innerFormat))
        {
            object arg = args[0];

            // ISpanFormattable first (DateTime, numeric types, etc. in .NET 7/8)
            if (arg is ISpanFormattable spanFormattable)
            {
                // Heuristic max length. If not enough, grow on-demand.
                Span<char> initial = stackalloc char[64];
                CultureInfo provider = CultureInfo.CurrentCulture;

                if (spanFormattable.TryFormat(initial, out int written, innerFormat, provider))
                {
                    return new string(initial[..written]);
                }

                // Rerun with rented buffer if initial stack is not enough
                int size = 128;
                do
                {
                    char[] rented = System.Buffers.ArrayPool<char>.Shared.Rent(size);
                    try
                    {
                        if (spanFormattable.TryFormat(rented, out written, innerFormat, provider))
                        {
                            return new string(rented, 0, written);
                        }
                    }
                    finally
                    {
                        System.Buffers.ArrayPool<char>.Shared.Return(rented);
                    }
                    size <<= 1;
                }
                while (size <= 1024 * 64);
            }

            // IFormattable fallback (boxed types or custom formattables)
            if (arg is IFormattable formattable)
            {
                return formattable.ToString(innerFormat.ToString(), CultureInfo.CurrentCulture) ?? string.Empty;
            }

            // Generic fallback
            return arg?.ToString() ?? string.Empty;
        }

        // General path: cached CompositeFormat to avoid reparsing the format string
        CompositeFormat composite = s_formatCache.GetOrAdd(format, static f => CompositeFormat.Parse(f));
        return string.Format(CultureInfo.CurrentCulture, composite, args);
    }

    /// <summary>
    /// Detects if 'format' is exactly "{0}" or "{0:...}" (no extra text),
    /// and extracts the inner format (after ':') if present.
    /// Returns true when pattern matches.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool TryParseSimplePlaceholder(string format, out ReadOnlySpan<char> innerFormat)
    {
        innerFormat = default;

        // Quick checks: must start with '{' and end with '}'
        if (format.Length < 3 || format[0] != '{' || format[^1] != '}')
        {
            return false;
        }

        // Allowed forms:
        // "{0}"                  -> innerFormat = default
        // "{0:formatString}"     -> innerFormat = "formatString"
        // No alignment, no index other than 0, no extra text around.
        // We'll parse a minimal subset to stay fast.
        ReadOnlySpan<char> span = format.AsSpan(1, format.Length - 2); // inside braces
                                                                       // Now span should be "0" or "0:...".
        if (span.Length == 1 && span[0] == '0')
        {
            return true;
        }

        if (span.Length > 2 && span[0] == '0' && span[1] == ':')
        {
            innerFormat = span[2..];
            return true;
        }

        return false;
    }

    #endregion Private Methods
}
