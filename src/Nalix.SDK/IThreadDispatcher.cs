// Copyright (c) 2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

namespace Nalix.SDK;

/// <summary>
/// Provides a mechanism to dispatch actions onto the main/UI thread.
/// </summary>
/// <remarks>
/// This abstraction is useful in cross-platform applications (e.g., .NET MAUI)
/// where code may need to update UI elements from background threads.
/// </remarks>
public interface IThreadDispatcher
{
    /// <summary>
    /// Posts an action to be executed on the main/UI thread asynchronously.
    /// </summary>
    /// <param name="action">
    /// The action to execute on the UI thread.
    /// </param>
    void Post(System.Action action);
}
