using Notio.Logging.Targets;
using System;
using System.Collections.Concurrent;
using System.Threading.Tasks;

namespace Notio.Logging.Internal.File
{
    /// <summary>
    /// A provider for general file logging.
    /// </summary>
    public class FileLoggerProvider : IDisposable
    {
        private readonly ConcurrentDictionary<string, FileLoggingTarget> loggers = new();
        private readonly BlockingCollection<string> entryQueue = new(1024);
        private readonly Task processQueueTask;
        private readonly FileWriter fWriter;

        internal FileLoggerOptions Options { get; private set; }

        /// <summary>
        /// Gets or sets the directory where log files will be stored.
        /// </summary>
        public string LogDirectory;

        /// <summary>
        /// Gets or sets the name of the log file.
        /// </summary>
        public string LogFileName;

        /// <summary>
        /// Gets a value indicating whether the log file will be appended to (true) or overwritten (false).
        /// </summary>
        public bool Append => Options.Append;

        /// <summary>
        /// Gets the maximum size of the log file before it rolls over.
        /// </summary>
        public int MaxFileSize => Options.MaxFileSize;

        /// <summary>
        /// Custom formatter for log file names.
        /// </summary>
        public Func<string, string>? FormatLogFileName
        {
            get => Options.FormatLogFileName;
            set { Options.FormatLogFileName = value; }
        }

        /// <summary>
        /// Custom handler for file errors.
        /// </summary>
        public Action<FileError>? HandleFileError
        {
            get => Options.HandleFileError;
            set { Options.HandleFileError = value; }
        }

        /// <summary>
        /// Initializes a new instance of <see cref="FileLoggerProvider"/> with the specified file name and default overwrite option.
        /// </summary>
        /// <param name="directory">The directory where the log file will be stored.</param>
        /// <param name="fileName">The log file name.</param>
        public FileLoggerProvider(string directory, string fileName)
            : this(directory, fileName, true)
        {
        }

        /// <summary>
        /// Initializes a new instance of <see cref="FileLoggerProvider"/> with the specified file name and overwrite option.
        /// </summary>
        /// <param name="directory">The directory where the log file will be stored.</param>
        /// <param name="fileName">The log file name.</param>
        /// <param name="append">The overwrite option.</param>
        public FileLoggerProvider(string directory, string fileName, bool append)
            : this(directory, fileName, new FileLoggerOptions() { Append = append })
        {
        }

        /// <summary>
        /// Initializes a new instance of <see cref="FileLoggerProvider"/> with the specified file name and configuration options.
        /// </summary>
        /// <param name="directory">The directory where the log file will be stored.</param>
        /// <param name="fileName">The log file name.</param>
        /// <param name="options">The configuration options.</param>
        public FileLoggerProvider(string directory, string fileName, FileLoggerOptions options)
        {
            Options = options;
            LogDirectory = directory;
            LogFileName = Environment.ExpandEnvironmentVariables(fileName);

            fWriter = new FileWriter(this);
            processQueueTask = Task.Factory.StartNew(
                ProcessQueue,
                this,
                TaskCreationOptions.LongRunning);
        }

        /// <summary>
        /// Releases the resources used by the <see cref="FileLoggerProvider"/>.
        /// </summary>
        public void Dispose()
        {
            entryQueue.CompleteAdding();
            try
            {
                processQueueTask.Wait(1500);  // similar to ConsoleLogger
            }
            catch (TaskCanceledException)
            {
            }
            catch (AggregateException ex) when (ex.InnerExceptions.Count == 1 && ex.InnerExceptions[0] is TaskCanceledException) { }

            loggers.Clear();
            fWriter.Close();
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Writes a log entry to the queue.
        /// </summary>
        /// <param name="message">The log message.</param>
        internal void WriteEntry(string message)
        {
            if (!entryQueue.IsAddingCompleted)
            {
                try
                {
                    entryQueue.Add(message);
                    return;
                }
                catch (InvalidOperationException) { }
            }
            // Do nothing if unable to add message
        }

        private void ProcessQueue()
        {
            var writeMessageFailed = false;
            foreach (var message in entryQueue.GetConsumingEnumerable())
            {
                try
                {
                    if (!writeMessageFailed)
                        fWriter.WriteMessage(message, entryQueue.Count == 0);
                }
                catch (Exception ex)
                {
                    // Handle errors if 'HandleFileError' is provided
                    var stopLogging = true;
                    if (HandleFileError != null)
                    {
                        var fileErr = new FileError(LogFileName, ex);
                        try
                        {
                            HandleFileError(fileErr);
                            if (fileErr.NewLogFileName != null)
                            {
                                fWriter.UseNewLogFile(fileErr.NewLogFileName);
                                // Write the failed message to the new log file
                                fWriter.WriteMessage(message, entryQueue.Count == 0);
                                stopLogging = false;
                            }
                        }
                        catch
                        {
                            // Ignore errors in HandleFileError or invalid file name proposals
                        }
                    }
                    if (stopLogging)
                    {
                        // Stop processing log messages as we cannot write to the log file
                        entryQueue.CompleteAdding();
                        writeMessageFailed = true;
                    }
                }
            }
        }

        private static void ProcessQueue(object? state)
        {
            if (state == null)
                return;

            FileLoggerProvider fileLogger = (FileLoggerProvider)state;
            fileLogger.ProcessQueue();
        }
    }
}