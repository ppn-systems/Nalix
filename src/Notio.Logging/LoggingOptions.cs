using Notio.Common.Enums;
using Notio.Common.Logging;
using System;
using System.IO;

namespace Notio.Logging
{
    /// <summary>
    /// Configures logging settings for the application.
    /// </summary>
    public sealed class LoggingOptions : IDisposable
    {
        private static readonly string _baseDirectory = AppDomain.CurrentDomain.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar);
        private readonly ILoggingPublisher _publisher;
        private bool _disposed;

        /// <summary>
        /// The minimum logging level required to log messages.
        /// </summary>
        public LoggingLevel MinimumLevel { get; private set; } = LoggingLevel.Trace;

        /// <summary>
        /// Indicates whether the default configuration is being used.
        /// </summary>
        internal bool IsDefaults { get; private set; } = true;

        /// <summary>
        /// Gets the directory path where log files are stored.
        /// </summary>
        public string LogDirectory { get; private set; } = Path.Combine(_baseDirectory, "Logs");

        /// <summary>
        /// Gets the default log file name.
        /// </summary>
        public string LogFileName { get; private set; } = "Logging-Notio";

        /// <summary>
        /// Initializes a new instance of the <see cref="LoggingOptions"/> class.
        /// </summary>
        /// <param name="publisher">The <see cref="ILoggingPublisher"/> instance for publishing log messages.</param>
        internal LoggingOptions(ILoggingPublisher publisher) => _publisher = publisher;

        /// <summary>
        /// Applies default configuration settings to the logging configuration.
        /// </summary>
        /// <param name="configure">The default configuration action.</param>
        /// <returns>The current <see cref="LoggingOptions"/> instance.</returns>
        public LoggingOptions ConfigureDefaults(Func<LoggingOptions, LoggingOptions> configure)
            => configure(this);

        /// <summary>
        /// Adds a logging target to the configuration.
        /// </summary>
        /// <param name="target">The <see cref="ILoggingTarget"/> to be added.</param>
        /// <returns>The current <see cref="LoggingOptions"/> instance.</returns>
        public LoggingOptions AddTarget(ILoggingTarget target)
        {
            IsDefaults = false;

            _publisher.AddTarget(target);
            return this;
        }

        /// <summary>
        /// Sets the minimum logging level.
        /// </summary>
        /// <param name="level">The minimum <see cref="LoggingLevel"/>.</param>
        /// <returns>The current <see cref="LoggingOptions"/> instance.</returns>
        public LoggingOptions SetMinLevel(LoggingLevel level)
        {
            IsDefaults = false;

            MinimumLevel = level;
            return this;
        }

        /// <summary>
        /// Sets the directory path for storing log files.
        /// </summary>
        /// <param name="directory">The new directory path.</param>
        /// <returns>The current <see cref="LoggingOptions"/> instance.</returns>
        /// <exception cref="ArgumentException">Thrown when the directory path is invalid.</exception>
        public LoggingOptions SetLogDirectory(string directory)
        {
            if (string.IsNullOrWhiteSpace(directory))
                throw new ArgumentException("Invalid directory.", nameof(directory));

            if (!Directory.Exists(directory))
                Directory.CreateDirectory(directory);

            IsDefaults = false;

            LogDirectory = directory;
            return this;
        }

        /// <summary>
        /// Sets the name of the log file.
        /// </summary>
        /// <param name="fileName">The new log file name.</param>
        /// <returns>The current <see cref="LoggingOptions"/> instance.</returns>
        /// <exception cref="ArgumentException">Thrown when the file name is invalid.</exception>
        public LoggingOptions SetLogFileName(string fileName)
        {
            if (string.IsNullOrWhiteSpace(fileName))
                throw new ArgumentException("Invalid file name.", nameof(fileName));

            IsDefaults = false;

            LogFileName = fileName;
            return this;
        }

        /// <summary>
        /// Validates the current configuration.
        /// </summary>
        /// <exception cref="InvalidOperationException">Thrown when configuration is invalid.</exception>
        public void Validate()
        {
            if (string.IsNullOrWhiteSpace(LogDirectory))
                throw new InvalidOperationException("Log directory is not set.");

            if (string.IsNullOrWhiteSpace(LogFileName))
                throw new InvalidOperationException("Log file name is not set.");
        }

        /// <summary>
        /// Releases unmanaged resources and performs other cleanup operations.
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Releases unmanaged and - optionally - managed resources.
        /// </summary>
        /// <param name="disposing"><c>true</c> to release both managed and unmanaged resources; <c>false</c> to release only unmanaged resources.</param>
        private void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                if (disposing)
                {
                    _publisher?.Dispose();
                }

                _disposed = true;
            }
        }

        ~LoggingOptions()
        {
            Dispose(false);
        }
    }
}
