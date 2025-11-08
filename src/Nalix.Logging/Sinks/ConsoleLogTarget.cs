// Copyright (c) 2025 PPN Corporation. All rights reserved.

using Nalix.Common.Logging;
using Nalix.Logging.Core;

namespace Nalix.Logging.Sinks;

/// <summary>
/// The ConsoleLogTarget class provides the ability to output log messages to the console,
/// with colors corresponding to the log severity levels.
/// </summary>
[System.Diagnostics.DebuggerNonUserCode]
[System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverage]
[System.Diagnostics.DebuggerDisplay("ConsoleTarget Colors={_options?.EnableColors}")]
public sealed class ConsoleLogTarget : ILoggerTarget
{
    #region Fields

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
    public ConsoleLogTarget() : this(new NLogixFormatter(true))
    {
    }

    #endregion Constructors

    #region Public Methods

    /// <summary>
    /// Outputs the log message to the console.
    /// </summary>
    /// <param name="logMessage">The log message to be outputted.</param>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining |
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
    public void Publish(LogEntry logMessage) => System.Console.WriteLine(_loggerFormatter.FormatLog(logMessage));

    #endregion Public Methods
}
