using Notio.Logging.Options;
using Notio.Logging.Targets;
using System;

namespace Notio.Logging;

/// <summary>
/// Provides a singleton instance of the NLogix logger.
/// This class ensures that only one instance of the logger is created throughout the application lifecycle.
/// </summary>
public sealed class NLogixSi
{
    #region Singleton Instance

    /// <summary>
    /// Lazy-loaded instance of the NLogix logger.
    /// </summary>
    private static readonly Lazy<NLogix> _instance = new(() =>
        new NLogix(delegate (LogOptions cfg)
        {
            // Add default logging targets (Console and File)
            cfg.AddTarget(new ConsoleLogTarget())
               .AddTarget(new FileLogTarget());
        }));

    /// <summary>
    /// Provides access to the singleton instance of the NLogix logger.
    /// </summary>
    public static NLogix Instance => _instance.Value;

    #endregion
}
