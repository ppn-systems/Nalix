// Copyright (c) 2025-2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Diagnostics;
using System.Diagnostics.Contracts;
using System.Runtime.CompilerServices;

namespace Nalix.Framework.Configuration.Binding;

public partial class ConfigurationLoader
{
    /// <summary>
    /// Gets the section name for a configuration type, with caching for performance.
    /// </summary>
    [Pure]
    [DebuggerStepThrough]
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static string GetSectionName(Type type)
        => _sectionNameCache.GetOrAdd(type, t =>
        {
            string section = t.Name;

            // Manual iteration to avoid LINQ allocation overhead
            string? longestMatch = null;
            int longestLength = 0;

            foreach (string suffix in _suffixesToTrim)
            {
                if (suffix.Length > longestLength &&
                    section.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
                {
                    longestMatch = suffix;
                    longestLength = suffix.Length;
                }
            }

            if (longestMatch != null)
            {
                section = section[..^longestLength];
            }

            return Capitalize(section);
        });

    /// <summary>
    /// Capitalizes the first letter of a string.
    /// </summary>
    [Pure]
    [DebuggerStepThrough]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static string Capitalize(string input)
        => string.IsNullOrEmpty(input) ? input : char.ToUpperInvariant(input[0]) + input[1..];
}
