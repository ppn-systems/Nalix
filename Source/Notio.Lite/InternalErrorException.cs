using System;
using System.Runtime.Serialization;

/*
 * NOTE TO CONTRIBUTORS:
 *
 * Never use this exception directly.
 * Use the methods in the SelfCheck class instead.
 */

namespace Notio.Lite;

/// <summary>
/// <para>The exception that is thrown by methods of the <see cref="SelfCheck"/> class
/// to signal a condition most probably caused by an internal error in a library
/// or application.</para>
/// <para>Do not use this class directly; use the methods of the <see cref="SelfCheck"/> class instead.</para>
/// </summary>
[Serializable]
public sealed class InternalErrorException : Exception
{
    /// <summary>
    /// <para>Initializes a new instance of the <see cref="InternalErrorException"/> class.</para>
    /// <para>Do not call this constrcutor directly; use the methods of the <see cref="SelfCheck"/> class instead.</para>
    /// </summary>
    /// <param name="message">The message that describes the error.</param>

    internal InternalErrorException(string message)
        : base(message)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="InternalErrorException"/> class.
    /// </summary>
    /// <param name="info">The <see cref="SerializationInfo"></see> that holds the serialized object data about the exception being thrown.</param>
    /// <param name="context">The <see cref="StreamingContext"></see> that contains contextual information about the source or destination.</param>
    [Obsolete("This API supports obsolete formatter-based serialization and should not be used.", DiagnosticId = "SYSLIB0051")]
    private InternalErrorException(SerializationInfo info, StreamingContext context)
        : base(info, context)
    {
    }
}