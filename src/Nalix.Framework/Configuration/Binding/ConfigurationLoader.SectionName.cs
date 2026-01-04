// Copyright (c) 2025 PPN Corporation. All rights reserved.

namespace Nalix.Framework.Configuration.Binding;

public partial class ConfigurationLoader
{
    /// <summary>
    /// Gets the section name for a configuration type, with caching for performance.
    /// </summary>
    [System.Diagnostics.Contracts.Pure]
    [System.Diagnostics.DebuggerStepThrough]
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    [return: System.Diagnostics.CodeAnalysis.NotNull]
    private static System.String GetSectionName(System.Type type)
        => _sectionNameCache.GetOrAdd(type, t =>
        {
            System.String section = t.Name;

            // Manual iteration to avoid LINQ allocation overhead
            System.String? longestMatch = null;
            System.Int32 longestLength = 0;

            foreach (System.String suffix in _suffixesToTrim)
            {
                if (suffix.Length > longestLength && 
                    section.EndsWith(suffix, System.StringComparison.OrdinalIgnoreCase))
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
    [System.Diagnostics.Contracts.Pure]
    [System.Diagnostics.DebuggerStepThrough]
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    [return: System.Diagnostics.CodeAnalysis.NotNull]
    private static System.String Capitalize(System.String input)
        => System.String.IsNullOrEmpty(input) ? input : System.Char.ToUpperInvariant(input[0]) + input[1..];
}
