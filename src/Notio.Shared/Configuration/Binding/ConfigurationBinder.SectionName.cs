using System;
using System.Linq;
using System.Runtime.CompilerServices;

namespace Notio.Shared.Configuration.Binding;

public partial class ConfigurationBinder
{
    /// <summary>
    /// Gets the section name for a configuration type, with caching for performance.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static string GetSectionName(Type type)
        => _sectionNameCache.GetOrAdd(type, t =>
        {
            string section = t.Name;

            foreach (string suffix in _suffixesToTrim.OrderByDescending(s => s.Length))
            {
                if (section.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
                {
                    section = section[..^suffix.Length];
                    break;
                }
            }

            return Capitalize(section);
        });

    /// <summary>
    /// Capitalizes the first letter of a string.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static string Capitalize(string input)
        => string.IsNullOrEmpty(input) ? input : char.ToUpperInvariant(input[0]) + input[1..];
}
