// Copyright (c) 2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

namespace Nalix.Abstractions;

/// <summary>
/// Controls rented-address validation when returning buffers to a pool.
/// </summary>
/// <remarks>
/// <para>
/// <b>Disabled</b> disables all rented-address tracking, eliminating
/// dictionary allocations from the hot path.
/// This is the recommended mode for production and benchmarks.
/// </para>
/// <para>
/// <b>SilentDrop</b> enables tracking and silently drops invalid returns.
/// </para>
/// <para>
/// <b>ThrowOnError</b> enables tracking and throws on invalid returns.
/// Intended for tests and debugging only.
/// </para>
/// </remarks>
public enum ReturnValidation
{
    /// <summary>
    /// No rented-address tracking. Zero-allocation production hot path.
    /// Double returns are prevented only by ownership checks and GC generation guards.
    /// </summary>
    Disabled = 0,

    /// <summary>
    /// Track rented addresses. Invalid returns are silently ignored.
    /// </summary>
    SilentDrop = 1,

    /// <summary>
    /// Track rented addresses. Invalid returns throw exceptions.
    /// Intended for tests and debugging only.
    /// </summary>
    ThrowOnError = 2
}
