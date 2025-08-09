namespace Nalix.Common.Logging;

/// <summary>
/// Defines a contract for formatting <see cref="LogEntry"/> objects into displayable or storable text.
/// </summary>
/// <remarks>
/// Implementations of this interface determine how log entries are represented as strings.
/// This may include adding timestamps, log levels, event IDs, or applying specific formatting styles (e.g., JSON, plain text).
/// </remarks>
public interface ILoggerFormatter
{
    /// <summary>
    /// Formats the specified <see cref="LogEntry"/> into a string representation.
    /// </summary>
    /// <param name="message">
    /// The log entry to be formatted.
    /// </param>
    /// <returns>
    /// A string containing the formatted log message.
    /// </returns>
    System.String FormatLog(LogEntry message);
}
