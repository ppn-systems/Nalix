using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace Notio.Debugger;

/// <summary>
/// Enhanced debug assertion class with extended functionality
/// </summary>
public static class Debug
{
    #region Configuration Properties

    /// <summary>
    /// Gets or sets whether detailed exception information should be included in assertions
    /// </summary>
    public static bool IncludeDetailedMessages { get; set; } = true;

    /// <summary>
    /// Gets or sets whether assertions should break into the debugger
    /// </summary>
    public static bool BreakOnAssertFailure { get; set; } = true;

    #endregion Configuration Properties

    #region Assert Methods

    /// <summary>
    /// Verifies that a condition is true. If the condition is false, throws an exception.
    /// </summary>
    /// <param name="condition">The condition to verify</param>
    /// <param name="message">Optional message to include in the exception</param>
    /// <param name="callerMemberName">Auto-populated with the calling method name</param>
    /// <param name="callerFilePath">Auto-populated with the source file path</param>
    /// <param name="callerLineNumber">Auto-populated with the source line number</param>
    [Conditional("DEBUG")]
    public static void Assert(bool condition, string message = null,
        [CallerMemberName] string callerMemberName = "",
        [CallerFilePath] string callerFilePath = "",
        [CallerLineNumber] int callerLineNumber = 0)
    {
        if (!condition)
        {
            string detailedMessage = IncludeDetailedMessages
                ? FormatAssertMessage(message, callerMemberName, callerFilePath, callerLineNumber)
                : message ?? "Debug assertion failed";

            if (BreakOnAssertFailure && System.Diagnostics.Debugger.IsAttached)
            {
                System.Diagnostics.Debugger.Break();
            }

            throw new InvalidOperationException(detailedMessage);
        }
    }

    /// <summary>
    /// Verifies that an object is not null. If the object is null, throws an exception.
    /// </summary>
    /// <param name="obj">The object to check for null</param>
    /// <param name="objectName">Name of the object being checked</param>
    /// <param name="callerMemberName">Auto-populated with the calling method name</param>
    /// <param name="callerFilePath">Auto-populated with the source file path</param>
    /// <param name="callerLineNumber">Auto-populated with the source line number</param>
    [Conditional("DEBUG")]
    public static void AssertNotNull(object obj, string objectName = null,
        [CallerMemberName] string callerMemberName = "",
        [CallerFilePath] string callerFilePath = "",
        [CallerLineNumber] int callerLineNumber = 0)
    {
        if (obj == null)
        {
            string message = $"Object {objectName ?? "reference"} is null";
            string detailedMessage = IncludeDetailedMessages
                ? FormatAssertMessage(message, callerMemberName, callerFilePath, callerLineNumber)
                : message;

            if (BreakOnAssertFailure && System.Diagnostics.Debugger.IsAttached)
            {
                System.Diagnostics.Debugger.Break();
            }

            throw new ArgumentNullException(objectName, detailedMessage);
        }
    }

    /// <summary>
    /// Verifies that a string is not null or empty. If the string is null or empty, throws an exception.
    /// </summary>
    /// <param name="value">The string to check</param>
    /// <param name="paramName">Name of the parameter being checked</param>
    /// <param name="callerMemberName">Auto-populated with the calling method name</param>
    /// <param name="callerFilePath">Auto-populated with the source file path</param>
    /// <param name="callerLineNumber">Auto-populated with the source line number</param>
    [Conditional("DEBUG")]
    public static void AssertNotNullOrEmpty(string value, string paramName = null,
        [CallerMemberName] string callerMemberName = "",
        [CallerFilePath] string callerFilePath = "",
        [CallerLineNumber] int callerLineNumber = 0)
    {
        if (string.IsNullOrEmpty(value))
        {
            string message = $"String {paramName ?? "parameter"} is null or empty";
            string detailedMessage = IncludeDetailedMessages
                ? FormatAssertMessage(message, callerMemberName, callerFilePath, callerLineNumber)
                : message;

            if (BreakOnAssertFailure && System.Diagnostics.Debugger.IsAttached)
            {
                System.Diagnostics.Debugger.Break();
            }

            throw new ArgumentException(detailedMessage, paramName);
        }
    }

    /// <summary>
    /// Verifies that a string is not null, empty or whitespace. If the string is null, empty or whitespace, throws an exception.
    /// </summary>
    /// <param name="value">The string to check</param>
    /// <param name="paramName">Name of the parameter being checked</param>
    /// <param name="callerMemberName">Auto-populated with the calling method name</param>
    /// <param name="callerFilePath">Auto-populated with the source file path</param>
    /// <param name="callerLineNumber">Auto-populated with the source line number</param>
    [Conditional("DEBUG")]
    public static void AssertNotNullOrWhiteSpace(string value, string paramName = null,
        [CallerMemberName] string callerMemberName = "",
        [CallerFilePath] string callerFilePath = "",
        [CallerLineNumber] int callerLineNumber = 0)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            string message = $"String {paramName ?? "parameter"} is null, empty or whitespace";
            string detailedMessage = IncludeDetailedMessages
                ? FormatAssertMessage(message, callerMemberName, callerFilePath, callerLineNumber)
                : message;

            if (BreakOnAssertFailure && System.Diagnostics.Debugger.IsAttached)
            {
                System.Diagnostics.Debugger.Break();
            }

            throw new ArgumentException(detailedMessage, paramName);
        }
    }

    /// <summary>
    /// Verifies that a value is within the specified range (inclusive).
    /// </summary>
    /// <typeparam name="T">The type of the value to check</typeparam>
    /// <param name="value">The value to check</param>
    /// <param name="min">The minimum acceptable value</param>
    /// <param name="max">The maximum acceptable value</param>
    /// <param name="paramName">Name of the parameter being checked</param>
    /// <param name="callerMemberName">Auto-populated with the calling method name</param>
    /// <param name="callerFilePath">Auto-populated with the source file path</param>
    /// <param name="callerLineNumber">Auto-populated with the source line number</param>
    [Conditional("DEBUG")]
    public static void AssertInRange<T>(T value, T min, T max, string paramName = null,
        [CallerMemberName] string callerMemberName = "",
        [CallerFilePath] string callerFilePath = "",
        [CallerLineNumber] int callerLineNumber = 0) where T : IComparable<T>
    {
        if (value.CompareTo(min) < 0 || value.CompareTo(max) > 0)
        {
            string message = $"Value {paramName ?? "parameter"} ({value}) is out of range [{min}-{max}]";
            string detailedMessage = IncludeDetailedMessages
                ? FormatAssertMessage(message, callerMemberName, callerFilePath, callerLineNumber)
                : message;

            if (BreakOnAssertFailure && System.Diagnostics.Debugger.IsAttached)
            {
                System.Diagnostics.Debugger.Break();
            }

            throw new ArgumentOutOfRangeException(paramName, detailedMessage);
        }
    }

    /// <summary>
    /// Fails unconditionally with the specified message.
    /// </summary>
    /// <param name="message">The message for the failure</param>
    /// <param name="callerMemberName">Auto-populated with the calling method name</param>
    /// <param name="callerFilePath">Auto-populated with the source file path</param>
    /// <param name="callerLineNumber">Auto-populated with the source line number</param>
    [Conditional("DEBUG")]
    public static void Fail(string message = "Debug failure",
        [CallerMemberName] string callerMemberName = "",
        [CallerFilePath] string callerFilePath = "",
        [CallerLineNumber] int callerLineNumber = 0)
    {
        string detailedMessage = IncludeDetailedMessages
            ? FormatAssertMessage(message, callerMemberName, callerFilePath, callerLineNumber)
            : message;

        if (BreakOnAssertFailure && System.Diagnostics.Debugger.IsAttached)
        {
            System.Diagnostics.Debugger.Break();
        }

        throw new InvalidOperationException(detailedMessage);
    }

    #endregion Assert Methods

    #region Helper Methods

    /// <summary>
    /// Formats an assertion message with caller information
    /// </summary>
    private static string FormatAssertMessage(string message, string callerMemberName, string callerFilePath, int callerLineNumber)
    {
        return $"{message ?? "Debug assertion failed"} at {callerMemberName} in {System.IO.Path.GetFileName(callerFilePath)}:line {callerLineNumber}";
    }

    #endregion Helper Methods
}
