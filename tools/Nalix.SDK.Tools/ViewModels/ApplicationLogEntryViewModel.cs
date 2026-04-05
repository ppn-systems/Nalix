using System;
using System.Globalization;
using Nalix.SDK.Tools.Configuration;

namespace Nalix.SDK.Tools.ViewModels;

/// <summary>
/// Represents one application log entry.
/// </summary>
public sealed class ApplicationLogEntryViewModel
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ApplicationLogEntryViewModel"/> class.
    /// </summary>
    /// <param name="timestamp">The entry timestamp.</param>
    /// <param name="source">The entry source.</param>
    /// <param name="message">The log message.</param>
    /// <param name="texts">The localized text resources.</param>
    public ApplicationLogEntryViewModel(DateTimeOffset timestamp, string source, string message, PacketToolTextConfig texts)
    {
        ArgumentNullException.ThrowIfNull(texts);
        this.Timestamp = timestamp;
        this.Source = source;
        this.Message = message;
        this.Summary = string.Format(CultureInfo.CurrentCulture, texts.LogEntrySummaryFormat, timestamp, source, message);
    }

    /// <summary>
    /// Gets the entry timestamp.
    /// </summary>
    public DateTimeOffset Timestamp { get; }

    /// <summary>
    /// Gets the entry source.
    /// </summary>
    public string Source { get; }

    /// <summary>
    /// Gets the log message.
    /// </summary>
    public string Message { get; }

    /// <summary>
    /// Gets the summary text displayed in the list.
    /// </summary>
    public string Summary { get; }
}
