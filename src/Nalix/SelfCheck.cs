using Nalix.Common.Exceptions;
using System.Runtime.CompilerServices;

namespace Nalix;

/// <summary>
/// Provides methods to perform self-checks in library or application code.
/// Helps identify and report internal logic errors with detailed context information.
/// </summary>
public static class SelfCheck
{
    // Constants to improve readability and performance
    private const char Colon = ':';
    private const char Space = ' ';
    private const char OpenBracket = '[';
    private const char CloseBracket = ']';
    private const char OpenParenthesis = '(';
    private const char CloseParenthesis = ')';

    // Pre-allocate default strings to reduce allocations
    private const string DefaultFile = "UnknownFile";
    private const string DefaultMethod = "UnknownMethod";

    /// <summary>
    /// Creates and returns an exception indicating that an internal self-check has failed.
    /// The exception includes detailed context about where the failure occurred.
    /// </summary>
    /// <param name="message">The error message describing the failure.</param>
    /// <param name="callerMethod">The method where the failure occurred (automatically populated).</param>
    /// <param name="filePath">The source file path where the failure occurred (automatically populated).</param>
    /// <param name="lineNumber">The line Number where the failure occurred (automatically populated).</param>
    /// <returns>A new <see cref="InternalErrorException"/> with detailed context information.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static InternalErrorException Failure(
        string message,
        [CallerMemberName] string callerMethod = "",
        [CallerFilePath] string filePath = "",
        [CallerLineNumber] int lineNumber = 0)
    {
        // Ensure we have valid values for caller information
        callerMethod = string.IsNullOrEmpty(callerMethod) ? DefaultMethod : callerMethod;

        // Extract just the filename without path for better readability
        string fileName = DefaultFile;
        if (!string.IsNullOrEmpty(filePath))
        {
            try
            {
                fileName = System.IO.Path.GetFileName(filePath);
            }
            catch (System.ArgumentException)
            {
                // If filename extraction fails, use the raw path or default
                fileName = string.IsNullOrEmpty(filePath) ? DefaultFile : filePath;
            }
        }

        // Estimate the required capacity for the StringBuilder to avoid resizing
        int capacity = callerMethod.Length + fileName.Length + message.Length + 25;

        // Build the formatted error message
        System.Text.StringBuilder sb = new(capacity);
        sb.Append(OpenBracket).Append(callerMethod);

        // Include file and line information if available
        if (!string.IsNullOrEmpty(fileName))
        {
            sb.Append(Colon).Append(Space).Append(fileName);

            if (lineNumber > 0)
            {
                sb.Append(OpenParenthesis).Append(lineNumber).Append(CloseParenthesis);
            }
        }

        // Complete the message with the user-provided text
        sb.Append(CloseBracket).Append(Space);

        // Ensure the user message is not null
        if (!string.IsNullOrEmpty(message))
        {
            sb.Append(message);
        }
        else
        {
            sb.Append("Unspecified internal error");
        }

        // Include current time and user for additional context
        sb.AppendLine();
        sb.Append("[UTC: ").Append(System.DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss")).Append(']');

        try
        {
            string username = System.Environment.UserName;
            if (!string.IsNullOrEmpty(username))
            {
                sb.Append(" [User: ").Append(username).Append(']');
            }
        }
        catch
        {
            // Ignore username retrieval errors
        }

        // Create and return the exception
        return new InternalErrorException(sb.ToString());
    }

    /// <summary>
    /// Performs a validation check and throws an exception if the condition is false.
    /// </summary>
    /// <param name="condition">The condition to check. If false, an exception is thrown.</param>
    /// <param name="message">The error message if the check fails.</param>
    /// <param name="callerMethod">The method where this check occurs (automatically populated).</param>
    /// <param name="filePath">The source file path (automatically populated).</param>
    /// <param name="lineNumber">The line Number (automatically populated).</param>
    /// <exception cref="InternalErrorException">Thrown if the condition is false.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Validate(
        bool condition, string message,
        [CallerMemberName] string callerMethod = "",
        [CallerFilePath] string filePath = "",
        [CallerLineNumber] int lineNumber = 0)
    {
        if (!condition)
            throw Failure(message, callerMethod, filePath, lineNumber);
    }

    /// <summary>
    /// Ensures a value is not null, throwing an exception if it is.
    /// </summary>
    /// <param name="value">The value to check.</param>
    /// <param name="paramName">The parameter name to include in the error message.</param>
    /// <param name="callerMethod">The method where this check occurs (automatically populated).</param>
    /// <param name="filePath">The source file path (automatically populated).</param>
    /// <param name="lineNumber">The line Number (automatically populated).</param>
    /// <exception cref="InternalErrorException">Thrown if the value is null.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void NotNull(
        object value, string paramName,
        [CallerMemberName] string callerMethod = "",
        [CallerFilePath] string filePath = "",
        [CallerLineNumber] int lineNumber = 0)
    {
        if (value == null)
            throw Failure($"Parameter '{paramName}' cannot be null", callerMethod, filePath, lineNumber);
    }

    /// <summary>
    /// Ensures a string is not null or empty, throwing an exception if it is.
    /// </summary>
    /// <param name="value">The string to check.</param>
    /// <param name="paramName">The parameter name to include in the error message.</param>
    /// <param name="callerMethod">The method where this check occurs (automatically populated).</param>
    /// <param name="filePath">The source file path (automatically populated).</param>
    /// <param name="lineNumber">The line Number (automatically populated).</param>
    /// <exception cref="InternalErrorException">Thrown if the string is null or empty.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void NotNullOrEmpty(
        string value, string paramName,
        [CallerMemberName] string callerMethod = "",
        [CallerFilePath] string filePath = "",
        [CallerLineNumber] int lineNumber = 0)
    {
        if (string.IsNullOrEmpty(value))
            throw Failure($"Parameter '{paramName}' cannot be null or empty", callerMethod, filePath, lineNumber);
    }
}
