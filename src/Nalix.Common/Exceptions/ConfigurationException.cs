using System.Runtime.Serialization;

namespace Nalix.Common.Exceptions;

/// <summary>
/// Represents errors that occur during the configuration process in the Notio real-time server.
/// </summary>
[System.Serializable]
public class ConfigurationException : BaseException
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ConfigurationException"/> class.
    /// </summary>
    public ConfigurationException()
        : base("A configuration error occurred.")
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="ConfigurationException"/> class with a specified error message.
    /// </summary>
    /// <param name="message">The error message that describes the exception.</param>
    public ConfigurationException(System.String message)
        : base(message)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="ConfigurationException"/> class with a specified error
    /// message and a reference to the inner exception that is the cause of this exception.
    /// </summary>
    /// <param name="message">The error message that describes the exception.</param>
    /// <param name="innerException">The exception that is the cause of the current exception.</param>
    public ConfigurationException(System.String message, System.Exception innerException)
        : base(message, innerException)
    {
    }

    /// <summary>
    /// Sets the <see cref="SerializationInfo"/> with information about the exception.
    /// </summary>
    /// <param name="info">The <see cref="SerializationInfo"/> that holds the serialized object data.</param>
    /// <param name="context">The <see cref="StreamingContext"/> that contains contextual information about the source or destination.</param>
    [System.Obsolete("This method is obsolete and will be removed in future versions.")]
    public override void GetObjectData(SerializationInfo info, StreamingContext context)
    {
        System.ArgumentNullException.ThrowIfNull(info);

        base.GetObjectData(info, context);
    }
}
