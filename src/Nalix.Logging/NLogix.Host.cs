using Nalix.Logging.Options;
using Nalix.Logging.Targets;

namespace Nalix.Logging;

public sealed partial class NLogix
{
    /// <summary>
    /// Provides a globally accessible, lazily initialized singleton instance of the <see cref="NLogix"/> logger.
    ///
    /// This class ensures that the logger is initialized only once during the application lifetime,
    /// with default logging targets configured for both console and file output.
    ///
    /// Use <see cref="Instance"/> to retrieve the shared logger instance throughout the application.
    /// </summary>
    public sealed class Host
    {
        /// <summary>
        /// The lazy-loaded singleton instance of the <see cref="NLogix"/> logger.
        /// The logger is configured during initialization with default targets.
        /// </summary>
        private static readonly System.Lazy<NLogix> _instance = new(() =>
            new NLogix(delegate (NLogOptions cfg)
            {
                // Configure default logging outputs
                _ = cfg.AddTarget(new ConsoleLogTarget())
                       .AddTarget(new FileLogTarget());
            }));

        /// <summary>
        /// Gets the global singleton instance of the <see cref="NLogix"/> logger.
        /// Use this property to log messages application-wide.
        /// </summary>
        public static NLogix Instance => _instance.Value;
    }
}
