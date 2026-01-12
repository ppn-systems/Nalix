// Copyright (c) 2025 PPN Corporation. All rights reserved.

namespace Nalix.Common.Core.Exceptions;

/// <summary>
/// The exception that is thrown by Notio's internal diagnostic checks to signal a condition
/// most probably caused by an error in Notio.
/// </summary>
/// <remarks>
/// <para>This API supports the Notio infrastructure and is not intended to be used directly from your code.</para>
/// <para>It is typically raised when a self-check or internal validation detects an invalid
/// state, configuration, or logic error within the system.</para>
/// </remarks>
[System.Serializable]
public class InternalErrorException : BaseException
{
    /// <summary>
    /// Gets additional diagnostic information related to this exception.
    /// </summary>
    /// <remarks>
    /// This property may contain debugging details such as a stack trace,
    /// failure context, or other relevant internal state.
    /// </remarks>
    public System.String Details { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="InternalErrorException"/> class.
    /// </summary>
    /// <remarks>
    /// <para>This API supports the Notio infrastructure and is not intended to be used directly from your code.</para>
    /// </remarks>
    public InternalErrorException()
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="InternalErrorException"/> class with a specified error message.
    /// </summary>
    /// <param name="message">
    /// The error message that describes the reason for the exception.
    /// </param>
    public InternalErrorException(System.String message)
        : base(message)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="InternalErrorException"/> class with a specified error
    /// message and a reference to the inner exception that is the cause of this exception.
    /// </summary>
    /// <param name="message">
    /// The error message that explains the reason for the exception.
    /// </param>
    /// <param name="innerException">
    /// The exception that caused the current exception, or <see langword="null"/> if none is specified.
    /// </param>
    public InternalErrorException(System.String message, System.Exception innerException)
        : base(message, innerException)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="InternalErrorException"/> class with a specified
    /// error message and additional diagnostic details.
    /// </summary>
    /// <param name="message">
    /// The error message that explains the reason for the exception.
    /// </param>
    /// <param name="details">
    /// Additional information about the error, which can be used for internal diagnostics.
    /// </param>
    public InternalErrorException(System.String message, System.String details)
        : base(message) => Details = details;
}
