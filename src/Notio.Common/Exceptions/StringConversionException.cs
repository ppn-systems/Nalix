namespace Notio.Common.Exceptions;

/// <summary>
/// The exception that is thrown when a conversion from a string to a
/// specified type fails.
/// </summary>
[System.Serializable]
public class StringConversionException : System.Exception
{
    /// <summary>
    /// Initializes a new instance of the <see cref="StringConversionException"/> class.
    /// </summary>
    public StringConversionException()
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="StringConversionException"/> class.
    /// </summary>
    /// <param name="message">The error message that explains the reason for the exception.</param>
    public StringConversionException(string message)
        : base(message)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="StringConversionException"/> class.
    /// </summary>
    /// <param name="message">The error message that explains the reason for the exception.</param>
    /// <param name="innerException">The exception that is the cause of the current exception,
    /// or <see langword="null" /> if no inner exception is specified.</param>
    public StringConversionException(string message, System.Exception innerException)
        : base(message, innerException)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="StringConversionException"/> class.
    /// </summary>
    /// <param name="type">The desired resulting type of the attempted conversion.</param>
    public StringConversionException(System.Type type)
        : base(BuildStandardMessageForType(type))
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="StringConversionException"/> class.
    /// </summary>
    /// <param name="type">The desired resulting type of the attempted conversion.</param>
    /// <param name="innerException">The exception that is the cause of the current exception,
    /// or <see langword="null" /> if no inner exception is specified.</param>
    public StringConversionException(System.Type type, System.Exception innerException)
        : base(BuildStandardMessageForType(type), innerException)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="StringConversionException"/> class.
    /// </summary>
    /// <param name="info">The <see cref="System.Runtime.Serialization.SerializationInfo" /> that holds the serialized object data
    /// about the exception being thrown.</param>
    /// <param name="context">The <see cref="System.Runtime.Serialization.StreamingContext" /> that contains contextual information
    /// about the source or destination.</param>
    [System.Obsolete("This API supports obsolete formatter-based serialization and should not be used.", DiagnosticId = "SYSLIB0051")]
    protected StringConversionException(
        System.Runtime.Serialization.SerializationInfo info,
        System.Runtime.Serialization.StreamingContext context)
        : base(info, context)
    {
    }

    private static string BuildStandardMessageForType(System.Type type)
        => $"Cannot convert a string to an instance of {type.FullName}";
}
