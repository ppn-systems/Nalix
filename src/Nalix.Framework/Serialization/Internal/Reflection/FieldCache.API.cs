// Copyright (c) 2025-2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;

#if DEBUG
[assembly: InternalsVisibleTo("Nalix.Framework.Tests")]
[assembly: InternalsVisibleTo("Nalix.Framework.Benchmarks")]
#endif

namespace Nalix.Framework.Serialization.Internal.Reflection;

/// <summary>
/// Provides caching mechanisms for retrieving field metadata efficiently.
/// </summary>
/// <typeparam name="T">The type whose fields are being cached.</typeparam>
internal static partial class FieldCache<T>
{
    /// <summary>
    /// Retrieves all cached field metadata as a span.
    /// </summary>
    /// <returns>A <see cref="ReadOnlySpan{T}"/> containing metadata for all fields.</returns>
    [DebuggerStepThrough]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static FieldSchema[] GetFields() => s_metadata;

}
