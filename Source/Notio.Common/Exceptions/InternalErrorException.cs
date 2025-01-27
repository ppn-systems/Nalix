using System;

namespace Notio.Common.Exceptions;

[Serializable]
public sealed class InternalErrorException : Exception
{
    public InternalErrorException(string message)
        : base(message)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="InternalErrorException"/> class with a specified error message and the parameter name that caused the error.
    /// </summary>
    /// <param name="message">The error message that explains the reason for the exception.</param>
    /// <param name="paramName">The name of the parameter that caused the exception.</param>
    public InternalErrorException(string message, string paramName) : base($"{message} Parameter: {paramName}") { }

    /// <summary>
    /// Initializes a new instance of the <see cref="InternalErrorException"/> class with a specified error message and a reference to the inner exception that is the cause of this exception.
    /// </summary>
    /// <param name="message">The error message that explains the reason for the exception.</param>
    /// <param name="innerException">The exception that is the cause of the current exception.</param>
    public InternalErrorException(string message, Exception innerException) : base(message, innerException) { }
}