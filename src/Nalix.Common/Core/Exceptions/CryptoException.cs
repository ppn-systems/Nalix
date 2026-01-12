// Copyright (c) 2025 PPN Corporation. All rights reserved.

namespace Nalix.Common.Core.Exceptions;

/// <summary>
/// Represents errors that occur during cryptographic operations such as encryption, decryption, or key generation.
/// </summary>
/// <remarks>
/// This exception is typically thrown when a cryptographic process fails due to invalid data,
/// unsupported algorithms, or incorrect keys.
/// </remarks>
[System.Serializable]
public class CryptoException : BaseException
{
    /// <summary>
    /// Initializes a new instance of the <see cref="CryptoException"/> class
    /// with a default error message.
    /// </summary>
    public CryptoException()
        : base("A cryptographic operation error occurred.")
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="CryptoException"/> class
    /// with a specified error message.
    /// </summary>
    /// <param name="message">
    /// The error message that describes the reason for the exception.
    /// </param>
    public CryptoException(System.String message)
        : base(message)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="CryptoException"/> class
    /// with a specified error message and a reference to the inner exception
    /// that is the cause of this exception.
    /// </summary>
    /// <param name="message">
    /// The error message that explains the reason for the exception.
    /// </param>
    /// <param name="innerException">
    /// The exception that caused the current exception.
    /// </param>
    public CryptoException(System.String message, System.Exception innerException)
        : base(message, innerException)
    {
    }
}
