// Copyright (c) 2025 PPN Corporation. All rights reserved.

namespace Nalix.Common.Exceptions;

/// <summary>
/// Serves as the base type for all custom exceptions in the Nalix system.
/// </summary>
/// <remarks>
/// Inherit from this class when creating domain-specific or module-specific exceptions.
/// It extends <see cref="System.Exception"/> and provides a consistent base for
/// exception handling, logging, and categorization throughout the application.
/// </remarks>
/// <example>
/// <code>
/// public sealed class PacketParseException : BaseException
/// {
///     public PacketParseException(string message)
///         : base(message) { }
///
///     public PacketParseException(string message, Exception innerException)
///         : base(message, innerException) { }
/// }
/// </code>
/// </example>
public abstract class BaseException : System.Exception
{
    /// <summary>
    /// Initializes a new instance of the <see cref="BaseException"/> class.
    /// </summary>
    protected BaseException()
    { }

    /// <summary>
    /// Initializes a new instance of the <see cref="BaseException"/> class
    /// with a specified error message.
    /// </summary>
    /// <param name="message">
    /// The error message that explains the reason for the exception.
    /// </param>
    protected BaseException(System.String message)
        : base(message) { }

    /// <summary>
    /// Initializes a new instance of the <see cref="BaseException"/> class
    /// with a specified error message and a reference to the inner exception
    /// that is the cause of this exception.
    /// </summary>
    /// <param name="message">
    /// The error message that explains the reason for the exception.
    /// </param>
    /// <param name="innerException">
    /// The exception that caused the current exception.
    /// </param>
    protected BaseException(System.String message, System.Exception innerException)
        : base(message, innerException) { }
}
