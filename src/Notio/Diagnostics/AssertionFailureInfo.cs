using System;
using System.Diagnostics;

namespace Notio.Diagnostics;

/// <summary>
/// Contains information about an assertion failure.
/// </summary>
/// <remarks>
/// Initializes a new instance of the <see cref="AssertionFailureInfo"/> class.
/// </remarks>
public sealed class AssertionFailureInfo(
    string message,
    string parameterName,
    string memberName,
    string filePath,
    int lineNumber,
    StackTrace stackTrace,
    DateTime timestamp)
{
    /// <summary>
    /// Gets the error message.
    /// </summary>
    public string Message { get; } = message;

    /// <summary>
    /// Gets the parameter name associated with the failure, if applicable.
    /// </summary>
    public string ParameterName { get; } = parameterName;

    /// <summary>
    /// Gets the name of the method where the assertion failed.
    /// </summary>
    public string MemberName { get; } = memberName;

    /// <summary>
    /// Gets the path to the file where the assertion failed.
    /// </summary>
    public string FilePath { get; } = filePath;

    /// <summary>
    /// Gets the line Number where the assertion failed.
    /// </summary>
    public int LineNumber { get; } = lineNumber;

    /// <summary>
    /// Gets the stack trace at the time of the failure, if captured.
    /// </summary>
    public StackTrace StackTrace { get; } = stackTrace;

    /// <summary>
    /// Gets the timestamp when the failure occurred.
    /// </summary>
    public DateTime Timestamp { get; } = timestamp;

    /// <summary>
    /// Returns a string representation of the assertion failure.
    /// </summary>
    public override string ToString()
        => $"[{Timestamp:yyyy-MM-dd HH:mm:ss.fff}] {Message} at {MemberName} in {FilePath}:line {LineNumber}";
}
