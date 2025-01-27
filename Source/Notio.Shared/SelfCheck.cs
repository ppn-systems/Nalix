using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System;
using Notio.Common.Exceptions;

namespace Notio.Shared;

/// <summary>
/// Provides helper methods for internal checks and exception generation
/// with detailed context information for debugging and logging.
/// </summary>
public static class SelfCheck
{
    /// <summary>
    /// Generates an <see cref="InternalErrorException"/> with a detailed message
    /// that includes the file path and line number where the exception occurred.
    /// </summary>
    /// <param name="message">The custom error message to be included in the exception.</param>
    /// <param name="filePath">
    /// The file path of the source code that invoked this method.
    /// This is automatically populated by the compiler using <see cref="CallerFilePathAttribute"/>.
    /// </param>
    /// <param name="lineNumber">
    /// The line number in the source file that invoked this method.
    /// This is automatically populated by the compiler using <see cref="CallerLineNumberAttribute"/>.
    /// </param>
    /// <returns>An <see cref="InternalErrorException"/> with a detailed error message.</returns>
    public static InternalErrorException Failure(string message,
        [CallerFilePath] string filePath = "",
        [CallerLineNumber] int lineNumber = 0)
        => new(BuildMessage(message, filePath, lineNumber));

    /// <summary>
    /// Builds a detailed error message that includes the error message,
    /// file name, line number, and assembly information.
    /// </summary>
    /// <param name="message">The custom error message to be included.</param>
    /// <param name="filePath">The file path where the error occurred.</param>
    /// <param name="lineNumber">The line number where the error occurred.</param>
    /// <returns>A formatted string containing detailed error information.</returns>
    private static string BuildMessage(string message, string filePath, int lineNumber)
    {
        // Get the current stack trace frames
        StackFrame[] frames = new StackTrace().GetFrames();
        if (frames == null)
            return message;

        try
        {
            // Extract the file name from the full file path
            filePath = Path.GetFileName(filePath);
        }
        catch (ArgumentException)
        {
        }

        // Find the first stack frame outside of the SelfCheck class
        StackFrame? stackFrame = frames.FirstOrDefault((StackFrame f) => f.GetMethod()?.ReflectedType != typeof(SelfCheck));

        // Build the message string
        StringBuilder stringBuilder = new StringBuilder().Append('[')
                                                         .Append(stackFrame?.GetType().Assembly.GetName().Name ?? "<unknown>");

        if (!string.IsNullOrEmpty(filePath))
        {
            stringBuilder.Append(": ").Append(filePath);
            if (lineNumber > 0)
                stringBuilder.Append('(').Append(lineNumber).Append(')');
        }

        return stringBuilder.Append("] ").Append(message).ToString();
    }
}