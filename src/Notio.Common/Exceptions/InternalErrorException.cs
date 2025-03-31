/*
 * NOTE TO CONTRIBUTORS:
 *
 * Never use this exception directly.
 * Use the methods in Notio.Common.SelfCheck instead.
 */

namespace Notio.Common.Exceptions;

/// <summary>
/// <para>The exception that is thrown by Notio's internal diagnostic checks to signal a condition
/// most probably caused by an error in Notio.</para>
/// <para>This API supports the Notio infrastructure and is not intended to be used directly from your code.</para>
/// </summary>
[System.Serializable]
public class InternalErrorException : BaseException
{
    /// <summary>
    /// Gets the detailed information related to this log entry.
    /// </summary>
    /// <remarks>
    /// This property typically contains additional context about the log event, such as
    /// stack traces, exception messages, or other relevant debugging information.
    /// </remarks>
    public string Details { get; }

    /// <summary>
    /// <para>Initializes a new instance of the <see cref="InternalErrorException"/> class.</para>
    /// <para>This API supports the Notio infrastructure and is not intended to be used directly from your code.</para>
    /// </summary>
    public InternalErrorException()
    {
    }

    /// <summary>
    /// <para>Initializes a new instance of the <see cref="InternalErrorException"/> class.</para>
    /// </summary>
    /// <param name="message">The message that describes the error.</param>
    public InternalErrorException(string message)
        : base(message)
    {
    }

    /// <summary>
    /// <para>Initializes a new instance of the <see cref="InternalErrorException"/> class.</para>
    /// <para>This API supports the Notio infrastructure and is not intended to be used directly from your code.</para>
    /// </summary>
    /// <param name="message">The error message that explains the reason for the exception.</param>
    /// <param name="innerException">The exception that is the cause of the current exception,
    /// or <see langword="null"/> if no inner exception is specified.</param>
    public InternalErrorException(string message, System.Exception innerException)
        : base(message, innerException)
    {
    }

    /// <summary>
    /// <para>Initializes a new instance of the <see cref="InternalErrorException"/> class.</para>
    /// <para>This API supports the Notio infrastructure and is not intended to be used directly from your code.</para>
    /// </summary>
    /// <param name="message">The error message that explains the reason for the exception.</param>
    /// <param name="details">The exception that is the cause of the current exception,
    /// or <see langword="null"/> if no inner exception is specified.</param>
    public InternalErrorException(string message, string details)
        : base(message) => Details = details;
}
