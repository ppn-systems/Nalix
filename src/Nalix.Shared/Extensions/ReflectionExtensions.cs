// Copyright (c) 2025 PPN Corporation. All rights reserved.

namespace Nalix.Shared.Extensions;

/// <summary>
/// Small helpers to keep builder code short and readable.
/// </summary>
internal static class ReflectionExtensions
{
    public static System.Reflection.MethodInfo? PublicStatic(this System.Type type, System.String name)
        => type.GetMethod(name, System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
}
