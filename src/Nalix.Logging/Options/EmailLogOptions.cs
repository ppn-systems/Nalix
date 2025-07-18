using Nalix.Common.Logging;

namespace Nalix.Logging.Options;

/// <summary>
/// Represents the configuration options for logging via email.
/// </summary>
public sealed class EmailLogOptions
{
    #region Fields

    private System.Int32 _port;
    private System.String _to = null!;
    private System.String _from = null!;
    private System.String _password = null!;
    private System.String _smtpServer = null!;

    #endregion Fields

    /// <summary>
    /// Gets or sets the SMTP server address.
    /// </summary>
    /// <exception cref="System.ArgumentNullException">Thrown if the value is null or empty.</exception>
    public System.String SmtpServer
    {
        get => _smtpServer;
        set => _smtpServer = System.String.IsNullOrWhiteSpace(value)
            ? throw new System.ArgumentNullException(nameof(SmtpServer), "SMTP server cannot be null or empty.")
            : value;
    }

    /// <summary>
    /// Gets or sets the SMTP server port.
    /// </summary>
    /// <exception cref="System.ArgumentOutOfRangeException">Thrown if the value is not between 1 and 65535.</exception>
    public System.Int32 Port
    {
        get => _port;
        set => _port = value is < 1 or > 65535
            ? throw new System.ArgumentOutOfRangeException(nameof(Port), "Port must be between 1 and 65535.")
            : value;
    }

    /// <summary>
    /// Gets or sets the sender's email address.
    /// </summary>
    /// <exception cref="System.ArgumentNullException">Thrown if the value is null or empty.</exception>
    public System.String From
    {
        get => _from;
        set => _from = System.String.IsNullOrWhiteSpace(value)
            ? throw new System.ArgumentNullException(nameof(From), "Sender email cannot be null or empty.")
            : value;
    }

    /// <summary>
    /// Gets or sets the recipient's email address.
    /// </summary>
    /// <exception cref="System.ArgumentNullException">Thrown if the value is null or empty.</exception>
    public System.String To
    {
        get => _to;
        set => _to = System.String.IsNullOrWhiteSpace(value)
            ? throw new System.ArgumentNullException(nameof(To), "Recipient email cannot be null or empty.")
            : value;
    }

    /// <summary>
    /// Gets or sets the password for authentication with the SMTP server.
    /// </summary>
    /// <exception cref="System.ArgumentNullException">Thrown if the value is null or empty.</exception>
    public System.String Password
    {
        get => _password;
        set => _password = System.String.IsNullOrWhiteSpace(value)
            ? throw new System.ArgumentNullException(nameof(Password), "Password cannot be null or empty.")
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
    public System.Boolean EnableSsl { get; set; } = true;

    /// <summary>
    /// Gets or sets the timeout for the SMTP operation in milliseconds.
    /// Defaults to 30,000 milliseconds (30 seconds).
    /// </summary>
    public System.Int32 Timeout { get; set; } = 30000;
}
