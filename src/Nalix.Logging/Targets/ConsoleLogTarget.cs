using Nalix.Common.Logging;
using Nalix.Logging.Formatters;
using Nalix.Logging.Options;

namespace Nalix.Logging.Targets;

/// <summary>
/// The ConsoleLogTarget class provides the ability to output log messages to the console,
/// with colors corresponding to the log severity levels.
/// </summary>
public sealed class ConsoleLogTarget : ILoggerTarget
{
    #region Fields

    private readonly ConsoleLogOptions? _options;
    private readonly ILoggerFormatter _loggerFormatter;

    #endregion Fields

    #region Constructors

    /// <summary>
    /// Initializes a new instance of the <see cref="ConsoleLogTarget"/> class with a default log formatter.
    /// </summary>
    /// <param name="loggerFormatter">The object responsible for formatting the log message.</param>
    public ConsoleLogTarget(ILoggerFormatter loggerFormatter)
    {
        System.ArgumentNullException.ThrowIfNull(loggerFormatter);

        _loggerFormatter = loggerFormatter;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="ConsoleLogTarget"/> class with a default log formatter.
    /// </summary>
    public ConsoleLogTarget() : this(new LoggingFormatter(true))
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="ConsoleLogTarget"/> class.
    /// </summary>
    /// <param name="options">The console logging options.</param>
    public ConsoleLogTarget(ConsoleLogOptions options)
        : this(new LoggingFormatter(options.EnableColors))
    {
        _options = options;
    }

    #endregion Constructors

    #region Public Methods

    /// <summary>
    /// Outputs the log message to the console.
    /// </summary>
    /// <param name="logMessage">The log message to be outputted.</param>
    public void Publish(LogEntry logMessage)
    {
        if (_options?.UseStandardError == true)
        {
            System.Console.Error.WriteLine(_loggerFormatter.FormatLog(logMessage));
        }
        else
        {
            System.Console.WriteLine(_loggerFormatter.FormatLog(logMessage));
        }
    }

    #endregion Public Methods
}
