using Notio.Common.Logging;
using Notio.Logging.Engine;
using Notio.Logging.Options;
using Notio.Logging.Targets;
using System;

namespace Notio.Logging;

/// <summary>
/// A singleton class that provides logging functionality for the application.
/// </summary>
public sealed partial class NLogix : LogEngine, ILogger
{
    #region Properties

    /// <summary>
    /// Gets the single instance of the <see cref="NLogix"/> class.
    /// </summary>
    public static NLogix Instance { get; set; } = new NLogix(delegate (LogOptions cfg)
    {
        cfg.AddTarget(new ConsoleLogTarget())
           .AddTarget(new FileLogTarget());
    });

    #endregion

    #region Constructors

    /// <summary>
    /// Initializes the logging system with optional configuration.
    /// </summary>
    /// <param name="configure">An optional action to configure the logging system.</param>
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Style", "IDE0290:Use primary constructor", Justification = "<Pending>")]
    public NLogix(Action<LogOptions>? configure = null)
        : base(configure)
    {
    }

    #endregion
}
