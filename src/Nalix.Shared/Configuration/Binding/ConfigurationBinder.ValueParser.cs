using Nalix.Shared.Internal;
using System;
using System.Runtime.CompilerServices;

namespace Nalix.Shared.Configuration.Binding;

public partial class ConfigurationBinder
{
    /// <summary>
    /// Gets the configuration value for a property using the appropriate method.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static object? GetConfigValue(ConfiguredIniFile configFile, string section, PropertyMetadata property)
        => property.TypeCode switch
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
            _ => throw new NotSupportedException(
                $"Value type {property.PropertyType.Name} is not supported for configuration files."),
        };


    /// <summary>
    /// Handles empty configuration values by writing defaults to the file.
    /// </summary>
    private void HandleEmptyValue(ConfiguredIniFile configFile, string section, PropertyMetadata property)
    {
        object? currentValue = property.PropertyInfo.GetValue(this);
        string valueToWrite = currentValue?.ToString() ?? GetDefaultValueString(property.TypeCode);

        configFile.WriteValue(section, property.Name, valueToWrite);
        _logger?.Warn($"Attribute value {property.Name} is empty, writing default to the file");
    }

    /// <summary>
    /// Gets a default value string for the specified type code.
    /// </summary>
    private static string GetDefaultValueString(TypeCode typeCode)
        => typeCode switch
        {
            TypeCode.Char => string.Empty,
            TypeCode.String => string.Empty,
            TypeCode.Boolean => "false",
            TypeCode.Byte => "0",
            TypeCode.SByte => "0",
            TypeCode.Decimal => "0",
            TypeCode.Int16 => "0",
            TypeCode.UInt16 => "0",
            TypeCode.Int32 => "0",
            TypeCode.UInt32 => "0",
            TypeCode.Int64 => "0",
            TypeCode.UInt64 => "0",
            TypeCode.Single => "0",
            TypeCode.Double => "0",
            TypeCode.DateTime => DateTime.UtcNow.ToString("O"),
            _ => string.Empty,
        };
}
