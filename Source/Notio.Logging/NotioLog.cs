using Notio.Common.Logging;
using Notio.Logging.Engine;
using Notio.Logging.Enums;
using Notio.Logging.Targets;
using System;
using System.Runtime.CompilerServices;

namespace Notio.Logging;

public sealed class NotioLog : LoggingEngine, ILogger
{
    private bool _isInitialized;
    private static readonly Lazy<NotioLog> _instance = new(() => new());

    private NotioLog()
    { }

    public static NotioLog Instance => _instance.Value;

    /// <summary>
    /// Initializes the logging system with optional configuration.
    /// </summary>
    public void Initialize(Action<NotioLogConfig>? configure = null)
    {
        if (_isInitialized)
            throw new InvalidOperationException("Logging has already been initialized.");

        _isInitialized = true;

        NotioLogConfig builder = new(base.Publisher);
        configure?.Invoke(builder);

        if (builder.IsDefaults)
        {
            builder.ConfigureDefaults(cfg =>
            {
                cfg.SetMinLevel(LoggingLevel.Information);
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

    /// <inheritdoc />
    public void Meta(string message, EventId? eventId = null)
        => WriteLog(LoggingLevel.Meta, eventId ?? EventId.Empty, message);
    /// <inheritdoc />
    public void Trace(string message, EventId? eventId = null)
        => WriteLog(LoggingLevel.Trace, eventId ?? EventId.Empty, SanitizeLogMessage(message));

    /// <inheritdoc />
    public void Debug(string message, EventId? eventId = null, [CallerMemberName] string memberName = "")
        => WriteLog(LoggingLevel.Debug, eventId ?? EventId.Empty, SanitizeLogMessage(message));

    /// <inheritdoc />
    public void Debug<TClass>(string message, EventId? eventId = null, [CallerMemberName] string memberName = "")
        where TClass : class
        => WriteLog(LoggingLevel.Debug, eventId ?? EventId.Empty, $"[{typeof(TClass).Name}:{memberName}] {message}");

    /// <inheritdoc />
    public void Info(string format, params object[] args)
        => WriteLog(LoggingLevel.Information, EventId.Empty, string.Format(format, args));

    /// <inheritdoc />
    public void Info(string message, EventId? eventId = null)
        => WriteLog(LoggingLevel.Information, eventId ?? EventId.Empty, message);

    /// <inheritdoc />
    public void Warn(string message, EventId? eventId = null)
        => WriteLog(LoggingLevel.Warning, eventId ?? EventId.Empty, message);

    /// <inheritdoc />
    public void Error(Exception exception, EventId? eventId = null)
        => WriteLog(LoggingLevel.Error, eventId ?? EventId.Empty, exception.Message, exception);

    /// <inheritdoc />
    public void Error(string message, Exception exception, EventId? eventId = null)
        => WriteLog(LoggingLevel.Error, eventId ?? EventId.Empty, message, exception);

    /// <inheritdoc />
    public void Fatal(string message, EventId? eventId = null)
        => WriteLog(LoggingLevel.Critical, eventId ?? EventId.Empty, message);

    /// <inheritdoc />
    public void Fatal(string message, Exception exception, EventId? eventId = null)
        => WriteLog(LoggingLevel.Critical, eventId ?? EventId.Empty, message, exception);

    // Sanitize log message to prevent log forging
    private static string SanitizeLogMessage(string message)
        // Remove potentially dangerous characters (e.g., newlines or control characters)
        => message?.Replace("\n", "").Replace("\r", "") ?? string.Empty;
}