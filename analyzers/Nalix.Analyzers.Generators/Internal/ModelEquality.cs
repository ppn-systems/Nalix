// Copyright (c) 2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Collections.Immutable;

namespace Nalix.Analyzers.Generators.Internal;

/// <summary>
/// Null-safe structural comparison of <see cref="ImmutableArray{T}"/> for incremental-generator models.
/// </summary>
/// <remarks>
/// A model produced by a <c>return default</c> path carries <c>default</c> (null-backed) arrays. The
/// incremental driver compares consecutive model values via <c>Equals</c> in
/// <c>NodeStateTable.Builder.GetModifiedItemAndState</c>; any access to <see cref="ImmutableArray{T}.Length"/>
/// or <see cref="ImmutableArray{T}.SequenceEqual"/> on a <c>default</c> value dereferences the null backing
/// array and throws <see cref="NullReferenceException"/>, which Roslyn surfaces as an IDE-wide crash. These
/// helpers treat <c>default</c> and empty as equivalent so equality never touches the null backing array.
/// </remarks>
internal static class ModelEquality
{
    /// <summary>
    /// Compares two arrays element-by-element using the default equality comparer,
    /// treating <c>default</c> and empty as equal.
    /// </summary>
    public static bool SequenceEqual<T>(ImmutableArray<T> left, ImmutableArray<T> right)
    {
        if (left.IsDefaultOrEmpty && right.IsDefaultOrEmpty)
        {
            return true;
        }

        if (left.IsDefaultOrEmpty || right.IsDefaultOrEmpty)
        {
            return false;
        }

        if (left.Length != right.Length)
        {
            return false;
        }

        for (int i = 0; i < left.Length; i++)
        {
            if (!System.Collections.Generic.EqualityComparer<T>.Default.Equals(left[i], right[i]))
            {
                return false;
            }
        }

        return true;
    }
}
