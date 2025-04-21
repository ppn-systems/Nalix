using Nalix.Common.Logging;
using System;

namespace Nalix.Logging.Options;

/// <summary>
/// Represents the configuration options for logging via email.
/// </summary>
public sealed class EmailLogOptions
{
    #region Fields

    private int _port;
    private string _to = null!;
    private string _from = null!;
    private string _password = null!;
    private string _smtpServer = null!;

    #endregion Fields

    /// <summary>
    /// Gets or sets the SMTP server address.
    /// </summary>
    /// <exception cref="ArgumentNullException">Thrown if the value is null or empty.</exception>
    public string SmtpServer
    {
        get => _smtpServer;
        set => _smtpServer = string.IsNullOrWhiteSpace(value)
            ? throw new ArgumentNullException(nameof(SmtpServer), "SMTP server cannot be null or empty.")
            : value;
    }

    /// <summary>
    /// Gets or sets the SMTP server port.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">Thrown if the value is not between 1 and 65535.</exception>
    public int Port
    {
        get => _port;
        set => _port = value is < 1 or > 65535
            ? throw new ArgumentOutOfRangeException(nameof(Port), "Port must be between 1 and 65535.")
            : value;
    }

    /// <summary>
    /// Gets or sets the sender's email address.
    /// </summary>
    /// <exception cref="ArgumentNullException">Thrown if the value is null or empty.</exception>
    public string From
    {
        get => _from;
        set => _from = string.IsNullOrWhiteSpace(value)
            ? throw new ArgumentNullException(nameof(From), "Sender email cannot be null or empty.")
            : value;
    }

    /// <summary>
    /// Gets or sets the recipient's email address.
    /// </summary>
    /// <exception cref="ArgumentNullException">Thrown if the value is null or empty.</exception>
    public string To
    {
        get => _to;
        set => _to = string.IsNullOrWhiteSpace(value)
            ? throw new ArgumentNullException(nameof(To), "Recipient email cannot be null or empty.")
            : value;
    }

    /// <summary>
    /// Gets or sets the password for authentication with the SMTP server.
    /// </summary>
    /// <exception cref="ArgumentNullException">Thrown if the value is null or empty.</exception>
    public string Password
    {
        get => _password;
        set => _password = string.IsNullOrWhiteSpace(value)
            ? throw new ArgumentNullException(nameof(Password), "Password cannot be null or empty.")
            : value;
    }

    /// <summary>
    /// Gets or sets the minimum log level required for an email to be sent.
    /// Defaults to <see cref="LogLevel.Error"/>.
    /// </summary>
    public LogLevel MinimumLevel { get; set; } = LogLevel.Error;

    /// <summary>
    /// Gets or sets a value indicating whether SSL is enabled for the SMTP connection.
    /// Defaults to <c>true</c>.
    /// </summary>
    public bool EnableSsl { get; set; } = true;

    /// <summary>
    /// Gets or sets the timeout for the SMTP operation in milliseconds.
    /// Defaults to 30,000 milliseconds (30 seconds).
    /// </summary>
    public int Timeout { get; set; } = 30000;
}
