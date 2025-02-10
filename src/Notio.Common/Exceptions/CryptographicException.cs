namespace Notio.Common.Exceptions;

/// <summary>
/// Custom exception for errors related to cryptographic operations.
/// </summary>
[System.Serializable]
public class CryptographicException : System.Exception
{
    /// <summary>
    /// Initializes a new instance of the <see cref="CryptographicException"/> class with a specified error message.
    /// </summary>
    /// <param name="message">The message that describes the error.</param>
    public CryptographicException(string message)
        : base(message)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="CryptographicException"/> class with a specified error message and a reference to the inner exception that is the cause of this exception.
    /// </summary>
    /// <param name="message">The error message that explains the reason for the exception.</param>
    /// <param name="innerException">The exception that is the cause of the current exception.</param>
    public CryptographicException(string message, System.Exception innerException)
        : base(message, innerException)
    {
    }
}
