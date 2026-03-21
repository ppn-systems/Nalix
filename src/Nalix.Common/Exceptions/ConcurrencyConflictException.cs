// Copyright (c) 2025 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

namespace Nalix.Common.Exceptions;

/// <summary>
/// Represents an exception that is thrown when a concurrency conflict is detected
/// and the requested operation is rejected to prevent inconsistent state.
/// </summary>
/// <remarks>
/// This exception is typically used in scenarios where multiple threads or processes
/// attempt to modify the same resource simultaneously, and a conflict resolution
/// strategy determines that the operation must be denied.
/// </remarks>
public sealed class ConcurrencyConflictException : InternalErrorException
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ConcurrencyConflictException"/> class.
    /// </summary>
    public ConcurrencyConflictException() : base()
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="ConcurrencyConflictException"/> class
    /// with a specified error message.
    /// </summary>
    /// <param name="message">A message that describes the error.</param>
    public ConcurrencyConflictException(System.String message) : base(message)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="ConcurrencyConflictException"/> class
    /// with a specified error message and additional details.
    /// </summary>
    /// <param name="message">A message that describes the error.</param>
    /// <param name="details">Additional information that provides more context about the error.</param>
    public ConcurrencyConflictException(System.String message, System.String details) : base(message, details)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="ConcurrencyConflictException"/> class
    /// with a specified error message and a reference to the inner exception that caused this exception.
    /// </summary>
    /// <param name="message">A message that describes the error.</param>
    /// <param name="innerException">The exception that caused the current exception.</param>
    public ConcurrencyConflictException(System.String message, System.Exception innerException) : base(message, innerException)
    {
    }
}