// Copyright (c) 2025 PPN Corporation. All rights reserved.

using Nalix.Common.Logging;

namespace Nalix.Logging.Core;

/// <summary>
/// Provides rich error context with metadata for structured error handling.
/// </summary>
/// <remarks>
/// This class captures comprehensive error information including timestamp, thread context,
/// correlation IDs, and performance metrics to facilitate debugging and monitoring.
/// </remarks>
[System.Diagnostics.DebuggerDisplay("Error={ErrorMessage}, Level={Severity}, CorrelationId={CorrelationId}")]
public sealed class StructuredErrorContext
{
    #region Properties

    /// <summary>
    /// Gets the unique correlation ID for tracking related errors across system boundaries.
    /// </summary>
    public System.String CorrelationId { get; }

    /// <summary>
    /// Gets the timestamp when the error occurred (UTC).
    /// </summary>
    public System.DateTime Timestamp { get; }

    /// <summary>
    /// Gets the error message.
    /// </summary>
    public System.String ErrorMessage { get; }

    /// <summary>
    /// Gets the exception that caused the error, if any.
    /// </summary>
    public System.Exception? Exception { get; }

    /// <summary>
    /// Gets the severity level of the error.
    /// </summary>
    public LogLevel Severity { get; }

    /// <summary>
    /// Gets the ID of the thread where the error occurred.
    /// </summary>
    public System.Int32 ThreadId { get; }

    /// <summary>
    /// Gets the name of the thread where the error occurred, if available.
    /// </summary>
    public System.String? ThreadName { get; }

    /// <summary>
    /// Gets the name of the logging target where the error occurred.
    /// </summary>
    public System.String? TargetName { get; }

    /// <summary>
    /// Gets the log entry that was being processed when the error occurred, if available.
    /// </summary>
    public LogEntry? OriginalLogEntry { get; }

    /// <summary>
    /// Gets the elapsed time for the operation that failed, if available.
    /// </summary>
    public System.TimeSpan? ElapsedTime { get; }

    /// <summary>
    /// Gets the number of retry attempts made before this error.
    /// </summary>
    public System.Int32 RetryAttempt { get; }

    /// <summary>
    /// Gets additional metadata associated with this error context.
    /// </summary>
    public System.Collections.Generic.IReadOnlyDictionary<System.String, System.Object> Metadata { get; }

    /// <summary>
    /// Gets the machine name where the error occurred.
    /// </summary>
    public System.String MachineName { get; }

    /// <summary>
    /// Gets the process ID where the error occurred.
    /// </summary>
    public System.Int32 ProcessId { get; }

    /// <summary>
    /// Gets the error category for classification.
    /// </summary>
    public ErrorCategory Category { get; }

    #endregion Properties

    #region Constructors

    /// <summary>
    /// Initializes a new instance of the <see cref="StructuredErrorContext"/> class.
    /// </summary>
    /// <param name="errorMessage">The error message.</param>
    /// <param name="exception">The exception that caused the error.</param>
    /// <param name="severity">The severity level of the error.</param>
    /// <param name="targetName">The name of the logging target where the error occurred.</param>
    /// <param name="originalLogEntry">The log entry being processed when the error occurred.</param>
    /// <param name="correlationId">The correlation ID for tracking related errors. If null, a new GUID is generated.</param>
    /// <param name="category">The error category.</param>
    /// <param name="retryAttempt">The number of retry attempts made.</param>
    /// <param name="elapsedTime">The elapsed time for the failed operation.</param>
    /// <param name="metadata">Additional metadata for the error context.</param>
    public StructuredErrorContext(
        System.String errorMessage,
        System.Exception? exception = null,
        LogLevel severity = LogLevel.Error,
        System.String? targetName = null,
        LogEntry? originalLogEntry = null,
        System.String? correlationId = null,
        ErrorCategory category = ErrorCategory.Unknown,
        System.Int32 retryAttempt = 0,
        System.TimeSpan? elapsedTime = null,
        System.Collections.Generic.IDictionary<System.String, System.Object>? metadata = null)
    {
        System.ArgumentException.ThrowIfNullOrWhiteSpace(errorMessage);

        ErrorMessage = errorMessage;
        Exception = exception;
        Severity = severity;
        TargetName = targetName;
        OriginalLogEntry = originalLogEntry;
        CorrelationId = correlationId ?? System.Guid.NewGuid().ToString("N");
        Category = category;
        RetryAttempt = retryAttempt;
        ElapsedTime = elapsedTime;
        Timestamp = System.DateTime.UtcNow;
        ThreadId = System.Environment.CurrentManagedThreadId;
        ThreadName = System.Threading.Thread.CurrentThread.Name;
        MachineName = System.Environment.MachineName;
        ProcessId = System.Environment.ProcessId;

        // Create read-only copy of metadata
        Metadata = metadata != null
            ? new System.Collections.Generic.Dictionary<System.String, System.Object>(metadata)
            : new System.Collections.Generic.Dictionary<System.String, System.Object>();
    }

    #endregion Constructors

    #region Methods

    /// <summary>
    /// Creates a new error context with an incremented retry attempt counter.
    /// </summary>
    /// <returns>A new <see cref="StructuredErrorContext"/> with incremented retry count.</returns>
    [System.Diagnostics.Contracts.Pure]
    public StructuredErrorContext WithIncrementedRetry()
    {
        return new StructuredErrorContext(
            ErrorMessage,
            Exception,
            Severity,
            TargetName,
            OriginalLogEntry,
            CorrelationId,
            Category,
            RetryAttempt + 1,
            ElapsedTime,
            new System.Collections.Generic.Dictionary<System.String, System.Object>(Metadata));
    }

    /// <summary>
    /// Returns a formatted string representation of the error context.
    /// </summary>
    /// <returns>A string containing the error context details.</returns>
    public override System.String ToString()
    {
        var sb = new System.Text.StringBuilder();
        _ = sb.AppendLine($"[Error Context - {Timestamp:yyyy-MM-dd HH:mm:ss.fff} UTC]");
        _ = sb.AppendLine($"Correlation ID: {CorrelationId}");
        _ = sb.AppendLine($"Severity: {Severity}");
        _ = sb.AppendLine($"Category: {Category}");
        _ = sb.AppendLine($"Message: {ErrorMessage}");

        if (!System.String.IsNullOrEmpty(TargetName))
        {
            _ = sb.AppendLine($"Target: {TargetName}");
        }

        _ = sb.AppendLine($"Thread: {ThreadId}" + (ThreadName != null ? $" ({ThreadName})" : ""));
        _ = sb.AppendLine($"Machine: {MachineName} (PID: {ProcessId})");

        if (RetryAttempt > 0)
        {
            _ = sb.AppendLine($"Retry Attempt: {RetryAttempt}");
        }

        if (ElapsedTime.HasValue)
        {
            _ = sb.AppendLine($"Elapsed Time: {ElapsedTime.Value.TotalMilliseconds:N2}ms");
        }

        if (Exception != null)
        {
            _ = sb.AppendLine($"Exception: {Exception.GetType().Name}");
            _ = sb.AppendLine($"Exception Message: {Exception.Message}");
        }

        if (Metadata.Count > 0)
        {
            _ = sb.AppendLine("Metadata:");
            foreach (var kvp in Metadata)
            {
                _ = sb.AppendLine($"  {kvp.Key}: {kvp.Value}");
            }
        }

        return sb.ToString();
    }

    #endregion Methods
}

/// <summary>
/// Defines error categories for classification and handling.
/// </summary>
public enum ErrorCategory
{
    /// <summary>
    /// Unknown or unclassified error.
    /// </summary>
    Unknown = 0,

    /// <summary>
    /// Network-related error (connection, timeout, etc.).
    /// </summary>
    Network = 1,

    /// <summary>
    /// Disk I/O error (file access, permissions, disk full, etc.).
    /// </summary>
    DiskIO = 2,

    /// <summary>
    /// Memory-related error (out of memory, allocation failure, etc.).
    /// </summary>
    Memory = 3,

    /// <summary>
    /// Configuration error (invalid settings, missing config, etc.).
    /// </summary>
    Configuration = 4,

    /// <summary>
    /// Permission or security error.
    /// </summary>
    Security = 5,

    /// <summary>
    /// Transient error that may succeed on retry.
    /// </summary>
    Transient = 6,

    /// <summary>
    /// Permanent error that will not succeed on retry.
    /// </summary>
    Permanent = 7,

    /// <summary>
    /// Target capacity exceeded (queue full, rate limit, etc.).
    /// </summary>
    Capacity = 8,

    /// <summary>
    /// Serialization or formatting error.
    /// </summary>
    Serialization = 9,

    /// <summary>
    /// Timeout error.
    /// </summary>
    Timeout = 10
}
