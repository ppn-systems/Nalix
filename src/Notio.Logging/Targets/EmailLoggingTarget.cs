using Notio.Common.Exceptions;
using Notio.Common.Logging;
using Notio.Logging.Options;
using System;
using System.Net;
using System.Net.Mail;
using System.Threading.Tasks;

namespace Notio.Logging.Targets;

/// <summary>
/// Logging target that sends log messages via email.
/// </summary>
/// <remarks>
/// This target sends logs to a specified email address using an SMTP server.
/// It only logs messages that meet the minimum logging level.
/// </remarks>
public sealed class EmailLoggingTarget : ILoggerTarget, IDisposable
{
    private readonly SmtpClient _smtpClient;
    private readonly EmailLoggingOptions _options;

    private bool _disposed;

    /// <summary>
    /// Initializes a new instance of the <see cref="EmailLoggingTarget"/> class using the provided configuration options.
    /// </summary>
    /// <param name="configure">The configuration options for email logging.</param>
    /// <exception cref="ArgumentNullException">Thrown if <paramref name="configure"/> is null.</exception>
    public EmailLoggingTarget(EmailLoggingOptions configure)
    {
        _options = configure ?? throw new ArgumentNullException(nameof(configure));

        _smtpClient = new SmtpClient(_options.SmtpServer, _options.Port)
        {
            Credentials = new NetworkCredential(_options.From, _options.Password),
            EnableSsl = _options.EnableSsl,
            Timeout = _options.Timeout
        };
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="EmailLoggingTarget"/> class.
    /// </summary>
    /// <param name="smtpServer">SMTP server address.</param>
    /// <param name="port">SMTP server port.</param>
    /// <param name="from">Sender email address.</param>
    /// <param name="to">Recipient email address.</param>
    /// <param name="password">SMTP authentication password.</param>
    /// <param name="minimumLevel">Minimum log level required to send an email.</param>
    /// <param name="enableSsl">Specifies whether SSL should be enabled.</param>
    /// <param name="timeout">Timeout in milliseconds for SMTP operations.</param>
    /// <exception cref="ArgumentNullException">Thrown if any required parameter is null or empty.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown if port is out of the valid range (1-65535).</exception>
    public EmailLoggingTarget(
        string smtpServer,
        int port,
        string from,
        string to,
        string password,
        LogLevel minimumLevel = LogLevel.Error,
        bool enableSsl = true,
        int timeout = 30000)
        : this(new EmailLoggingOptions
        {
            SmtpServer = smtpServer,
            Port = port,
            From = from,
            To = to,
            Password = password,
            MinimumLevel = minimumLevel,
            EnableSsl = enableSsl,
            Timeout = timeout
        })
    {
    }

    /// <summary>
    /// Asynchronously publishes a log entry by sending an email.
    /// </summary>
    /// <param name="entry">The log entry to send via email.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    /// <exception cref="ObjectDisposedException">Thrown if the instance is disposed.</exception>
    /// <exception cref="InternalErrorException">Thrown if email sending fails.</exception>
    public async Task PublishAsync(LogEntry entry)
    {
        ObjectDisposedException.ThrowIf(_disposed, nameof(EmailLoggingTarget));

        if (entry.LogLevel < _options.MinimumLevel)
            return;

        using var mailMessage = CreateMailMessage(entry);

        try
        {
            await _smtpClient.SendMailAsync(mailMessage).ConfigureAwait(false);
        }
        catch (SmtpException ex)
        {
            throw new InternalErrorException("Failed to send email log", ex);
        }
    }

    /// <summary>
    /// Publishes a log entry synchronously by sending an email.
    /// </summary>
    /// <param name="entry">The log entry to send via email.</param>
    public void Publish(LogEntry entry)
        => PublishAsync(entry).GetAwaiter().GetResult();

    /// <summary>
    /// Creates an HTML-formatted email message from the log entry.
    /// </summary>
    /// <param name="entry">The log entry.</param>
    /// <returns>A formatted <see cref="MailMessage"/> instance.</returns>
    private MailMessage CreateMailMessage(LogEntry entry)
    {
        string exceptionHtml = entry.Exception is not null
                ? $"<p><b>Exception:</b> <pre style='background:#f8f9fa; padding:10px;'>{entry.Exception}</pre></p>"
                : "";

        string htmlBody = string.Format(HtmlTemplate,
            entry.TimeStamp,
            GetLogLevelColor(entry.LogLevel),
            entry.LogLevel,
            entry.Message,
            exceptionHtml
        );

        return new MailMessage(_options.From, _options.To)
        {
            Subject = $"[{entry.LogLevel}] - {DateTime.Now:yyyy-MM-dd HH:mm:ss}",
            Body = htmlBody,
            IsBodyHtml = true,
            Priority = entry.LogLevel == LogLevel.Critical ? MailPriority.High : MailPriority.Normal
        };
    }

    /// <summary>
    /// Gets the color associated with each log level.
    /// </summary>
    /// <param name="level">The log level.</param>
    /// <returns>A hex color code.</returns>
    private static string GetLogLevelColor(LogLevel level) => level switch
    {
        LogLevel.Trace => "#6c757d",         // Gray
        LogLevel.Debug => "#007bff",         // Blue
        LogLevel.Information => "#17a2b8",   // Cyan
        LogLevel.Warning => "#ffc107",       // Yellow
        LogLevel.Error => "#dc3545",         // Red
        LogLevel.Critical => "#b71c1c",      // Dark Red
        _ => "#000000"                       // Black
    };

    /// <summary>
    /// Disposes of resources used by the <see cref="EmailLoggingTarget"/>.
    /// </summary>
    public void Dispose()
    {
        if (_disposed) return;

        _smtpClient?.Dispose();
        _disposed = true;

        GC.SuppressFinalize(this);
    }

    private const string HtmlTemplate = @"
        <html>
        <body style='font-family: Arial, sans-serif; font-size: 14px;'>
            <h3 style='color: #333;'>Log Notification</h3>
            <p><b>Timestamp:</b> {0}</p>
            <p><b>Level:</b> <span style='color:{1};'>{2}</span></p>
            <p><b>Message:</b> {3}</p>
            {4}
        </body>
        </html>";
}
