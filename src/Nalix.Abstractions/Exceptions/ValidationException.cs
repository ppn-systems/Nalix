// Copyright (c) 2025-2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System;

namespace Nalix.Abstractions.Exceptions;

/// <summary>
/// The exception that is thrown when a configuration or data validation check fails.
/// AOT-safe replacement for <c>System.ComponentModel.DataAnnotations.ValidationException</c>.
/// </summary>
public class ValidationException : BaseException
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ValidationException"/> class.
    /// </summary>
    public ValidationException()
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="ValidationException"/> class
    /// with a specified error message.
    /// </summary>
    /// <param name="message">
    /// The error message that explains the reason for the exception.
    /// </param>
    public ValidationException(string message)
        : base(message)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="ValidationException"/> class
    /// with a specified error message and a reference to the inner exception
    /// that is the cause of this exception.
    /// </summary>
    /// <param name="message">
    /// The error message that explains the reason for the exception.
    /// </param>
    /// <param name="innerException">
    /// The exception that caused the current exception.
    /// </param>
    public ValidationException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
