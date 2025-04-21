using Nalix.Logging.Options;
using Nalix.Logging.Targets;
using System;

namespace Nalix.Logging;

/// <summary>
/// Provides a globally accessible, lazily initialized singleton instance of the <see cref="NLogix"/> logger.
///
/// This class ensures that the logger is initialized only once during the application lifetime,
/// with default logging targets configured for both console and file output.
///
/// Use <see cref="Instance"/> to retrieve the shared logger instance throughout the application.
/// </summary>
public sealed class NLogixHost
{
    #region Singleton Instance

    /// <summary>
    /// The lazy-loaded singleton instance of the <see cref="NLogix"/> logger.
    /// The logger is configured during initialization with default targets.
    /// </summary>
    private static readonly Lazy<NLogix> _instance = new(() =>
        new NLogix(delegate (LogOptions cfg)
        {
            // Configure default logging outputs
            cfg.AddTarget(new ConsoleLogTarget())
               .AddTarget(new FileLogTarget());
        }));

    /// <summary>
    /// Gets the global singleton instance of the <see cref="NLogix"/> logger.
    /// Use this property to log messages application-wide.
    /// </summary>
    public static NLogix Instance => _instance.Value;

    #endregion Singleton Instance
}
