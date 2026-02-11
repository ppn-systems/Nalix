// Copyright (c) 2025-2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Diagnostics;
using System.Diagnostics.Contracts;
using System.Runtime.CompilerServices;

namespace Nalix.SDK;

/// <summary>
/// An <see cref="IThreadDispatcher"/> that executes actions immediately on the calling thread.
/// </summary>
/// <remarks>
/// This dispatcher does not switch threads. It is typically used for tests or
/// for environments where thread marshalling is not required.
/// </remarks>
[StackTraceHidden]
[DebuggerStepThrough]
[DebuggerNonUserCode]
public sealed class InlineDispatcher : IThreadDispatcher
{
    /// <inheritdoc/>
    [Pure]
    [MethodImpl(MethodImplOptions.NoInlining)]
    public void Post(Action action) => action?.Invoke();
}
