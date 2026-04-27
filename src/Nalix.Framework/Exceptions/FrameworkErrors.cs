// Copyright (c) 2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System;

namespace Nalix.Framework.Exceptions;

/// <summary>
/// Provides cached, zero-allocation exception instances for common framework errors.
/// These instances avoid the overhead of stack trace generation by overriding the StackTrace property.
/// </summary>
internal static class FrameworkErrors
{
    #region Resource Errors

    public static readonly InvalidOperationException SlabBucketAllocationFailed =
        new CachedInvalidOperationException("SlabBucket: failed to allocate standalone buffer.");

    #endregion Resource Errors

    #region Private Cached Exception Types

    private sealed class CachedInvalidOperationException(string message) : InvalidOperationException(message)
    {
        public override string? StackTrace => "   at Nalix.Framework (Cached Exception)";
    }

    #endregion Private Cached Exception Types
}
