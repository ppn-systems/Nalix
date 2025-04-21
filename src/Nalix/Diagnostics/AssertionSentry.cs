using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Threading;

namespace Nalix.Diagnostics;

/// <summary>
/// Enhanced debug assertion utility with expanded functionality, performance optimizations, 
/// and comprehensive logging capabilities.
/// </summary>
public static class AssertionSentry
{
    #region Configuration Properties

    /// <summary>
    /// Gets or sets whether detailed exception information should be included in assertions.
    /// </summary>
    public static bool IncludeDetailedMessages { get; set; } = true;

    /// <summary>
    /// Gets or sets whether assertions should break into the debugger when available.
    /// </summary>
    public static bool BreakOnAssertFailure { get; set; } = true;

    /// <summary>
    /// Gets or sets whether to log assertion failures to the trace listener.
    /// </summary>
    public static bool LogAssertionsToTrace { get; set; } = true;

    /// <summary>
    /// Gets or sets whether to capture stack traces for assertion failures.
    /// </summary>
    public static bool CaptureStackTrace { get; set; } = true;

    /// <summary>
    /// Gets or sets the maximum Number of recent assertion failures to remember.
    /// </summary>
    public static int MaxTrackedAssertions { get; set; } = 10;

    /// <summary>
    /// Gets or sets a custom global assertion handler that is invoked on every assertion failure.
    /// </summary>
    public static Action<AssertionFailureInfo> GlobalAssertionHandler { get; set; }

    /// <summary>
    /// Gets or sets whether to throw exceptions for assertion failures.
    /// If set to false, assertions will log and break but not throw.
    /// </summary>
    public static bool ThrowExceptions { get; set; } = true;

    /// <summary>
    /// Gets a value indicating whether debug mode is enabled.
    /// This is always true in DEBUG builds and false in RELEASE builds.
    /// </summary>
    public static bool IsDebugBuild
    {
        get
        {
#if DEBUG
            return true;
#else
            return false;
#endif
        }
    }

    #endregion

    #region Fields

    /// <summary>
    /// Stores recent assertion failures for analysis.
    /// </summary>
    private static readonly Queue<AssertionFailureInfo> _recentFailures = new();

    /// <summary>
    /// Lock object for thread-safe access to the recent failures queue.
    /// </summary>
    private static readonly Lock _lock = new();

    /// <summary>
    /// StringBuilder used for formatting error messages to avoid allocations.
    /// </summary>
    [ThreadStatic]
    private static System.Text.StringBuilder _messageBuilder;

    #endregion

    #region Assert Methods

    /// <summary>
    /// Verifies that a condition is true. If the condition is false, an exception is thrown.
    /// </summary>
    /// <param name="condition">The condition to verify.</param>
    /// <param name="message">Optional message to include in the exception.</param>
    /// <param name="callerMemberName">Auto-populated with the calling method name.</param>
    /// <param name="callerFilePath">Auto-populated with the source file path.</param>
    /// <param name="callerLineNumber">Auto-populated with the source line Number.</param>
    /// <exception cref="InvalidOperationException">Thrown when the condition is false.</exception>
    [Conditional("DEBUG")]
    public static void Assert(
        [DoesNotReturnIf(false)] bool condition,
        string message = null,
        [CallerMemberName] string callerMemberName = "",
        [CallerFilePath] string callerFilePath = "",
        [CallerLineNumber] int callerLineNumber = 0)
    {
        if (!condition)
        {
            HandleAssertionFailure(
                message ?? "AssertionSentry assertion failed",
                null,
                callerMemberName,
                callerFilePath,
                callerLineNumber,
                exception => new InvalidOperationException(exception));
        }
    }

    /// <summary>
    /// Verifies that an object is not null. If the object is null, an exception is thrown.
    /// </summary>
    /// <param name="obj">The object to check for null.</param>
    /// <param name="objectName">Name of the object being checked.</param>
    /// <param name="callerMemberName">Auto-populated with the calling method name.</param>
    /// <param name="callerFilePath">Auto-populated with the source file path.</param>
    /// <param name="callerLineNumber">Auto-populated with the source line Number.</param>
    /// <exception cref="ArgumentNullException">Thrown when the object is null.</exception>
    [Conditional("DEBUG")]
    public static void AssertNotNull(
        [NotNull] object obj,
        string objectName = null,
        [CallerMemberName] string callerMemberName = "",
        [CallerFilePath] string callerFilePath = "",
        [CallerLineNumber] int callerLineNumber = 0)
    {
        if (obj == null)
        {
            HandleAssertionFailure(
                $"Object {objectName ?? "reference"} is null",
                objectName,
                callerMemberName,
                callerFilePath,
                callerLineNumber,
                exception => new ArgumentNullException(objectName, exception));
        }
    }

    /// <summary>
    /// Verifies that a string is not null or empty. If the string is null or empty, an exception is thrown.
    /// </summary>
    /// <param name="value">The string to check.</param>
    /// <param name="paramName">Name of the parameter being checked.</param>
    /// <param name="callerMemberName">Auto-populated with the calling method name.</param>
    /// <param name="callerFilePath">Auto-populated with the source file path.</param>
    /// <param name="callerLineNumber">Auto-populated with the source line Number.</param>
    /// <exception cref="ArgumentException">Thrown when the string is null or empty.</exception>
    [Conditional("DEBUG")]
    public static void AssertNotNullOrEmpty(
        [NotNull] string value,
        string paramName = null,
        [CallerMemberName] string callerMemberName = "",
        [CallerFilePath] string callerFilePath = "",
        [CallerLineNumber] int callerLineNumber = 0)
    {
        if (string.IsNullOrEmpty(value))
        {
            HandleAssertionFailure(
                $"String {paramName ?? "parameter"} is null or empty",
                paramName,
                callerMemberName,
                callerFilePath,
                callerLineNumber,
                exception => new ArgumentException(exception, paramName));
        }
    }

    /// <summary>
    /// Verifies that a string is not null, empty or whitespace. If it is, an exception is thrown.
    /// </summary>
    /// <param name="value">The string to check.</param>
    /// <param name="paramName">Name of the parameter being checked.</param>
    /// <param name="callerMemberName">Auto-populated with the calling method name.</param>
    /// <param name="callerFilePath">Auto-populated with the source file path.</param>
    /// <param name="callerLineNumber">Auto-populated with the source line Number.</param>
    /// <exception cref="ArgumentException">Thrown when the string is null, empty or whitespace.</exception>
    [Conditional("DEBUG")]
    public static void AssertNotNullOrWhiteSpace(
        [NotNull] string value,
        string paramName = null,
        [CallerMemberName] string callerMemberName = "",
        [CallerFilePath] string callerFilePath = "",
        [CallerLineNumber] int callerLineNumber = 0)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            HandleAssertionFailure(
                $"String {paramName ?? "parameter"} is null, empty or whitespace",
                paramName,
                callerMemberName,
                callerFilePath,
                callerLineNumber,
                exception => new ArgumentException(exception, paramName));
        }
    }

    /// <summary>
    /// Verifies that a value is within the specified range (inclusive).
    /// </summary>
    /// <typeparam name="T">The type of the value to check.</typeparam>
    /// <param name="value">The value to check.</param>
    /// <param name="min">The minimum acceptable value.</param>
    /// <param name="max">The maximum acceptable value.</param>
    /// <param name="paramName">Name of the parameter being checked.</param>
    /// <param name="callerMemberName">Auto-populated with the calling method name.</param>
    /// <param name="callerFilePath">Auto-populated with the source file path.</param>
    /// <param name="callerLineNumber">Auto-populated with the source line Number.</param>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when the value is outside the specified range.</exception>
    [Conditional("DEBUG")]
    public static void AssertInRange<T>(
        T value,
        T min,
        T max,
        string paramName = null,
        [CallerMemberName] string callerMemberName = "",
        [CallerFilePath] string callerFilePath = "",
        [CallerLineNumber] int callerLineNumber = 0) where T : IComparable<T>
    {
        if (value.CompareTo(min) < 0 || value.CompareTo(max) > 0)
        {
            HandleAssertionFailure(
                $"Value {paramName ?? "parameter"} ({value}) is out of range [{min}-{max}]",
                paramName,
                callerMemberName,
                callerFilePath,
                callerLineNumber,
                exception => new ArgumentOutOfRangeException(paramName, value, exception));
        }
    }

    /// <summary>
    /// Verifies that two objects are equal. If not, an exception is thrown.
    /// </summary>
    /// <typeparam name="T">The type of objects being compared.</typeparam>
    /// <param name="expected">The expected value.</param>
    /// <param name="actual">The actual value.</param>
    /// <param name="message">Optional message describing the assertion.</param>
    /// <param name="callerMemberName">Auto-populated with the calling method name.</param>
    /// <param name="callerFilePath">Auto-populated with the source file path.</param>
    /// <param name="callerLineNumber">Auto-populated with the source line Number.</param>
    /// <exception cref="InvalidOperationException">Thrown when the objects are not equal.</exception>
    [Conditional("DEBUG")]
    public static void AssertEqual<T>(
        T expected,
        T actual,
        string message = null,
        [CallerMemberName] string callerMemberName = "",
        [CallerFilePath] string callerFilePath = "",
        [CallerLineNumber] int callerLineNumber = 0)
    {
        if (!EqualityComparer<T>.Default.Equals(expected, actual))
        {
            string formattedMessage = message ?? $"Expected: {expected}, Actual: {actual}";
            HandleAssertionFailure(
                formattedMessage,
                null,
                callerMemberName,
                callerFilePath,
                callerLineNumber,
                exception => new InvalidOperationException(exception));
        }
    }

    /// <summary>
    /// Verifies that two objects are not equal. If they are, an exception is thrown.
    /// </summary>
    /// <typeparam name="T">The type of objects being compared.</typeparam>
    /// <param name="notExpected">The value that should not match.</param>
    /// <param name="actual">The actual value.</param>
    /// <param name="message">Optional message describing the assertion.</param>
    /// <param name="callerMemberName">Auto-populated with the calling method name.</param>
    /// <param name="callerFilePath">Auto-populated with the source file path.</param>
    /// <param name="callerLineNumber">Auto-populated with the source line Number.</param>
    /// <exception cref="InvalidOperationException">Thrown when the objects are equal.</exception>
    [Conditional("DEBUG")]
    public static void AssertNotEqual<T>(
        T notExpected,
        T actual,
        string message = null,
        [CallerMemberName] string callerMemberName = "",
        [CallerFilePath] string callerFilePath = "",
        [CallerLineNumber] int callerLineNumber = 0)
    {
        if (EqualityComparer<T>.Default.Equals(notExpected, actual))
        {
            string formattedMessage = message ?? $"Value should not be equal to: {notExpected}";
            HandleAssertionFailure(
                formattedMessage,
                null,
                callerMemberName,
                callerFilePath,
                callerLineNumber,
                exception => new InvalidOperationException(exception));
        }
    }

    /// <summary>
    /// Fails unconditionally with the specified message.
    /// </summary>
    /// <param name="message">The message for the failure.</param>
    /// <param name="callerMemberName">Auto-populated with the calling method name.</param>
    /// <param name="callerFilePath">Auto-populated with the source file path.</param>
    /// <param name="callerLineNumber">Auto-populated with the source line Number.</param>
    /// <exception cref="InvalidOperationException">Always thrown.</exception>
    [Conditional("DEBUG")]
    [DoesNotReturn]
    public static void Fail(
        string message = "AssertionSentry failure",
        [CallerMemberName] string callerMemberName = "",
        [CallerFilePath] string callerFilePath = "",
        [CallerLineNumber] int callerLineNumber = 0)
    {
        HandleAssertionFailure(
            message,
            null,
            callerMemberName,
            callerFilePath,
            callerLineNumber,
            exception => new InvalidOperationException(exception));
    }

    #endregion

    #region Extended Assert Methods

    /// <summary>
    /// Asserts that the code executes within the specified time limit.
    /// </summary>
    /// <param name="action">The action to execute.</param>
    /// <param name="milliseconds">Maximum allowed time in milliseconds.</param>
    /// <param name="message">Optional message to include if the assertion fails.</param>
    /// <param name="callerMemberName">Auto-populated with the calling method name.</param>
    /// <param name="callerFilePath">Auto-populated with the source file path.</param>
    /// <param name="callerLineNumber">Auto-populated with the source line Number.</param>
    [Conditional("DEBUG")]
    public static void AssertExecutionTime(
        Action action,
        int milliseconds,
        string message = null,
        [CallerMemberName] string callerMemberName = "",
        [CallerFilePath] string callerFilePath = "",
        [CallerLineNumber] int callerLineNumber = 0)
    {
        var sw = Stopwatch.StartNew();
        action();
        sw.Stop();

        if (sw.ElapsedMilliseconds > milliseconds)
        {
            HandleAssertionFailure(
                message ?? $"Execution time ({sw.ElapsedMilliseconds}ms) exceeded limit of {milliseconds}ms",
                null,
                callerMemberName,
                callerFilePath,
                callerLineNumber,
                exception => new InvalidOperationException(exception));
        }
    }

    /// <summary>
    /// Asserts that a collection is not null or empty.
    /// </summary>
    /// <typeparam name="T">The type of items in the collection.</typeparam>
    /// <param name="collection">The collection to check.</param>
    /// <param name="paramName">Name of the parameter being checked.</param>
    /// <param name="callerMemberName">Auto-populated with the calling method name.</param>
    /// <param name="callerFilePath">Auto-populated with the source file path.</param>
    /// <param name="callerLineNumber">Auto-populated with the source line Number.</param>
    /// <exception cref="ArgumentException">Thrown when the collection is null or empty.</exception>
    [Conditional("DEBUG")]
    public static void AssertNotNullOrEmpty<T>(
        IEnumerable<T> collection,
        string paramName = null,
        [CallerMemberName] string callerMemberName = "",
        [CallerFilePath] string callerFilePath = "",
        [CallerLineNumber] int callerLineNumber = 0)
    {
        if (collection == null)
        {
            HandleAssertionFailure(
                $"Collection {paramName ?? "parameter"} is null",
                paramName,
                callerMemberName,
                callerFilePath,
                callerLineNumber,
                exception => new ArgumentNullException(paramName, exception));
            return;
        }

        // Check if empty - use specialized checks for common collection types
        bool isEmpty = true;

        if (collection is ICollection<T> countable)
        {
            isEmpty = countable.Count == 0;
        }
        else if (collection is IReadOnlyCollection<T> readOnlyCountable)
        {
            isEmpty = readOnlyCountable.Count == 0;
        }
        else
        {
            // Fall back to enumeration for other collection types
            using var enumerator = collection.GetEnumerator();
            isEmpty = !enumerator.MoveNext();
        }

        if (isEmpty)
        {
            HandleAssertionFailure(
                $"Collection {paramName ?? "parameter"} is empty",
                paramName,
                callerMemberName,
                callerFilePath,
                callerLineNumber,
                exception => new ArgumentException(exception, paramName));
        }
    }

    /// <summary>
    /// Asserts that a value is of the specified type.
    /// </summary>
    /// <param name="value">The value to check.</param>
    /// <param name="expectedType">The expected type.</param>
    /// <param name="paramName">Name of the parameter being checked.</param>
    /// <param name="callerMemberName">Auto-populated with the calling method name.</param>
    /// <param name="callerFilePath">Auto-populated with the source file path.</param>
    /// <param name="callerLineNumber">Auto-populated with the source line Number.</param>
    [Conditional("DEBUG")]
    public static void AssertType(
        object value,
        Type expectedType,
        string paramName = null,
        [CallerMemberName] string callerMemberName = "",
        [CallerFilePath] string callerFilePath = "",
        [CallerLineNumber] int callerLineNumber = 0)
    {
        AssertNotNull(value, paramName, callerMemberName, callerFilePath, callerLineNumber);
        AssertNotNull(expectedType, "expectedType", callerMemberName, callerFilePath, callerLineNumber);

        if (!expectedType.IsInstanceOfType(value))
        {
            HandleAssertionFailure(
                $"Value {paramName ?? "parameter"} is of type {value.GetType().FullName} but {expectedType.FullName} was expected",
                paramName,
                callerMemberName,
                callerFilePath,
                callerLineNumber,
                exception => new ArgumentException(exception, paramName));
        }
    }

    /// <summary>
    /// Asserts that a value is of the specified type.
    /// </summary>
    /// <typeparam name="T">The expected type.</typeparam>
    /// <param name="value">The value to check.</param>
    /// <param name="paramName">Name of the parameter being checked.</param>
    /// <param name="callerMemberName">Auto-populated with the calling method name.</param>
    /// <param name="callerFilePath">Auto-populated with the source file path.</param>
    /// <param name="callerLineNumber">Auto-populated with the source line Number.</param>
    [Conditional("DEBUG")]
    public static void AssertType<T>(
        object value,
        string paramName = null,
        [CallerMemberName] string callerMemberName = "",
        [CallerFilePath] string callerFilePath = "",
        [CallerLineNumber] int callerLineNumber = 0)
    {
        AssertType(value, typeof(T), paramName, callerMemberName, callerFilePath, callerLineNumber);
    }

    #endregion

    #region Conditional Compilation Method Variants

    /// <summary>
    /// Verifies that a condition is true in both debug and release builds.
    /// </summary>
    /// <param name="condition">The condition to verify.</param>
    /// <param name="message">Optional message to include in the exception.</param>
    /// <param name="callerMemberName">Auto-populated with the calling method name.</param>
    /// <param name="callerFilePath">Auto-populated with the source file path.</param>
    /// <param name="callerLineNumber">Auto-populated with the source line Number.</param>
    /// <exception cref="InvalidOperationException">Thrown when the condition is false.</exception>
    public static void AlwaysAssert(
        [DoesNotReturnIf(false)] bool condition,
        string message = null,
        [CallerMemberName] string callerMemberName = "",
        [CallerFilePath] string callerFilePath = "",
        [CallerLineNumber] int callerLineNumber = 0)
    {
        if (!condition)
        {
            HandleAssertionFailure(
                message ?? "Assertion failed",
                null,
                callerMemberName,
                callerFilePath,
                callerLineNumber,
                exception => new InvalidOperationException(exception));
        }
    }

    #endregion

    #region Helper Methods

    /// <summary>
    /// Handles an assertion failure by formatting the message, breaking into the debugger if configured,
    /// logging the failure, and potentially throwing an exception.
    /// </summary>
    /// <param name="message">The error message.</param>
    /// <param name="paramName">The parameter name related to the failure, if applicable.</param>
    /// <param name="callerMemberName">The method where the failure occurred.</param>
    /// <param name="callerFilePath">The file where the failure occurred.</param>
    /// <param name="callerLineNumber">The line Number where the failure occurred.</param>
    /// <param name="exceptionFactory">A function to create the appropriate exception type.</param>
    [DoesNotReturn]
    private static void HandleAssertionFailure(
        string message,
        string paramName,
        string callerMemberName,
        string callerFilePath,
        int callerLineNumber,
        Func<string, Exception> exceptionFactory)
    {
        string detailedMessage = IncludeDetailedMessages
            ? FormatAssertMessage(message, callerMemberName, callerFilePath, callerLineNumber)
            : message;

        // Create the assertion failure info
        var failureInfo = new AssertionFailureInfo(
            message,
            paramName,
            callerMemberName,
            callerFilePath,
            callerLineNumber,
            CaptureStackTrace ? new StackTrace(1, true) : null,
            DateTime.UtcNow
        );

        // Track the failure
        TrackFailure(failureInfo);

        // Invoke custom handler if set
        GlobalAssertionHandler?.Invoke(failureInfo);

        // Log to trace if enabled
        if (LogAssertionsToTrace)
        {
            System.Diagnostics.Trace.WriteLine(detailedMessage, "AssertionSentry Assertion Failure");
        }

        // Break into debugger if configured and attached
        if (BreakOnAssertFailure && Debugger.IsAttached)
        {
            Debugger.Break();
        }

        // Throw exception if configured
        if (ThrowExceptions)
        {
            throw exceptionFactory(detailedMessage);
        }

        // If we didn't throw, we still need to prevent normal execution
        // by exiting the method (this should technically never be reached,
        // as the DoesNotReturn attribute indicates)
        Environment.FailFast($"Critical assertion failure: {detailedMessage}");
    }

    /// <summary>
    /// Formats an assertion message with caller information.
    /// </summary>
    /// <param name="message">The base message.</param>
    /// <param name="callerMemberName">The method name.</param>
    /// <param name="callerFilePath">The file path.</param>
    /// <param name="callerLineNumber">The line Number.</param>
    /// <returns>A formatted message with caller information.</returns>
    private static string FormatAssertMessage(
        string message,
        string callerMemberName,
        string callerFilePath,
        int callerLineNumber)
    {
        // Use a thread-local StringBuilder to avoid allocations
        _messageBuilder ??= new System.Text.StringBuilder(256);
        _messageBuilder.Clear();

        _messageBuilder.Append(message ?? "AssertionSentry assertion failed");
        _messageBuilder.Append(" at ");
        _messageBuilder.Append(callerMemberName);
        _messageBuilder.Append(" in ");

        // Get just the filename, not the full path
        string fileName = callerFilePath;
        int lastSeparatorIndex = Math.Max(
            callerFilePath.LastIndexOf('\\'),
            callerFilePath.LastIndexOf('/'));
        if (lastSeparatorIndex >= 0 && lastSeparatorIndex < callerFilePath.Length - 1)
        {
            fileName = callerFilePath[(lastSeparatorIndex + 1)..];
        }

        _messageBuilder.Append(fileName);
        _messageBuilder.Append(":line ");
        _messageBuilder.Append(callerLineNumber);

        return _messageBuilder.ToString();
    }

    /// <summary>
    /// Tracks an assertion failure in the recent failures queue.
    /// </summary>
    /// <param name="failureInfo">Information about the failure.</param>
    private static void TrackFailure(AssertionFailureInfo failureInfo)
    {
        if (MaxTrackedAssertions <= 0)
            return;

        lock (_lock)
        {
            _recentFailures.Enqueue(failureInfo);

            // Ensure we don't exceed the maximum tracked assertions
            while (_recentFailures.Count > MaxTrackedAssertions)
            {
                _recentFailures.Dequeue();
            }
        }
    }

    /// <summary>
    /// Gets a copy of recent assertion failures.
    /// </summary>
    /// <returns>An array of recent assertion failures.</returns>
    public static AssertionFailureInfo[] GetRecentFailures()
    {
        lock (_lock)
        {
            return [.. _recentFailures];
        }
    }

    /// <summary>
    /// Clears the record of recent assertion failures.
    /// </summary>
    public static void ClearFailureHistory()
    {
        lock (_lock)
        {
            _recentFailures.Clear();
        }
    }

    #endregion
}
