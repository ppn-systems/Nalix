namespace Nalix.Common.Exceptions;

/// <summary>
/// The exception that is thrown when a conversion from a string to a
/// specified type fails.
/// </summary>
[System.Serializable]
public class StringConversionException : BaseException
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
    public StringConversionException(System.String message)
        : base(message)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="StringConversionException"/> class.
    /// </summary>
    /// <param name="message">The error message that explains the reason for the exception.</param>
    /// <param name="innerException">The exception that is the cause of the current exception,
    /// or <see langword="null" /> if no inner exception is specified.</param>
    public StringConversionException(System.String message, System.Exception innerException)
        : base(message, innerException)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="StringConversionException"/> class.
    /// </summary>
    /// <param name="type">The desired resulting type of the attempted conversion.</param>
    public StringConversionException(System.Type type)
        : base($"Cannot convert a string to an instance of {type.FullName}")
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="StringConversionException"/> class.
    /// </summary>
    /// <param name="type">The desired resulting type of the attempted conversion.</param>
    /// <param name="innerException">The exception that is the cause of the current exception,
    /// or <see langword="null" /> if no inner exception is specified.</param>
    public StringConversionException(System.Type type, System.Exception innerException)
        : base($"Cannot convert a string to an instance of {type.FullName}", innerException)
    {
    }
}
