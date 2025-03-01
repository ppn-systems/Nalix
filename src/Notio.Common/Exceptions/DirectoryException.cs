using System;

namespace Notio.Common.Exceptions;

/// <summary>
/// Exception thrown when a directory operation fails.
/// </summary>
/// <remarks>
/// Initializes a new instance of the <see cref="DirectoryException"/> class.
/// </remarks>
/// <param name="message">The error message.</param>
/// <param name="directoryPath">The directory path that caused the exception.</param>
/// <param name="innerException">The inner exception, if any.</param>
public class DirectoryException(string message, string directoryPath, Exception innerException = null)
    : Exception(message, innerException)
{
    /// <summary>
    /// Gets the path that caused the exception.
    /// </summary>
    public string DirectoryPath { get; } = directoryPath;
}
