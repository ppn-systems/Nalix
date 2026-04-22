// Copyright (c) 2025-2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System;

namespace Nalix.Common.Exceptions;

/// <summary>
/// Represents errors that occur during LZ4 compression or decompression.
/// </summary>
public class LZ4Exception : BaseException
{
    /// <summary>
    /// Initializes a new instance of the <see cref="LZ4Exception"/> class.
    /// </summary>
    public LZ4Exception()
        : base("An LZ4 compression or decompression error occurred.")
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="LZ4Exception"/> class with a specified error message.
    /// </summary>
    /// <param name="message">The message that describes the error.</param>
    public LZ4Exception(string message)
        : base(message)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="LZ4Exception"/> class with a specified error message and inner exception.
    /// </summary>
    /// <param name="message">The message that describes the error.</param>
    /// <param name="innerException">The inner exception that is the cause of this exception.</param>
    public LZ4Exception(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
