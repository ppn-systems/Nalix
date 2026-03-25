// Copyright (c) 2025-2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Diagnostics.Contracts;
using System.Globalization;
using System.Reflection;
using System.Runtime.CompilerServices;
using Nalix.Common.Diagnostics;
using Nalix.Framework.Configuration.Internal;
using Nalix.Framework.Injection;

namespace Nalix.Framework.Configuration.Binding;

public partial class ConfigurationLoader
{
    /// <summary>
    /// Cache for enum getter methods to avoid repeated reflection calls.
    /// </summary>
    private static readonly System.Collections.Concurrent.ConcurrentDictionary<
        Type, MethodInfo> _enumGetterCache = new();

    /// <summary>
    /// Gets the configuration value for a property using the appropriate method.
    /// </summary>
    [Pure]
    [DebuggerStepThrough]
    [MethodImpl(MethodImplOptions.NoInlining |
        MethodImplOptions.AggressiveOptimization)]
    [return: MaybeNull]
    private static object? GetConfigValue(
        IniConfig configFile, string section, PropertyMetadata property)
    {
        // Handle Enums of any underlying type with cached reflection
        if (property.PropertyType.IsEnum)
        {
            MethodInfo method = _enumGetterCache.GetOrAdd(
                property.PropertyType,
                enumType =>
                {
                    MethodInfo? baseMethod = typeof(IniConfig).GetMethod(
                        nameof(IniConfig.GetEnum),
                        BindingFlags.Public | BindingFlags.Instance);

                    return baseMethod == null
                        ? throw new InvalidOperationException($"Could not find GetEnum method on {nameof(IniConfig)}.")
                        : baseMethod.MakeGenericMethod(enumType);
                });

            return method.Invoke(configFile, [section, property.Name]);
        }

        return property.TypeCode switch
        {
            TypeCode.Char => configFile.GetChar(section, property.Name),
            TypeCode.Byte => configFile.GetByte(section, property.Name),
            TypeCode.SByte => configFile.GetSByte(section, property.Name),
            TypeCode.String => configFile.GetString(section, property.Name),
            TypeCode.Boolean => configFile.GetBool(section, property.Name),
            TypeCode.Decimal => configFile.GetDecimal(section, property.Name),
            TypeCode.Int16 => configFile.GetInt16(section, property.Name),
            TypeCode.UInt16 => configFile.GetUInt16(section, property.Name),
            TypeCode.Int32 => configFile.GetInt32(section, property.Name),
            TypeCode.UInt32 => configFile.GetUInt32(section, property.Name),
            TypeCode.Int64 => configFile.GetInt64(section, property.Name),
            TypeCode.UInt64 => configFile.GetUInt64(section, property.Name),
            TypeCode.Single => configFile.GetSingle(section, property.Name),
            TypeCode.Double => configFile.GetDouble(section, property.Name),
            TypeCode.DateTime => configFile.GetDateTime(section, property.Name),
            TypeCode.Object when property.PropertyType == typeof(Guid)
                => configFile.GetGuid(section, property.Name),
            TypeCode.Object when property.PropertyType == typeof(TimeSpan)
                => configFile.GetTimeSpan(section, property.Name),
            TypeCode.Empty => throw new NotImplementedException(),
            TypeCode.DBNull => throw new NotImplementedException(),
            _ => ThrowUnsupported(property),
        };
    }

    /// <summary>
    /// Handles empty configuration values by writing defaults — and any associated
    /// comment — to the file. The comment is written only when the key is new,
    /// consistent with <see cref="IniConfig.WriteValue"/> behavior.
    /// </summary>
    [DebuggerStepThrough]
    [MethodImpl(MethodImplOptions.NoInlining |
        MethodImplOptions.AggressiveOptimization)]
    private void HandleEmptyValue(
        IniConfig configFile, string section, PropertyMetadata property)
    {
        object? currentValue = property.PropertyInfo.GetValue(this);

        object valueToWrite = property.PropertyType.IsEnum
            ? currentValue?.ToString()
              ?? Enum.GetValues(property.PropertyType).GetValue(0)!.ToString()!
            : currentValue?.ToString()
              ?? GetDefaultValueString(property);

        // WriteValue already guards against overwriting an existing key.
        // WriteComment is called first so the comment appears above the key;
        // both writes are no-ops if the key already exists in the file.
        configFile.WriteComment(section, property.Name, property.Comment);
        configFile.WriteValue(section, property.Name, valueToWrite);

        InstanceManager.Instance.GetExistingInstance<ILogger>()?
                                .Debug($"[FW.{nameof(ConfigurationLoader)}:Internal] " +
                                       $"default-written section={section} key={property.Name} val={valueToWrite}");
    }

    /// <summary>
    /// Gets a default value string for the specified type code.
    /// </summary>
    [Pure]
    [DebuggerStepThrough]
    [MethodImpl(MethodImplOptions.AggressiveInlining |
        MethodImplOptions.AggressiveOptimization)]
    private static string GetDefaultValueString(PropertyMetadata propertyType)
    {
        switch (propertyType.TypeCode)
        {
            case TypeCode.Object:
                if (propertyType.PropertyType == typeof(Guid))
                {
                    return Guid.Empty.ToString("c", CultureInfo.InvariantCulture);
                }

                if (propertyType.PropertyType == typeof(TimeSpan))
                {
                    return TimeSpan.Zero.ToString("c", CultureInfo.InvariantCulture);
                }

                break;
            case TypeCode.Char:
            case TypeCode.String:
                return string.Empty;
            case TypeCode.Boolean:
                return "false";
            case TypeCode.SByte:
            case TypeCode.Byte:
            case TypeCode.Int16:
            case TypeCode.UInt16:
            case TypeCode.Int32:
            case TypeCode.UInt32:
            case TypeCode.Int64:
            case TypeCode.UInt64:
            case TypeCode.Single:
            case TypeCode.Double:
            case TypeCode.Decimal:
                return "0";
            case TypeCode.DateTime:
                return DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture);
            case TypeCode.Empty:
                break;
            case TypeCode.DBNull:
                break;
            default:
                break;
        }

        return string.Empty;
    }

    [StackTraceHidden]
    [DebuggerStepThrough]
    [DoesNotReturn]
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static object ThrowUnsupported(PropertyMetadata property)
    {
        InstanceManager.Instance.GetExistingInstance<ILogger>()?
                                .Error($"[FW.{nameof(ConfigurationLoader)}:Internal] " +
                                       $"unsupported-type type={property.PropertyType.Name} info={property.PropertyInfo.Name} key={property.Name}");

        throw new NotSupportedException(
            $"Value type {property.PropertyType.Name} is not supported for configuration files.");
    }
}
