namespace Notio.Common.Enums;

/// <summary>
/// Represents the types of errors that can occur during a connection.
/// </summary>
public enum ConnectionError
{
    /// <summary>
    /// An undefined error.
    /// </summary>
    Undefined,

    /// <summary>
    /// Error occurred while reading data.
    /// </summary>
    ReadError,

    /// <summary>
    /// Error occurred while sending data.
    /// </summary>
    SendError,

    /// <summary>
    /// Network connection error.
    /// </summary>
    NetworkError,

    /// <summary>
    /// Encryption error.
    /// </summary>
    EncryptionError,

    /// <summary>
    /// Decryption error.
    /// </summary>
    DecryptionError,

    /// <summary>
    /// Mismatch in data.
    /// </summary>
    DataMismatch,

    /// <summary>
    /// Data size is too large.
    /// </summary>
    DataTooLarge,

    /// <summary>
    /// Connection was lost.
    /// </summary>
    ConnectionLost,

    /// <summary>
    /// Authentication error.
    /// </summary>
    AuthenticationError,

    /// <summary>
    /// Error occurred while closing the connection.
    /// </summary>
    CloseError,

    /// <summary>
    /// The stream was closed.
    /// </summary>
    StreamClosed
}