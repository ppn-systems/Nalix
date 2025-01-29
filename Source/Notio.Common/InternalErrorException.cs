using System;
using System.Runtime.Serialization;

/*
 * NOTE TO CONTRIBUTORS:
 *
 * Never use this exception directly.
 * Use the methods in Notio.Common.SelfCheck instead.
 */

namespace Notio.Common;

/// <summary>
/// <para>The exception that is thrown by Notio's internal diagnostic checks to signal a condition
/// most probably caused by an error in Notio.</para>
/// <para>This API supports the Notio infrastructure and is not intended to be used directly from your code.</para>
/// </summary>
[Serializable]
public class InternalErrorException : Exception
{
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
    /// <para>Do not call this constrcutor directly; use the methods of the <see cref="SelfCheck"/> class instead.</para>
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
    public InternalErrorException(string message, Exception innerException)
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
        : base(message)
    {
        Details = details;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="InternalErrorException"/> class.
    /// <para>This API supports the Notio infrastructure and is not intended to be used directly from your code.</para>
    /// </summary>
    /// <param name="info">The <see cref="SerializationInfo"></see> that holds the serialized object data about the exception being thrown.</param>
    /// <param name="context">The <see cref="StreamingContext"></see> that contains contextual information about the source or destination.</param>
    [Obsolete("This API supports obsolete formatter-based serialization and should not be used.", DiagnosticId = "SYSLIB0051")]
    protected InternalErrorException(SerializationInfo info, StreamingContext context)
        : base(info, context)
    {
    }
}