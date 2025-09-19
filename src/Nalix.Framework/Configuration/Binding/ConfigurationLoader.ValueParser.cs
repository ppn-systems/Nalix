// Copyright (c) 2025 PPN Corporation. All rights reserved.

using Nalix.Common.Diagnostics;
using Nalix.Framework.Configuration.Internal;
using Nalix.Framework.Injection;
using System.Collections.Generic;

namespace Nalix.Framework.Configuration.Binding;

public partial class ConfigurationLoader
{
    /// <summary>
    /// Cache for enum getter methods to avoid repeated reflection calls.
    /// </summary>
    private static readonly System.Collections.Concurrent.ConcurrentDictionary<
        System.Type, System.Reflection.MethodInfo> _enumGetterCache = new();

    /// <summary>
    /// Gets the configuration value for a property using the appropriate method.
    /// </summary>
    [System.Diagnostics.Contracts.Pure]
    [System.Diagnostics.DebuggerStepThrough]
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.NoInlining |
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
    [return: System.Diagnostics.CodeAnalysis.MaybeNull]
    private static System.Object? GetConfigValue(IniConfig configFile, System.String section, PropertyMetadata property)
    {
        // --- Support binding List<T> ---
        if (property.PropertyType.IsGenericType &&
            property.PropertyType.GetGenericTypeDefinition() == typeof(System.Collections.Generic.List<>))
        {
            // Đọc chuỗi từ config: "A,B,C"
            System.String csv = configFile.GetString(section, property.Name);
            System.Type elementType = property.PropertyType.GetGenericArguments()[0];

            System.String[] values = System.String.IsNullOrEmpty(csv)
                ? [] : csv.Split([','], System.StringSplitOptions.RemoveEmptyEntries | System.StringSplitOptions.TrimEntries);

            // Chuyển từng phần tử sang kiểu đúng
            System.Collections.IList list = (System.Collections.IList)System.Activator.CreateInstance(typeof(List<>).MakeGenericType(elementType))!;

            foreach (System.String s in values)
            {
                System.Object? v = elementType == typeof(System.String)
                    ? s
                    : elementType.IsEnum
                        ? System.Enum.Parse(elementType, s, ignoreCase: true)
                        : elementType == typeof(System.Int32)
                            ? System.Int32.Parse(s)
                        : elementType == typeof(System.Int64)
                            ? System.Int64.Parse(s)
                        : elementType == typeof(System.Byte)
                            ? System.Byte.Parse(s)
                        : elementType == typeof(System.SByte)
                            ? System.SByte.Parse(s)
                        : elementType == typeof(System.UInt16)
                            ? System.UInt16.Parse(s)
                        : elementType == typeof(System.Int16)
                            ? System.Int16.Parse(s)
                        : elementType == typeof(System.UInt32)
                            ? System.UInt32.Parse(s)
                        : elementType == typeof(System.UInt64)
                            ? System.UInt64.Parse(s)
                        : elementType == typeof(System.Single)
                            ? System.Single.Parse(s, System.Globalization.CultureInfo.InvariantCulture)
                        : elementType == typeof(System.Double)
                            ? System.Double.Parse(s, System.Globalization.CultureInfo.InvariantCulture)
                        : elementType == typeof(System.Decimal)
                            ? System.Decimal.Parse(s, System.Globalization.CultureInfo.InvariantCulture)
                        : elementType == typeof(System.Boolean)
                            ? System.Boolean.Parse(s)
                        : elementType == typeof(System.DateTime)
                            ? System.DateTime.Parse(s, null, System.Globalization.DateTimeStyles.RoundtripKind)
                        : elementType == typeof(System.TimeSpan)
                            ? System.TimeSpan.Parse(s)
                        : elementType == typeof(System.Guid)
                            ? System.Guid.Parse(s)
                        : System.Convert.ChangeType(s, elementType, System.Globalization.CultureInfo.InvariantCulture);

                list.Add(v);
            }

            return list;
        }

        // Handle Enums of any underlying type with cached reflection
        if (property.PropertyType.IsEnum)
        {
            // Get or create the generic method for this enum type
            System.Reflection.MethodInfo method = _enumGetterCache.GetOrAdd(
                property.PropertyType,
                enumType =>
                {
                    System.Reflection.MethodInfo? baseMethod = typeof(IniConfig).GetMethod(
                        nameof(IniConfig.GetEnum),
                        System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);

                    return baseMethod == null
                        ? throw new System.InvalidOperationException(
                            $"Could not find GetEnum method on {nameof(IniConfig)}.")
                        : baseMethod.MakeGenericMethod(enumType);
                });

            return method.Invoke(configFile, [section, property.Name]);
        }

        return property.TypeCode switch
        {
            System.TypeCode.Char => configFile.GetChar(section, property.Name),
            System.TypeCode.Byte => configFile.GetByte(section, property.Name),
            System.TypeCode.SByte => configFile.GetSByte(section, property.Name),
            System.TypeCode.String => configFile.GetString(section, property.Name),
            System.TypeCode.Boolean => configFile.GetBool(section, property.Name),
            System.TypeCode.Decimal => configFile.GetDecimal(section, property.Name),
            System.TypeCode.Int16 => configFile.GetInt16(section, property.Name),
            System.TypeCode.UInt16 => configFile.GetUInt16(section, property.Name),
            System.TypeCode.Int32 => configFile.GetInt32(section, property.Name),
            System.TypeCode.UInt32 => configFile.GetUInt32(section, property.Name),
            System.TypeCode.Int64 => configFile.GetInt64(section, property.Name),
            System.TypeCode.UInt64 => configFile.GetUInt64(section, property.Name),
            System.TypeCode.Single => configFile.GetSingle(section, property.Name),
            System.TypeCode.Double => configFile.GetDouble(section, property.Name),
            System.TypeCode.DateTime => configFile.GetDateTime(section, property.Name),
            System.TypeCode.Object when property.PropertyType == typeof(System.Guid) => configFile.GetGuid(section, property.Name),
            System.TypeCode.Object when property.PropertyType == typeof(System.TimeSpan) => configFile.GetTimeSpan(section, property.Name),
            _ => ThrowUnsupported(property),
        };
    }

    /// <summary>
    /// Handles empty configuration values by writing defaults to the file.
    /// </summary>
    [System.Diagnostics.Contracts.Pure]
    [System.Diagnostics.DebuggerStepThrough]
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.NoInlining |
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
    private void HandleEmptyValue(IniConfig configFile, System.String section, PropertyMetadata property)
    {
        System.Object? currentValue = property.PropertyInfo.GetValue(this);
        System.Object valueToWrite = currentValue ?? "null";

        valueToWrite = property.PropertyType.IsEnum
            ? currentValue?.ToString() ?? System.Enum.GetValues(property.PropertyType).GetValue(0)!.ToString()!
            : currentValue?.ToString() ?? GetDefaultValueString(property);

        configFile.WriteValue(section, property.Name, valueToWrite);
        InstanceManager.Instance.GetExistingInstance<ILogger>()?
                                .Debug($"[FW.{nameof(ConfigurationLoader)}:Internal] " +
                                       $"default-written section={section} key={property.Name} val={valueToWrite}");
    }

    /// <summary>
    /// Gets a default value string for the specified type code.
    /// </summary>
    [System.Diagnostics.Contracts.Pure]
    [System.Diagnostics.DebuggerStepThrough]
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining |
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
    [return: System.Diagnostics.CodeAnalysis.NotNull]
    private static System.String GetDefaultValueString(PropertyMetadata propertyType)
    {
        switch (propertyType.TypeCode)
        {
            case System.TypeCode.Object:
                if (propertyType.PropertyType == typeof(System.Guid))
                {
                    return System.Guid.Empty.ToString("c", System.Globalization.CultureInfo.InvariantCulture);
                }
                if (propertyType.PropertyType == typeof(System.TimeSpan))
                {
                    return System.TimeSpan.Zero.ToString("c", System.Globalization.CultureInfo.InvariantCulture);
                }
                break;
            case System.TypeCode.Char:
            case System.TypeCode.String:
                return System.String.Empty;
            case System.TypeCode.Boolean:
                return "false";
            case System.TypeCode.SByte:
            case System.TypeCode.Byte:
            case System.TypeCode.Int16:
            case System.TypeCode.UInt16:
            case System.TypeCode.Int32:
            case System.TypeCode.UInt32:
            case System.TypeCode.Int64:
            case System.TypeCode.UInt64:
            case System.TypeCode.Single:
            case System.TypeCode.Double:
            case System.TypeCode.Decimal:
                return "0";
            case System.TypeCode.DateTime:
                return System.DateTime.UtcNow.ToString("O", System.Globalization.CultureInfo.InvariantCulture);
        }
        return System.String.Empty;
    }

    [System.Diagnostics.StackTraceHidden]
    [System.Diagnostics.DebuggerStepThrough]
    [System.Diagnostics.CodeAnalysis.DoesNotReturn]
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    [return: System.Diagnostics.CodeAnalysis.NotNull]
    private static System.Object ThrowUnsupported(PropertyMetadata property)
    {
        InstanceManager.Instance.GetExistingInstance<ILogger>()?
                                .Error($"[FW.{nameof(ConfigurationLoader)}:Internal] " +
                                       $"unsupported-type type={property.PropertyType.Name} key={property.Name}");

        throw new System.NotSupportedException($"Value type {property.PropertyType.Name} is not supported for configuration files.");
    }
}
