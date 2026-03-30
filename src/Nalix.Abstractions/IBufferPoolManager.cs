// Copyright (c) 2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System;

namespace Nalix.Abstractions;

/// <summary>
/// Defines a contract for managing pooled byte buffers with support for
/// allocation strategies, diagnostics, and memory trimming.
/// </summary>
/// <remarks>
/// This abstraction allows consumers to rent and return buffers efficiently
/// without being coupled to a specific pooling implementation.
/// </remarks>
public interface IBufferPoolManager : IDisposable, IReportable
{
    /// <summary>
    /// Rents a buffer with at least the specified minimum length.
    /// </summary>
    /// <param name="minimumLength">
    /// The minimum number of bytes required.
    /// </param>
    /// <returns>
    /// A byte array whose length is greater than or equal to <paramref name="minimumLength"/>.
    /// </returns>
    /// <exception cref="ArgumentException">
    /// Thrown when the requested size cannot be satisfied and fallback is disabled.
    /// </exception>
    byte[] Rent(int minimumLength = 256);

    /// <summary>
    /// Returns a buffer to the pool.
    /// </summary>
    /// <param name="array">
    /// The buffer to return. If <see langword="null"/>, the call is ignored.
    /// </param>
    /// <param name="arrayClear">
    /// If <see langword="true"/>, the buffer will be cleared before being returned.
    /// </param>
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Naming", "CA1716:Identifiers should not match keywords", Justification = "<Pending>")]
    void Return(byte[]? array, bool arrayClear = false);
}
