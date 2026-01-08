// Copyright (c) 2025 PPN Corporation. All rights reserved.

using Nalix.Logging.Sinks;

namespace Nalix.Logging;

public sealed partial class NLogix
{
    /// <summary>
    /// <para>Provides a globally accessible, lazily initialized singleton instance of the <see cref="NLogix"/> logger.</para>
    /// <para>
    /// This class ensures that the logger is initialized only once during the application lifetime,
    /// with default logging targets configured for both console and file output.
    /// </para>
    /// <para>Use <see cref="Instance"/> to retrieve the shared logger instance throughout the application.</para>
    /// </summary>
    public sealed class Host
    {
        /// <summary>
        /// The lazy-loaded singleton instance of the <see cref="NLogix"/> logger.
        /// The logger is configured during initialization with default targets.
        /// </summary>
        private static readonly System.Lazy<NLogix> _instance = new(static () =>
            new NLogix(cfg =>
            {
                // Configure default logging outputs
                _ = cfg.RegisterTarget(new BatchFileLogTarget())
                       .RegisterTarget(new BatchConsoleLogTarget());
            })
        );

        /// <summary>
        /// Gets the global singleton instance of the <see cref="NLogix"/> logger.
        /// Use this property to log messages application-wide.
        /// </summary>
        public static NLogix Instance => _instance.Value;
    }
}
