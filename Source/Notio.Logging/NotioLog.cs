using Notio.Logging.Engine;
using Notio.Logging.Enums;
using Notio.Logging.Metadata;
using Notio.Logging.Targets;
using System;
using System.Runtime.CompilerServices;

namespace Notio.Logging;

public sealed class NotioLog : LoggingEngine
{
    private bool _isInitialized;
    private static readonly Lazy<NotioLog> _instance = new(() => new());

    private NotioLog()
    { }

    public static NotioLog Instance => _instance.Value;

    /// <summary>
    /// Initializes the logging system with optional configuration.
    /// </summary>
    public void Initialize(Action<LoggingConfig>? configure = null)
    {
        if (_isInitialized)
            throw new InvalidOperationException("Logging has already been initialized.");

        _isInitialized = true;

        LoggingConfig builder = new(base.Publisher);
        configure?.Invoke(builder);

        if (builder.IsDefaults)
        {
            builder.ConfigureDefaults(cfg =>
            {
                cfg.AddTarget(new ConsoleTarget());
                cfg.AddTarget(new FileTarget(cfg.LogDirectory, cfg.LogFileName));
                return cfg;
            });
        }
    }

    /// <summary>
    /// Writes a log entry with specified level, event ID, message, and optional exception.
    /// </summary>
    public void WriteLog(LoggingLevel level, EventId eventId, string message, Exception? exception = null)
       => base.CreateLogEntry(level, eventId, message, exception);

    /// <summary>
    /// Logs metadata information.
    /// </summary>
    public void Meta(string message, EventId? eventId = null)
        => WriteLog(LoggingLevel.Meta, eventId ?? EventId.Empty, message);

    /// <summary>
    /// Logs trace information.
    /// </summary>
    public void Trace(string message, EventId? eventId = null)
        => WriteLog(LoggingLevel.Trace, eventId ?? EventId.Empty, message);

    /// <summary>
    /// Logs debug information.
    /// </summary>
    public void Debug(string message, EventId? eventId = null, [CallerMemberName] string memberName = "")
        => WriteLog(LoggingLevel.Debug, eventId ?? EventId.Empty, $"[{memberName}] {message}");

    /// <summary>
    /// Logs debug information for a specific class.
    /// </summary>
    public void Debug<TClass>(string message, EventId? eventId = null, [CallerMemberName] string memberName = "")
        where TClass : class
        => WriteLog(LoggingLevel.Debug, eventId ?? EventId.Empty, $"[{typeof(TClass).Name}:{memberName}] {message}");

    /// <summary>
    /// Logs information.
    /// </summary>
    public void Info(string format, params object[] args)
        => WriteLog(LoggingLevel.Information, EventId.Empty, string.Format(format, args));

    /// <summary>
    /// Logs information.
    /// </summary>
    public void Info(string message, EventId? eventId = null)
        => WriteLog(LoggingLevel.Information, eventId ?? EventId.Empty, message);

    /// <summary>
    /// Logs a warning message.
    /// </summary>
    public void Warn(string message, EventId? eventId = null)
        => WriteLog(LoggingLevel.Warning, eventId ?? EventId.Empty, message);

    /// <summary>
    /// Logs an error with exception.
    /// </summary>
    public void Error(Exception exception, EventId? eventId = null)
        => WriteLog(LoggingLevel.Error, eventId ?? EventId.Empty, exception.Message, exception);

    /// <summary>
    /// Logs an error with message and exception.
    /// </summary>
    public void Error(string message, Exception exception, EventId? eventId = null)
        => WriteLog(LoggingLevel.Error, eventId ?? EventId.Empty, message, exception);

    /// <summary>
    /// Logs a critical error message.
    /// </summary>
    public void Fatal(string message, EventId? eventId = null)
        => WriteLog(LoggingLevel.Critical, eventId ?? EventId.Empty, message);

    /// <summary>
    /// Logs a critical error with message and exception.
    /// </summary>
    public void Fatal(string message, Exception exception, EventId? eventId = null)
        => WriteLog(LoggingLevel.Critical, eventId ?? EventId.Empty, message, exception);
}