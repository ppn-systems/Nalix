// Copyright (c) 2025-2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System;

namespace Nalix.SDK;

/// <summary>
/// Provides a mechanism for dispatching actions onto a target thread.
/// </summary>
/// <remarks>
/// This abstraction is useful in cross-platform applications where code may need
/// to marshal work back to a UI thread or other single-threaded context.
/// </remarks>
public interface IThreadDispatcher
{
    /// <summary>
    /// Posts an action for execution on the target thread.
    /// </summary>
    /// <param name="action">The action to execute.</param>
    void Post(Action action);
}
