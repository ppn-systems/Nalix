namespace Nalix.Common.Exceptions;

/// <summary>
/// Base exception class for all custom exceptions in the system.
/// </summary>
public abstract class BaseException : System.Exception
{
    /// <summary>
    /// Initializes a new instance of the <see cref="BaseException"/> class.
    /// </summary>
    protected BaseException()
    { }

    /// <summary>
    /// Initializes a new instance of the <see cref="BaseException"/> class.
    /// </summary>
    /// <param name="message">The error message that explains the reason for the exception.</param>
    protected BaseException(System.String message)
        : base(message) { }

    /// <summary>
    /// Initializes a new instance of the <see cref="BaseException"/> class with a specified
    /// error message and a reference to the inner exception that is the cause of this exception.
    /// </summary>
    /// <param name="message">The error message that explains the reason for the exception.</param>
    /// <param name="innerException">The exception that is the cause of the current exception.</param>
    protected BaseException(System.String message, System.Exception innerException)
        : base(message, innerException) { }
}
