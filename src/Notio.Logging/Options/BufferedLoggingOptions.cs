namespace Notio.Logging.Options;

/// <summary>
/// Defines the options for buffered logging.
/// </summary>
public class BufferedLoggingOptions
{
    /// <summary>
    /// Gets or sets the options for the file logger.
    /// </summary>
    public FileLoggerOptions FileLoggerOptions { get; set; } = new FileLoggerOptions();

    /// <summary>
    /// Gets or sets the maximum number of log entries to store in the buffer before flushing.
    /// </summary>
    public int MaxBufferSize { get; set; } = 100;

    /// <summary>
    /// Gets or sets the interval at which the buffer will be automatically flushed.
    /// </summary>
    public System.TimeSpan FlushInterval { get; set; } = System.TimeSpan.FromSeconds(30);
}
