using Notio.Common.Logging;
using Notio.Logging.Core.Formatters;
using Notio.Logging.Options;
using System;

namespace Notio.Logging.Targets;

/// <summary>
/// The ConsoleLoggingTarget class provides the ability to output log messages to the console,
/// with colors corresponding to the log severity levels.
/// </summary>
public sealed class ConsoleLoggingTarget : ILoggerTarget
{
    #region Fields

    private readonly ConsoleLoggingOptions? _options;
    private readonly ILoggerFormatter _loggerFormatter;

    #endregion

    #region Constructors

    /// <summary>
    /// Initializes a new instance of the <see cref="ConsoleLoggingTarget"/> class with a default log formatter.
    /// </summary>
    /// <param name="loggerFormatter">The object responsible for formatting the log message.</param>
    public ConsoleLoggingTarget(ILoggerFormatter loggerFormatter)
    {
        ArgumentNullException.ThrowIfNull(loggerFormatter);

        _loggerFormatter = loggerFormatter;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="ConsoleLoggingTarget"/> class with a default log formatter.
    /// </summary>
    public ConsoleLoggingTarget() : this(new LoggingFormatter(true))
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="ConsoleLoggingTarget"/> class.
    /// </summary>
    /// <param name="options">The console logging options.</param>
    public ConsoleLoggingTarget(ConsoleLoggingOptions options)
        : this(new LoggingFormatter(options.EnableColors))
    {
        _options = options;
    }

    #endregion

    #region Public Methods

    /// <summary>
    /// Outputs the log message to the console.
    /// </summary>
    /// <param name="logMessage">The log message to be outputted.</param>
    public void Publish(LogEntry logMessage)
    {
        if (_options?.UseStandardError == true)
        {
            Console.Error.WriteLine(_loggerFormatter.FormatLog(logMessage));
        }
        else
        {
            Console.WriteLine(_loggerFormatter.FormatLog(logMessage));
        }
    }

    #endregion
}
