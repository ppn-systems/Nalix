using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace Nalix.SDK.Tools.Extensions;

/// <summary>
/// Provides helper methods for safe reflection operations used by the tool.
/// </summary>
public static class ReflectionExtensions
{
    /// <summary>
    /// Returns all loadable types from the specified assembly.
    /// </summary>
    /// <param name="assembly">The assembly to inspect.</param>
    /// <returns>The loadable types.</returns>
    public static IEnumerable<Type> GetLoadableTypes(this Assembly assembly)
    {
        ArgumentNullException.ThrowIfNull(assembly);

        try
        {
            return assembly.GetTypes();
        }
        catch (ReflectionTypeLoadException exception)
        {
            return exception.Types.Where(static type => type is not null)!;
        }
    }
}
