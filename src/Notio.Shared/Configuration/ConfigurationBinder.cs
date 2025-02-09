using Notio.Common.Logging;
using System;
using System.Reflection;

namespace Notio.Shared.Configuration;

/// <summary>
/// Provides access to configuration values by binding them to properties.
/// This class uses reflection to populate properties from an INI configuration file.
/// </summary>
/// <remarks>
/// Derived classes should have the suffix "Configured" in their name (e.g., FooConfig).
/// The section and key names in the INI file are derived from the class and property names.
/// </remarks>
public abstract class ConfiguredBinder
{
    /// <summary>
    /// Initializes an instance of <see cref="ConfiguredBinder"/> from the provided <see cref="ConfiguredIniFile"/>
    /// using reflection to set property values based on the configuration file.
    /// </summary>
    /// <param name="configFile">The INI configuration file to load values from.</param>
    internal void Initialize(ConfiguredIniFile configFile)
    {
        Type type = GetType();
        string section = GetSectionName(type);

        foreach (var property in type.GetProperties())
        {
            // Skip properties with the ConfiguredIgnore attribute
            if (property.IsDefined(typeof(ConfiguredIgnoreAttribute)))
                continue;

            // Get the configuration value for the property
            object? value = GetConfigValue(configFile, section, property);

            // Handle missing or empty configuration values
            if (value == null || (value is string strValue && string.IsNullOrEmpty(strValue)))
            {
                HandleEmptyValue(configFile, section, property);
                continue;
            }

            // Try to assign the value to the property
            try
            {
                AssignValueToProperty(property, value);
            }
            catch (Exception ex)
            {
                NotioDebug.Warn($"Error setting value for {property.Name}: {ex.Message}");
            }
        }
    }

    private static string GetSectionName(Type type)
    {
        string section = type.Name;
        if (section.EndsWith("Configured", StringComparison.OrdinalIgnoreCase))
            section = section[..^6];
        return section;
    }

    private static object? GetConfigValue(ConfiguredIniFile configFile, string section, PropertyInfo property)
    {
        return Type.GetTypeCode(property.PropertyType) switch
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
            _ => throw new NotImplementedException($"Value type {property.PropertyType} is not supported for configuration files."),
        };
    }

    private void HandleEmptyValue(ConfiguredIniFile configFile, string section, PropertyInfo property)
    {
        // Attempt to write a default or existing value to the configuration file if empty
        configFile.WriteValue(section, property.Name, property.GetValue(this)?.ToString() ?? string.Empty);
        NotioDebug.Warn($"Attribute value {property.Name} is empty, writing to the file");
    }

    private void AssignValueToProperty(PropertyInfo property, object value)
    {
        // Ensure that the value is compatible with the property type before setting it
        if (property.PropertyType.IsAssignableFrom(value?.GetType()))
        {
            property.SetValue(this, value);
        }
        else
        {
            NotioDebug.Warn(
                $"Type mismatch for property {property.Name}: " +
                $"Expected {property.PropertyType}, but got {value?.GetType()}");
        }
    }
}