using Nalix.Common.Logging;
using Nalix.Shared.Configuration.Metadata;
using Nalix.Shared.Internal;
using System;
using System.Collections.Concurrent;
using System.Threading;

namespace Nalix.Shared.Configuration.Binding;

/// <summary>
/// Provides high-performance access to configuration values by binding them to properties.
/// This class uses optimized reflection with caching to efficiently populate properties from an INI configuration file.
/// </summary>
/// <remarks>
/// Derived classes should have the suffix "Config" in their name (e.g., FooConfig).
/// The section and key names in the INI file are derived from the class and property names.
/// </remarks>
public abstract partial class ConfigurationBinder
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
        ConfigurationMetadata metadata = GetOrCreateMetadata(type);

        // Copy each property value to the clone
        foreach (PropertyMetadata propertyInfo in metadata.BindableProperties)
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
        ConfigurationMetadata metadata = GetOrCreateMetadata(type);

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
                if (value == null || value is string strValue && string.IsNullOrEmpty(strValue))
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

    #endregion
}
