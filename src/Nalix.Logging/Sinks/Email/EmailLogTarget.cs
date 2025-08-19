// Copyright (c) 2025 PPN Corporation. All rights reserved.

using Nalix.Common.Exceptions;
using Nalix.Common.Logging;

namespace Nalix.Logging.Sinks.Email;

/// <summary>
/// Logging target that sends log messages via email.
/// </summary>
/// <remarks>
/// This target sends logs to a specified email address using an SMTP server.
/// It only sends log messages that meet or exceed the specified minimum log level.
/// </remarks>
public sealed class EmailLogTarget : ILoggerTarget, System.IDisposable
{
    #region Fields

    private readonly System.Net.Mail.SmtpClient _smtpClient;
    private readonly EmailLogOptions _options;
    private System.Boolean _disposed;

    #endregion Fields

    #region Constructors

    /// <summary>
    /// Initializes a new instance of the <see cref="EmailLogTarget"/> class using the provided configuration options.
    /// </summary>
    /// <param name="configure">The configuration options for email logging.</param>
    /// <exception cref="System.ArgumentNullException">Thrown if <paramref name="configure"/> is null.</exception>
    public EmailLogTarget(EmailLogOptions configure)
    {
        _options = configure
            ?? throw new System.ArgumentNullException(nameof(configure));

        _smtpClient = new System.Net.Mail.SmtpClient(_options.SmtpServer, _options.Port)
        {
            Credentials = new System.Net.NetworkCredential(_options.From, _options.Password),
            EnableSsl = _options.EnableSsl,
            Timeout = _options.Timeout
        };
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="EmailLogTarget"/> class with SMTP server details and email log options.
    /// </summary>
    /// <param name="smtpServer">SMTP server address.</param>
    /// <param name="port">SMTP server port.</param>
    /// <param name="from">Sender email address.</param>
    /// <param name="to">Recipient email address.</param>
    /// <param name="password">SMTP authentication password.</param>
    /// <param name="minimumLevel">Minimum log level required to send an email.</param>
    /// <param name="enableSsl">Specifies whether SSL should be enabled.</param>
    /// <param name="timeout">Timeout in milliseconds for SMTP operations.</param>
    /// <exception cref="System.ArgumentNullException">Thrown if any required parameter is null or empty.</exception>
    /// <exception cref="System.ArgumentOutOfRangeException">Thrown if port is out of the valid range (1-65535).</exception>
    public EmailLogTarget(
        System.String smtpServer, System.Int32 port,
        System.String from, System.String to, System.String password,
        LogLevel minimumLevel = LogLevel.Error,
        System.Boolean enableSsl = true, System.Int32 timeout = 30000)
        : this(new EmailLogOptions
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

    #endregion Constructors

    #region Public Methods

    /// <summary>
    /// Asynchronously publishes a log entry by sending an email.
    /// </summary>
    /// <param name="entry">The log entry to send via email.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    /// <exception cref="System.ObjectDisposedException">Thrown if the instance is disposed.</exception>
    /// <exception cref="InternalErrorException">Thrown if email sending fails.</exception>
    public async System.Threading.Tasks.Task PublishAsync(LogEntry entry)
    {
        System.ObjectDisposedException.ThrowIf(_disposed, nameof(EmailLogTarget));

        if (entry.LogLevel < _options.MinimumLevel)
        {
            return;
        }

        using var mailMessage = CreateMailMessage(entry);

        try
        {
            await _smtpClient.SendMailAsync(mailMessage).ConfigureAwait(false);
        }
        catch (System.Net.Mail.SmtpException ex)
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

    #endregion Public Methods

    #region IDisposable

    /// <summary>
    /// Disposes of resources used by the <see cref="EmailLogTarget"/>.
    /// This method ensures that any resources are cleaned up when the instance is no longer needed.
    /// </summary>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _smtpClient?.Dispose();
        _disposed = true;

        System.GC.SuppressFinalize(this);
    }

    #endregion IDisposable

    #region Private Methods

    /// <summary>
    /// Creates an HTML-formatted email message from the log entry.
    /// </summary>
    /// <param name="entry">The log entry.</param>
    /// <returns>A formatted <see cref="System.Net.Mail.MailMessage"/> instance.</returns>
    private System.Net.Mail.MailMessage CreateMailMessage(LogEntry entry)
    {
        System.String exceptionHtml = entry.Exception is not null
                ? $"<p><b>Exception:</b> {entry.Exception.GetType().Name}: {entry.Exception.Message}</pre></p>"
                : "";

        System.String htmlBody = System.String.Format(HtmlTemplate,
            entry.TimeStamp,
            GetLogLevelColor(entry.LogLevel),
            entry.LogLevel,
            entry.Message,
            exceptionHtml
        );

        return new System.Net.Mail.MailMessage(_options.From, _options.To)
        {
            Subject = $"[{entry.LogLevel}] - {System.DateTime.Now:yyyy-MM-dd HH:mm:ss}",
            Body = htmlBody,
            IsBodyHtml = true,
            Priority = entry.LogLevel == LogLevel.Critical
                ? System.Net.Mail.MailPriority.High
                : System.Net.Mail.MailPriority.Normal
        };
    }

    /// <summary>
    /// Gets the color associated with each log level.
    /// </summary>
    /// <param name="level">The log level.</param>
    /// <returns>A hex color code associated with the log level.</returns>
    private static System.String GetLogLevelColor(LogLevel level) => level switch
    {
        LogLevel.Trace => "#6c757d",         // Gray
        LogLevel.Debug => "#007bff",         // Blue
        LogLevel.Information => "#17a2b8",   // Cyan
        LogLevel.Warning => "#ffc107",       // Yellow
        LogLevel.Error => "#dc3545",         // Red
        LogLevel.Critical => "#b71c1c",      // Dark Red
        _ => "#000000"                       // Black
    };

    private const System.String HtmlTemplate = @"
        <html>
        <body style='font-family: Arial, sans-serif; font-size: 14px;'>
            <h3 style='color: #333;'>Log Notification</h3>
            <p><b>Timestamp:</b> {0}</p>
            <p><b>Level:</b> <span style='color:{1};'>{2}</span></p>
            <p><b>Message:</b> {3}</p>
            {4}
        </body>
        </html>";

    #endregion Private Methods
}
