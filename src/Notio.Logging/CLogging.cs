using Notio.Common.Logging;
using Notio.Logging.Engine;
using Notio.Logging.Options;
using Notio.Logging.Targets;
using System;

namespace Notio.Logging;

/// <summary>
/// A singleton class that provides logging functionality for the application.
/// </summary>
public sealed partial class CLogging : LoggingEngine, ILogger
{
    #region Properties

    /// <summary>
    /// Gets the single instance of the <see cref="CLogging"/> class.
    /// </summary>
    public static CLogging Instance { get; set; } = new CLogging(delegate (LoggingOptions cfg)
    {
        cfg.AddTarget(new ConsoleLoggingTarget())
           .AddTarget(new FileLoggingTarget());
    });

    #endregion

    #region Constructors

    /// <summary>
    /// Initializes the logging system with optional configuration.
    /// </summary>
    /// <param name="configure">An optional action to configure the logging system.</param>
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Style", "IDE0290:Use primary constructor", Justification = "<Pending>")]
    public CLogging(Action<LoggingOptions>? configure = null)
        : base(configure)
    {
    }

    #endregion
}
