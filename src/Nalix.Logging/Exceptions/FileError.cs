// Copyright (c) 2025 PPN Corporation. All rights reserved.

namespace Nalix.Logging.Exceptions;

/// <summary>
/// Represents an error that occurred during file logging operations.
/// Contains context information and recovery options.
/// </summary>
/// <remarks>
/// Creates a new file error instance with detailed context.
/// </remarks>
/// <param name="ex">The exception that caused the error.</param>
/// <param name="filePath">The file path where the error occurred.</param>
[System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverage]
[System.Diagnostics.DebuggerDisplay("{Exception.Message,nq} ({OriginalFilePath})")]
public sealed class FileError(System.Exception ex, System.String filePath)
{
    /// <summary>
    /// Gets or sets the new log file name to use when recovering from errors.
    /// </summary>
    [System.Diagnostics.CodeAnalysis.NotNull]
    public System.String NewLogFileName { get; set; } = System.String.Empty;

    /// <summary>
    /// Gets the original log file path where the error occurred.
    /// </summary>
    [System.Diagnostics.CodeAnalysis.NotNull]
    public System.String OriginalFilePath { get; } = filePath ?? System.String.Empty;

    /// <summary>
    /// Gets the exception that caused the file error.
    /// </summary>
    public System.Exception Exception { get; } = ex ?? throw new System.ArgumentNullException(nameof(ex));
}
