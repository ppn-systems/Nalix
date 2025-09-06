// Copyright (c) 2025 PPN Corporation. All rights reserved.

using Nalix.Common.Logging.Abstractions;
using Nalix.Shared.Configuration.Internal;
using Nalix.Shared.Injection;

namespace Nalix.Shared.Configuration.Binding;

public partial class ConfigurationLoader
{
    /// <summary>
    /// Gets the configuration value for a property using the appropriate method.
    /// </summary>
    [System.Diagnostics.Contracts.Pure]
    [System.Diagnostics.DebuggerStepThrough]
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining |
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
    private static System.Object? GetConfigValue(
        IniConfig configFile,
        System.String section, PropertyMetadata property)
        => property.TypeCode switch
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
            _ => ThrowUnsupported(property),
        };

    /// <summary>
    /// Handles empty configuration values by writing defaults to the file.
    /// </summary>
    [System.Diagnostics.Contracts.Pure]
    [System.Diagnostics.DebuggerStepThrough]
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining |
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
    private void HandleEmptyValue(
        IniConfig configFile,
        System.String section,
        PropertyMetadata property)
    {
        System.Object? currentValue = property.PropertyInfo.GetValue(this);
        System.String valueToWrite = currentValue?.ToString() ?? GetDefaultValueString(property.TypeCode);

        configFile.WriteValue(section, property.Name, valueToWrite);
        InstanceManager.Instance.GetExistingInstance<ILogger>()?
                                .Debug($"[{nameof(ConfigurationLoader)}] default-written section={section} key={property.Name} val={valueToWrite}");
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
    private static System.String GetDefaultValueString(System.TypeCode typeCode)
        => typeCode switch
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
            System.TypeCode.DateTime => System.DateTime.UtcNow.ToString("O"),
            _ => System.String.Empty,
        };

    [System.Diagnostics.DebuggerStepThrough]
    [System.Runtime.CompilerServices.MethodImpl(
    System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining |
    System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
    private static System.Object ThrowUnsupported(PropertyMetadata property)
    {
        InstanceManager.Instance.GetExistingInstance<ILogger>()?
                                .Error($"[{nameof(ConfigurationLoader)}] " +
                                       $"unsupported-type type={property.PropertyType.Name} key={property.Name}");

        throw new System.NotSupportedException(
            $"Value type {property.PropertyType.Name} is not supported for configuration files.");
    }
}
