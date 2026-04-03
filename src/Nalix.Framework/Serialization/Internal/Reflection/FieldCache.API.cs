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
/// Exposes the cached field layout for a specific serialized type.
/// The generic cache is built once per closed <typeparamref name="T"/> and then
/// reused by the serializer without repeating reflection work.
/// </summary>
/// <typeparam name="T">The type whose fields are being cached.</typeparam>
internal static partial class FieldCache<T>
{
    /// <summary>
    /// Retrieves the cached field metadata array for the current type.
    /// </summary>
    /// <returns>The cached <see cref="FieldSchema"/> array for <typeparamref name="T"/>.</returns>
    [DebuggerStepThrough]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static FieldSchema[] GetFields() => s_metadata;

}
