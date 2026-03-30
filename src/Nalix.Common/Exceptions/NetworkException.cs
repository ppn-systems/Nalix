// Copyright (c) 2025-2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System;

namespace Nalix.Common.Exceptions;

/// <summary>
/// Represents a network-related error that occurs during network operations.
/// </summary>
public class NetworkException : BaseException
{
    /// <summary>
    /// Initializes a new instance of the <see cref="NetworkException"/> class.
    /// </summary>
    public NetworkException()
    { }

    /// <summary>
    /// Initializes a new instance of the <see cref="NetworkException"/> class with a specified error message.
    /// </summary>
    /// <param name="message">The error message that explains the reason for the exception.</param>
    public NetworkException(string message)
        : base(message) { }

    /// <summary>
    /// Initializes a new instance of the <see cref="NetworkException"/> class with a specified error message and a reference to the inner exception that is the cause of this exception.
    /// </summary>
    /// <param name="message">The error message that explains the reason for the exception.</param>
    /// <param name="innerException">The exception that is the cause of this exception.</param>
    public NetworkException(string message, Exception innerException)
        : base(message, innerException) { }
}
