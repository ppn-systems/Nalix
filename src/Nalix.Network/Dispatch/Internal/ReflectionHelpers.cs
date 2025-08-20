// Copyright (c) 2025 PPN Corporation. All rights reserved.

[assembly: System.Runtime.CompilerServices.InternalsVisibleTo("Nalix.Network.Tests")]
[assembly: System.Runtime.CompilerServices.InternalsVisibleTo("Nalix.Network.Benchmarks")]

namespace Nalix.Network.Dispatch.Internal;

/// <summary>
/// Small helpers to keep builder code short and readable.
/// </summary>
internal static class ReflectionHelpers
{
    public static System.Collections.Generic.IEnumerable<System.Type> SafeGetTypes(System.Reflection.Assembly asm)
    {
        try
        {
            return asm.GetTypes();
        }
        catch (System.Reflection.ReflectionTypeLoadException ex)
        {
            return System.Linq.Enumerable.OfType<System.Type>(ex.Types);
        }
        catch
        {
            return [];
        }
    }

    public static System.Reflection.MethodInfo? PublicStatic(this System.Type type, System.String name)
        => type.GetMethod(name, System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
}
