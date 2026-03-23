// Copyright (c) 2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

namespace Nalix.SDK;

/// <summary>
/// A dispatcher implementation that executes actions immediately on the calling thread.
/// </summary>
/// <remarks>
/// This dispatcher does not switch to any specific thread (e.g., UI thread).
/// It is typically used for testing, non-UI scenarios, or environments where
/// thread marshalling is not required.
/// </remarks>
[System.Diagnostics.StackTraceHidden]
[System.Diagnostics.DebuggerStepThrough]
[System.Diagnostics.DebuggerNonUserCode]
public sealed class InlineDispatcher : IThreadDispatcher
{
    /// <summary>
    /// Executes the specified <paramref name="action"/> synchronously on the current thread.
    /// </summary>
    /// <param name="action">
    /// The action to execute. If <c>null</c>, no operation is performed.
    /// </param>
    [System.Diagnostics.Contracts.Pure]
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    public void Post(System.Action action) => action?.Invoke();
}