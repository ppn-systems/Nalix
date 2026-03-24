// Copyright (c) 2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System;

namespace Nalix.Common.Exceptions;

/// <summary>
/// Classifies runtime exceptions for safe recovery paths.
/// </summary>
public static class ExceptionClassifier
{
    /// <summary>
    /// Returns <see langword="true"/> when the exception is recoverable and safe to catch.
    /// Fatal runtime exceptions should never be swallowed by resilience paths.
    /// </summary>
    /// <param name="ex">The exception to classify.</param>
    /// <returns><see langword="true"/> if the exception is non-fatal.</returns>
    public static bool IsNonFatal(Exception ex)
    {
        ArgumentNullException.ThrowIfNull(ex);

        return ex is not (
            OutOfMemoryException or
            StackOverflowException or
            AccessViolationException or
            AppDomainUnloadedException or
            BadImageFormatException or
            CannotUnloadAppDomainException);
    }
}
