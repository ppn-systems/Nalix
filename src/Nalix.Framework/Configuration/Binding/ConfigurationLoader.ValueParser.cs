// Copyright (c) 2025 PPN Corporation. All rights reserved.

using Nalix.Common.Logging;
using Nalix.Framework.Configuration.Internal;
using Nalix.Framework.Injection;

namespace Nalix.Framework.Configuration.Binding;

public partial class ConfigurationLoader
{
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
        // Handle Enums of any underlying type
        if (property.PropertyType.IsEnum)
        {
            // Use reflection to call generic method
            var method = typeof(IniConfig)
                .GetMethod(nameof(IniConfig.GetEnum))
                ?.MakeGenericMethod(property.PropertyType);

            return method?.Invoke(configFile, [section, property.Name]);
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
        System.String valueToWrite;

        if (property.PropertyType.IsEnum)
        {
            valueToWrite = currentValue?.ToString() ?? System.Enum.GetValues(property.PropertyType).GetValue(0)!.ToString()!;
        }
        else
        {
            valueToWrite = currentValue?.ToString() ?? GetDefaultValueString(property);
        }

        configFile.WriteValue(section, property.Name, valueToWrite);
        InstanceManager.Instance.GetExistingInstance<ILogger>()?
                                .Debug($"[{nameof(ConfigurationLoader)}:{nameof(HandleEmptyValue)}] " +
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
        => propertyType.TypeCode switch
        {
            System.TypeCode.Byte => "0",
            System.TypeCode.SByte => "0",
            System.TypeCode.Decimal => "0",
            System.TypeCode.Int16 => "0",
            System.TypeCode.UInt16 => "0",
            System.TypeCode.Int32 => "0",
            System.TypeCode.UInt32 => "0",
            System.TypeCode.Int64 => "0",
            System.TypeCode.UInt64 => "0",
            System.TypeCode.Single => "0",
            System.TypeCode.Double => "0",
            System.TypeCode.Boolean => "false",
            System.TypeCode.Char => System.String.Empty,
            System.TypeCode.String => System.String.Empty,
            System.TypeCode.DateTime => System.DateTime.UtcNow.ToString("O", System.Globalization.CultureInfo.InvariantCulture),
            System.TypeCode.Object when propertyType.PropertyType == typeof(System.TimeSpan) =>
            System.TimeSpan.Zero.ToString("c", System.Globalization.CultureInfo.InvariantCulture),
            _ => System.String.Empty,
        };

    [System.Diagnostics.StackTraceHidden]
    [System.Diagnostics.DebuggerStepThrough]
    [System.Diagnostics.CodeAnalysis.DoesNotReturn]
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    [return: System.Diagnostics.CodeAnalysis.NotNull]
    private static System.Object ThrowUnsupported(PropertyMetadata property)
    {
        InstanceManager.Instance.GetExistingInstance<ILogger>()?
                                .Error($"[{nameof(ConfigurationLoader)}:{nameof(ThrowUnsupported)}] " +
                                       $"unsupported-type type={property.PropertyType.Name} key={property.Name}");

        throw new System.NotSupportedException(
            $"Value type {property.PropertyType.Name} is not supported for configuration files.");
    }
}
