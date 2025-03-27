using Notio.Common.Exceptions;
using Notio.Common.Logging;
using System;
using System.Net;
using System.Net.Mail;
using System.Threading;
using System.Threading.Tasks;

namespace Notio.Logging.Targets;

/// <summary>
/// Logging target that sends log messages via email.
/// </summary>
/// <remarks>
/// This target sends logs to a specified email address using an SMTP server.
/// It only logs messages that meet the minimum logging level.
/// </remarks>
public sealed class EmailLoggingTarget : ILoggingTarget, IDisposable
{
    private readonly int _port;
    private readonly string _to;
    private readonly string _from;
    private readonly string _password;
    private readonly string _smtpServer;
    private readonly Lock _lock = new();
    private readonly SmtpClient _smtpClient;
    private readonly LoggingLevel _minimumLevel;

    private bool _disposed;

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
        LoggingLevel minimumLevel = LoggingLevel.Error,
        bool enableSsl = true,
        int timeout = 30000) // 30 seconds default timeout
    {
        ValidateParameters(smtpServer, port, from, to, password);

        _smtpServer = smtpServer;
        _port = port;
        _from = from;
        _to = to;
        _password = password;
        _minimumLevel = minimumLevel;

        _smtpClient = new SmtpClient(_smtpServer, _port)
        {
            Credentials = new NetworkCredential(_from, _password),
            EnableSsl = enableSsl,
            Timeout = timeout
        };
    }

    /// <summary>
    /// Asynchronously publishes a log entry by sending an email.
    /// </summary>
    /// <param name="entry">The log entry to send via email.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    /// <exception cref="ObjectDisposedException">Thrown if the instance is disposed.</exception>
    /// <exception cref="InternalErrorException">Thrown if email sending fails.</exception>
    public async Task PublishAsync(LoggingEntry entry)
    {
        ObjectDisposedException.ThrowIf(_disposed, nameof(EmailLoggingTarget));

        if (entry.LogLevel < _minimumLevel)
            return;

        MailMessage mailMessage;
        lock (_lock)
        {
            mailMessage = CreateMailMessage(entry);
        }

        try
        {
            await _smtpClient.SendMailAsync(mailMessage).ConfigureAwait(false);
        }
        catch (SmtpException ex)
        {
            throw new InternalErrorException("Failed to send email log", ex);
        }
        finally
        {
            mailMessage.Dispose();
        }
    }

    /// <summary>
    /// Publishes a log entry synchronously by sending an email.
    /// </summary>
    /// <param name="entry">The log entry to send via email.</param>
    public void Publish(LoggingEntry entry)
        => PublishAsync(entry).GetAwaiter().GetResult();

    private MailMessage CreateMailMessage(LoggingEntry entry)
        => new(_from, _to)
        {
            Subject = $"[{entry.LogLevel}] - {DateTime.Now:yyyy-MM-dd HH:mm:ss}",
            Body = $"Timestamp: {entry.TimeStamp}" +
                   $"Level: {entry.LogLevel}" +
                   $"Message: {entry.Message}" +
                   $"Exception: {entry.Exception?.ToString() ?? "None"}",
            IsBodyHtml = false,
            Priority = entry.LogLevel == LoggingLevel.Critical ? MailPriority.High : MailPriority.Normal
        };

    private static void ValidateParameters(
        string smtpServer, int port, string from, string to, string password)
    {
        if (string.IsNullOrWhiteSpace(smtpServer)) throw new ArgumentNullException(nameof(smtpServer));
        if (port < 1 || port > 65535) throw new ArgumentOutOfRangeException(nameof(port));
        if (string.IsNullOrWhiteSpace(from)) throw new ArgumentNullException(nameof(from));
        if (string.IsNullOrWhiteSpace(to)) throw new ArgumentNullException(nameof(to));
        if (string.IsNullOrWhiteSpace(password)) throw new ArgumentNullException(nameof(password));
    }

    /// <summary>
    /// Disposes of resources used by the <see cref="EmailLoggingTarget"/>.
    /// </summary>
    public void Dispose()
    {
        if (_disposed) return;

        lock (_lock)
        {
            _smtpClient?.Dispose();
            _disposed = true;
        }

        GC.SuppressFinalize(this);
    }
}
