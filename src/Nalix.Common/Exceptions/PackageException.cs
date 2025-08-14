// Copyright (c) 2025 PPN Corporation. All rights reserved.

namespace Nalix.Common.Exceptions;

/// <summary>
/// Represents errors that occur during packet processing operations.
/// </summary>
/// <remarks>
/// This exception is typically thrown when a packet fails validation, 
/// serialization, deserialization, or other protocol-related operations.
/// </remarks>
[System.Serializable]
public class PackageException : BaseException
{
    /// <summary>
    /// Initializes a new instance of the <see cref="PackageException"/> class 
    /// with a specified error message.
    /// </summary>
    /// <param name="message">
    /// The message that describes the error.
    /// </param>
    public PackageException(System.String message)
        : base(message)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="PackageException"/> class 
    /// with a specified error message and a reference to the inner exception 
    /// that is the cause of this exception.
    /// </summary>
    /// <param name="message">
    /// The error message that explains the reason for the exception.
    /// </param>
    /// <param name="innerException">
    /// The exception that is the cause of the current exception.
    /// </param>
    public PackageException(System.String message, System.Exception innerException)
        : base(message, innerException)
    {
    }
}
