// Copyright (c) 2025 PPN Corporation. All rights reserved.

using Nalix.Common.Logging;
using Nalix.Logging.Sinks;

namespace Nalix.Logging.Core;

/// <summary>
/// Abstract class that provides a high-performance logging engine to process log entries.
/// </summary>
[System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverage]
[System.Diagnostics.DebuggerDisplay("{GetType().Name,nq}")]
public abstract class NLogixEngine : System.IDisposable
{
    #region Fields

    private readonly NLogixOptions _logOptions;
    private readonly NLogixDistributor _distributor;

    private LogLevel _minLevel;
    private System.Int32 _isDisposed;

    private static readonly System.Collections.Concurrent.ConcurrentDictionary<System.String, System.Text.CompositeFormat> s_formatCache;

    #endregion Fields

    #region Constructors

    static NLogixEngine()
    {
        s_formatCache = new(System.StringComparer.Ordinal);
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="NLogixEngine"/> class.
    /// </summary>
    /// <param name="configureOptions">
    /// An action that allows configuring the logging options.
    /// This action is used to set up logging options such as the minimum logging level and file options.
    /// </param>
    protected NLogixEngine(System.Action<NLogixOptions>? configureOptions = null)
    {
        _distributor = new NLogixDistributor();
        _logOptions = new NLogixOptions(_distributor);

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
                _ = cfg.AddTarget(new ConsoleLogTarget());
                _ = cfg.AddTarget(new FileLogTarget(_logOptions.FileOptions));
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
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining |
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
    protected void Configure(System.Action<NLogixOptions> configureOptions)
    {
        configureOptions?.Invoke(_logOptions);

        _minLevel = _logOptions.MinLevel;
    }

    /// <summary>
    /// Checks if the log level meets the minimum required level for logging.
    /// </summary>
    /// <param name="level">The log level to check.</param>
    /// <returns><c>true</c> if the log level is enabled for logging.</returns>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining |
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
    public System.Boolean IsEnabled(LogLevel level) => level >= _minLevel;

    /// <summary>
    /// Creates and publishes a log entry if the log level is enabled.
    /// </summary>
    /// <param name="level">The severity level of the log entry.</param>
    /// <param name="eventId">The event identifier associated with the log entry.</param>
    /// <param name="message">The log message.</param>
    /// <param name="error">Optional exception information.</param>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining |
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
    [System.Diagnostics.CodeAnalysis.SuppressMessage(
        "Reliability", "CA2012:Use ValueTasks correctly", Justification = "<Pending>")]
    [System.Diagnostics.CodeAnalysis.SuppressMessage(
        "CodeQuality", "IDE0079:Remove unnecessary suppression", Justification = "<Pending>")]
    protected void CreateLogEntry(
        LogLevel level, EventId eventId,
        System.String message, System.Exception? error = null)
    {
        if (_isDisposed != 0 || !IsEnabled(level))
        {
            return;
        }

        // Create and publish the log entry
        _ = _distributor.PublishAsync(new LogEntry(level, eventId, message, error));
    }

    /// <summary>
    /// Creates and publishes a log entry with a formatted message if the log level is enabled.
    /// </summary>
    /// <param name="level">The severity level of the log entry.</param>
    /// <param name="eventId">The event identifier associated with the log entry.</param>
    /// <param name="format">The message format string with placeholders.</param>
    /// <param name="args">The argument values for the format string.</param>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining |
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
    protected void CreateFormattedLogEntry(
        LogLevel level, EventId eventId,
        System.String format, params System.Object[] args)
    {
        // Skip expensive string formatting if the log level is disabled
        if (_isDisposed != 0 || !IsEnabled(level))
        {
            return;
        }

        // Format the message only if we're going to use it
        CreateLogEntry(level, eventId, FormatMessage(format, args));
    }

    /// <summary>
    /// Releases managed and unmanaged resources used by the logging engine.
    /// </summary>
    /// <param name="disposing">True if called from Dispose(), false if called from finalizer.</param>
    public virtual void Dispose(System.Boolean disposing)
    {
        // Thread-safe disposal check using Interlocked
        if (System.Threading.Interlocked.Exchange(ref _isDisposed, 1) != 0)
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
        Dispose(true);
        System.GC.SuppressFinalize(this);
    }

    #endregion Disposable

    #region Private Methods

    [System.Diagnostics.Contracts.Pure]
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    private static System.String FormatMessage(System.String format, System.Object[]? args)
    {
        if (System.String.IsNullOrEmpty(format) || args == null || args.Length == 0)
        {
            return format;
        }

        // Fast path: single argument with "{0}" or "{0:...}" pattern.
        if (args.Length == 1 && TryParseSimplePlaceholder(format, out var innerFormat))
        {
            var arg = args[0];

            // ISpanFormattable first (DateTime, numeric types, etc. in .NET 7/8)
            if (arg is System.ISpanFormattable spanFormattable)
            {
                // Heuristic max length. If not enough, grow on-demand.
                System.Span<System.Char> initial = stackalloc System.Char[64];
                var provider = System.Globalization.CultureInfo.CurrentCulture;
                if (spanFormattable.TryFormat(initial, out System.Int32 written, innerFormat, provider))
                {
                    return new System.String(initial[..written]);
                }

                // Rerun with rented buffer if initial stack is not enough
                System.Int32 size = 128;
                while (true)
                {
                    System.Char[] rented = System.Buffers.ArrayPool<System.Char>.Shared.Rent(size);
                    try
                    {
                        if (spanFormattable.TryFormat(rented, out written, innerFormat, provider))
                        {
                            return new System.String(rented, 0, written);
                        }
                    }
                    finally
                    {
                        System.Buffers.ArrayPool<System.Char>.Shared.Return(rented);
                    }
                    size <<= 1;
                    if (size > 1024 * 64) // hard cap to avoid runaway
                    {
                        break;
                    }
                }
            }

            // IFormattable fallback (boxed types or custom formattables)
            if (arg is System.IFormattable formattable)
            {
                return formattable.ToString(innerFormat.ToString(), System.Globalization.CultureInfo.CurrentCulture) ?? System.String.Empty;
            }

            // Generic fallback
            return arg?.ToString() ?? System.String.Empty;
        }

        // General path: cached CompositeFormat to avoid reparsing the format string
        var composite = s_formatCache.GetOrAdd(format, static f => System.Text.CompositeFormat.Parse(f));
        return System.String.Format(System.Globalization.CultureInfo.CurrentCulture, composite, args);
    }

    /// <summary>
    /// Detects if 'format' is exactly "{0}" or "{0:...}" (no extra text),
    /// and extracts the inner format (after ':') if present.
    /// Returns true when pattern matches.
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    private static System.Boolean TryParseSimplePlaceholder(System.String format, out System.ReadOnlySpan<System.Char> innerFormat)
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
        System.ReadOnlySpan<System.Char> span = System.MemoryExtensions.AsSpan(format, 1, format.Length - 2); // inside braces
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