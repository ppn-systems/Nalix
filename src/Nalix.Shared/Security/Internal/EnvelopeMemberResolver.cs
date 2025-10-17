// Copyright (c) 2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using Nalix.Common.Security.Attributes;
using Nalix.Common.Security.Enums;

namespace Nalix.Shared.Security.Internal;

/// <summary>
/// Resolves and caches sensitive-member metadata for a given CLR type.
/// <para>
/// On first access the resolver scans all public instance properties and fields
/// for <see cref="SensitiveDataAttribute"/>, compiles getter/setter delegates via
/// <see cref="System.Runtime.CompilerServices.UnsafeAccessorAttribute"/> -style
/// Expression trees, and stores everything in a thread-safe cache.
/// Subsequent accesses are a single dictionary lookup — O(1), zero allocations.
/// </para>
/// </summary>
internal static class EnvelopeMemberResolver
{
    // ── Constants ────────────────────────────────────────────────────────────

    /// <summary>
    /// Minimum sensitivity level that triggers encryption.
    /// Members below this threshold are skipped silently.
    /// </summary>
    internal const DataSensitivityLevel EncryptionThreshold = DataSensitivityLevel.Confidential;

    // ── Cache ─────────────────────────────────────────────────────────────────

    // ConcurrentDictionary: write only on first access per type, reads are lock-free after that.
    private static readonly System.Collections.Concurrent.ConcurrentDictionary<System.Type, SensitiveMemberCache> _cache = new();

    // ── Public API ────────────────────────────────────────────────────────────

    /// <summary>
    /// Returns the <see cref="SensitiveMemberCache"/> for <paramref name="type"/>,
    /// building and caching it on first call.
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    internal static SensitiveMemberCache GetMembers(System.Type type) => _cache.GetOrAdd(type, BuildCache);

    // ── Private Helpers ───────────────────────────────────────────────────────

    private static SensitiveMemberCache BuildCache(System.Type t)
    {
        // ── Properties ────────────────────────────────────────────────────────
        System.Reflection.PropertyInfo[] allProperties = t.GetProperties(
            System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);

        System.Collections.Generic.List<SensitiveMemberInfo> tempProps = new(allProperties.Length);

        foreach (System.Reflection.PropertyInfo prop in allProperties)
        {
            // Skip read-only properties — we need to write back the encrypted/decrypted value.
            if (!prop.CanWrite)
            {
                continue;
            }

            SensitiveDataAttribute? attr = System.Reflection.CustomAttributeExtensions.GetCustomAttribute<SensitiveDataAttribute>(prop);
            if (attr is null)
            {
                continue;
            }

            tempProps.Add(new SensitiveMemberInfo
            {
                Name = prop.Name,
                MemberType = prop.PropertyType,
                Level = attr.Level,
                Getter = BuildPropertyGetter(prop),
                Setter = BuildPropertySetter(prop)
            });
        }

        // ── Fields ────────────────────────────────────────────────────────────
        System.Reflection.FieldInfo[] allFields = t.GetFields(
            System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);

        System.Collections.Generic.List<SensitiveMemberInfo> tempFields = new(allFields.Length);

        foreach (System.Reflection.FieldInfo field in allFields)
        {
            SensitiveDataAttribute? attr = System.Reflection.CustomAttributeExtensions.GetCustomAttribute<SensitiveDataAttribute>(field);
            if (attr is null)
            {
                continue;
            }

            tempFields.Add(new SensitiveMemberInfo
            {
                Name = field.Name,
                MemberType = field.FieldType,
                Level = attr.Level,
                Getter = BuildFieldGetter(field),
                Setter = BuildFieldSetter(field)
            });
        }

        SensitiveMemberInfo[] propsArray = [.. tempProps];
        SensitiveMemberInfo[] fieldsArray = [.. tempFields];

        // Pre-compute encryptable count once — avoids LINQ on every Decrypt error path.
        System.Int32 encryptableCount = 0;
        foreach (SensitiveMemberInfo m in propsArray)
        {
            if (m.Level >= EncryptionThreshold)
            {
                encryptableCount++;
            }
        }
        foreach (SensitiveMemberInfo m in fieldsArray)
        {
            if (m.Level >= EncryptionThreshold)
            {
                encryptableCount++;
            }
        }

        return new SensitiveMemberCache(propsArray, fieldsArray, encryptableCount);
    }

    // ── Compiled Accessors ────────────────────────────────────────────────────
    // Expression-compiled once per member → O(1) access thereafter, no reflection overhead.

    private static System.Func<System.Object, System.Object?> BuildPropertyGetter(System.Reflection.PropertyInfo prop)
    {
        var instance = System.Linq.Expressions.Expression.Parameter(typeof(System.Object), "obj");
        var cast = System.Linq.Expressions.Expression.Convert(instance, prop.DeclaringType!);
        var access = System.Linq.Expressions.Expression.Property(cast, prop);
        var box = System.Linq.Expressions.Expression.Convert(access, typeof(System.Object));
        return System.Linq.Expressions.Expression.Lambda<System.Func<System.Object, System.Object?>>(box, instance).Compile();
    }

    private static System.Action<System.Object, System.Object?> BuildPropertySetter(System.Reflection.PropertyInfo prop)
    {
        var instance = System.Linq.Expressions.Expression.Parameter(typeof(System.Object), "obj");
        var value = System.Linq.Expressions.Expression.Parameter(typeof(System.Object), "val");
        var cast = System.Linq.Expressions.Expression.Convert(instance, prop.DeclaringType!);
        var castVal = System.Linq.Expressions.Expression.Convert(value, prop.PropertyType);
        var assign = System.Linq.Expressions.Expression.Call(cast, prop.SetMethod!, castVal);
        return System.Linq.Expressions.Expression.Lambda<System.Action<System.Object, System.Object?>>(assign, instance, value).Compile();
    }

    private static System.Func<System.Object, System.Object?> BuildFieldGetter(System.Reflection.FieldInfo field)
    {
        var instance = System.Linq.Expressions.Expression.Parameter(typeof(System.Object), "obj");
        var cast = System.Linq.Expressions.Expression.Convert(instance, field.DeclaringType!);
        var access = System.Linq.Expressions.Expression.Field(cast, field);
        var box = System.Linq.Expressions.Expression.Convert(access, typeof(System.Object));
        return System.Linq.Expressions.Expression.Lambda<System.Func<System.Object, System.Object?>>(box, instance).Compile();
    }

    private static System.Action<System.Object, System.Object?> BuildFieldSetter(System.Reflection.FieldInfo field)
    {
        var instance = System.Linq.Expressions.Expression.Parameter(typeof(System.Object), "obj");
        var value = System.Linq.Expressions.Expression.Parameter(typeof(System.Object), "val");
        var cast = System.Linq.Expressions.Expression.Convert(instance, field.DeclaringType!);
        var castVal = System.Linq.Expressions.Expression.Convert(value, field.FieldType);
        var assign = System.Linq.Expressions.Expression.Assign(System.Linq.Expressions.Expression.Field(cast, field), castVal);
        return System.Linq.Expressions.Expression.Lambda<System.Action<System.Object, System.Object?>>(assign, instance, value).Compile();
    }
}
