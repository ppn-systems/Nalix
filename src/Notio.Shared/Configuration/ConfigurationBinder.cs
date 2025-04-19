using Notio.Common.Logging;
using Notio.Shared.Configuration.Attributes;
using Notio.Shared.Configuration.Metadata;
using Notio.Shared.Internal;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading;

namespace Notio.Shared.Configuration;

/// <summary>
/// Provides high-performance access to configuration values by binding them to properties.
/// This class uses optimized reflection with caching to efficiently populate properties from an INI configuration file.
/// </summary>
/// <remarks>
/// Derived classes should have the suffix "Config" in their name (e.g., FooConfig).
/// The section and key names in the INI file are derived from the class and property names.
/// </remarks>
public abstract class ConfigurationBinder
{
    #region Fields

    private static readonly ConcurrentDictionary<Type, ConfigurationMetadata> _metadataCache = new();
    private static readonly ConcurrentDictionary<Type, string> _sectionNameCache = new();
    private static readonly string[] _suffixesToTrim =
    [
        "Configuration", "Settings", "Options", "Configs", "Config"
    ];

    private readonly ILogger? _logger;

    private int _isInitialized; // Flag to track initialization status
    private DateTime _lastInitializationTime; // Track the last initialization time

    #endregion

    #region Properties

    /// <summary>
    /// Gets a value indicating whether this instance has been initialized.
    /// </summary>
    public bool IsInitialized => Volatile.Read(ref _isInitialized) == 1;

    /// <summary>
    /// Gets the time when this configuration was last initialized.
    /// </summary>
    public DateTime LastInitializationTime => _lastInitializationTime;

    #endregion

    #region Constructor

    /// <summary>
    /// Derived classes should have the suffix "Config" in their name (e.g., FooConfig).
    /// The section and key names in the INI file are derived from the class and property names.
    /// </summary>
    public ConfigurationBinder()
    {
    }

    /// <summary>
    /// Derived classes should have the suffix "Config" in their name (e.g., FooConfig).
    /// The section and key names in the INI file are derived from the class and property names.
    /// </summary>
    /// <param name="logger">The logger to log events and errors.</param>
    public ConfigurationBinder(ILogger logger) => _logger = logger;

    #endregion

    #region Public Methods

    /// <summary>
    /// Creates a shallow clone of this configuration instance.
    /// </summary>
    /// <returns>A new instance with the same property values.</returns>
    public T Clone<T>() where T : ConfigurationBinder, new()
    {
        T clone = new();
        Type type = GetType();

        // Get the configuration metadata
        var metadata = GetOrCreateMetadata(type);

        // Copy each property value to the clone
        foreach (var propertyInfo in metadata.BindableProperties)
        {
            object? value = propertyInfo.PropertyInfo.GetValue(this);
            propertyInfo.PropertyInfo.SetValue(clone, value);
        }

        // Mark as initialized
        Interlocked.Exchange(ref clone._isInitialized, _isInitialized);
        clone._lastInitializationTime = _lastInitializationTime;

        return clone;
    }

    #endregion

    #region Private Methods

    /// <summary>
    /// Initializes an instance of <see cref="ConfigurationBinder"/> from the provided <see cref="ConfiguredIniFile"/>
    /// using optimized reflection with caching to set property values based on the configuration file.
    /// </summary>
    /// <param name="configFile">The INI configuration file to load values from.</param>
    internal void Initialize(ConfiguredIniFile configFile)
    {
        ArgumentNullException.ThrowIfNull(configFile);

        Type type = GetType();

        // Get or create configuration metadata for this type
        var metadata = GetOrCreateMetadata(type);

        // Get the section name from cache
        string section = GetSectionName(type);

        // Process each bindable property
        foreach (var propertyInfo in metadata.BindableProperties)
        {
            try
            {
                // Get the configuration value using the appropriate method
                object? value = GetConfigValue(configFile, section, propertyInfo);

                // Handle missing or empty configuration values
                if (value == null || (value is string strValue && string.IsNullOrEmpty(strValue)))
                {
                    HandleEmptyValue(configFile, section, propertyInfo);
                    continue;
                }

                // Assign the value to the property using the cached setter
                propertyInfo.SetValue(this, value);
            }
            catch (Exception ex)
            {
                _logger?.Warn($"Error setting value for {propertyInfo.Name}: {ex.Message}");
            }
        }

        // Mark as initialized and record timestamp
        Interlocked.Exchange(ref _isInitialized, 1);
        _lastInitializationTime = DateTime.UtcNow;
    }

    /// <summary>
    /// Gets the configuration metadata for a type, creating it if it doesn't exist.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static ConfigurationMetadata GetOrCreateMetadata(Type type)
        => _metadataCache.GetOrAdd(type, CreateConfigurationMetadata);

    /// <summary>
    /// Creates configuration metadata for a type.
    /// </summary>
    private static ConfigurationMetadata CreateConfigurationMetadata(Type type)
    {
        var bindableProperties = new List<PropertyMetadata>();

        foreach (var property in type.GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            // Skip properties with the ConfiguredIgnore attribute
            if (property.IsDefined(typeof(ConfiguredIgnoreAttribute)))
                continue;

            // Skip properties that can't be written to
            if (!property.CanWrite)
                continue;

            // Create the property metadata
            var propertyMetadata = new PropertyMetadata
            {
                PropertyInfo = property,
                Name = property.Name,
                PropertyType = property.PropertyType,
                TypeCode = Type.GetTypeCode(property.PropertyType)
            };

            bindableProperties.Add(propertyMetadata);
        }

        return new ConfigurationMetadata
        {
            ConfigurationType = type,
            BindableProperties = [.. bindableProperties]
        };
    }

    /// <summary>
    /// Gets the section name for a configuration type, with caching for performance.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static string GetSectionName(Type type)
        => _sectionNameCache.GetOrAdd(type, t =>
        {
            string section = t.Name;

            foreach (string suffix in _suffixesToTrim.OrderByDescending(s => s.Length))
            {
                if (section.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
                {
                    section = section[..^suffix.Length];
                    break;
                }
            }

            return Capitalize(section);
        });

    /// <summary>
    /// Capitalizes the first letter of a string.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static string Capitalize(string input)
        => string.IsNullOrEmpty(input) ? input : char.ToUpperInvariant(input[0]) + input[1..];

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

    #endregion
}
