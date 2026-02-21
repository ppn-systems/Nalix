// Copyright (c) 2025 PPN Corporation. All rights reserved.

namespace Nalix.Common.Exceptions;

/// <summary>
/// Represents errors that occur during serialization or deserialization processes.
/// </summary>
/// <remarks>
/// This exception is typically thrown when converting objects to or from a serialized format fails,
/// such as JSON, binary, or custom protocol formats.
/// </remarks>
[System.Serializable]
public class SerializationException : BaseException
{
    /// <summary>
    /// Initializes a new instance of the <see cref="SerializationException"/> class
    /// with a default error message.
    /// </summary>
    public SerializationException()
        : base("Serialization operation failed.")
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="SerializationException"/> class
    /// with a specified error message.
    /// </summary>
    /// <param name="message">
    /// The error message that describes the reason for the exception.
    /// </param>
    public SerializationException(System.String message)
        : base(message)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="SerializationException"/> class
    /// with a specified error message and a reference to the inner exception that is
    /// the cause of this exception.
    /// </summary>
    /// <param name="message">
    /// The error message that explains the reason for the exception.
    /// </param>
    /// <param name="innerException">
    /// The exception that is the cause of the current exception.
    /// </param>
    public SerializationException(System.String message, System.Exception innerException)
        : base(message, innerException)
    {
    }
}
