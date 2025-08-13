using Nalix.Common.Logging;
using Nalix.Shared.Configuration.Internal;

namespace Nalix.Shared.Configuration.Binding;

/// <summary>
/// Provides high-performance access to configuration values by binding them to properties.
/// This class uses optimized reflection with caching to efficiently populate properties from an INI configuration file.
/// </summary>
/// <remarks>
/// Derived classes should have the suffix "Config" in their name (e.g., FooConfig).
/// The section and key names in the INI file are derived from the class and property names.
/// </remarks>
[System.Diagnostics.DebuggerDisplay("{GetType().Name,nq} (Initialized = {IsInitialized})")]
public abstract partial class ConfigurationLoader
{
    #region Fields

    private static readonly System.Collections.Concurrent.ConcurrentDictionary<
        System.Type, System.String> _sectionNameCache;

    private static readonly System.Collections.Concurrent.ConcurrentDictionary<
        System.Type, ConfigurationMetadata> _metadataCache;

    private static readonly System.String[] _suffixesToTrim =
    [
        "Configuration",
        "Settings",
        "Options",
        "Configs",
        "Config"
    ];

    private readonly ILogger? _logger;

    private System.Int32 _isInitialized; // Flag to track initialization status

    #endregion Fields

    #region Contructor

    static ConfigurationLoader()
    {
        _metadataCache = new();
        _sectionNameCache = new();
    }

    #endregion Contructor

    #region Properties

    /// <summary>
    /// Gets a value indicating whether this instance has been initialized.
    /// </summary>
    public System.Boolean IsInitialized => System.Threading.Volatile.Read(ref _isInitialized) == 1;

    /// <summary>
    /// Gets the time when this configuration was last initialized.
    /// </summary>
    public System.DateTime LastInitializationTime { get; private set; }

    #endregion Properties

    #region Constructor

    /// <summary>
    /// Derived classes should have the suffix "Config" in their name (e.g., FooConfig).
    /// The section and key names in the INI file are derived from the class and property names.
    /// </summary>
    public ConfigurationLoader()
    {
    }

    /// <summary>
    /// Derived classes should have the suffix "Config" in their name (e.g., FooConfig).
    /// The section and key names in the INI file are derived from the class and property names.
    /// </summary>
    /// <param name="logger">The logger to log events and errors.</param>
    public ConfigurationLoader(ILogger logger) => _logger = logger;

    #endregion Constructor

    #region Public Methods

    /// <summary>
    /// Creates a shallow clone of this configuration instance.
    /// </summary>
    /// <returns>A new instance with the same property values.</returns>
    [System.Diagnostics.Contracts.Pure]
    [return: System.Diagnostics.CodeAnalysis.NotNull]
    public T Clone<T>() where T : ConfigurationLoader, new()
    {
        T clone = new();
        System.Type type = GetType();

        // Get the configuration metadata
        ConfigurationMetadata metadata = GetOrCreateMetadata(type);

        // Copy each property value to the clone
        foreach (PropertyMetadata propertyInfo in metadata.BindableProperties)
        {
            System.Object? value = propertyInfo.PropertyInfo.GetValue(this);
            propertyInfo.PropertyInfo.SetValue(clone, value);
        }

        // Mark as initialized
        _ = System.Threading.Interlocked.Exchange(ref clone._isInitialized, _isInitialized);
        clone.LastInitializationTime = LastInitializationTime;

        return clone;
    }

    #endregion Public Methods

    #region Private Methods

    /// <summary>
    /// Initializes an instance of <see cref="ConfigurationLoader"/> from the provided <see cref="IniConfig"/>
    /// using optimized reflection with caching to set property values based on the configuration file.
    /// </summary>
    /// <param name="configFile">The INI configuration file to load values from.</param>
    [System.Diagnostics.CodeAnalysis.MemberNotNull(nameof(_isInitialized), nameof(LastInitializationTime))]
    internal void Initialize(IniConfig configFile)
    {
        System.ArgumentNullException.ThrowIfNull(configFile);

        System.Type type = GetType();

        // Get or create configuration metadata for this type
        ConfigurationMetadata metadata = GetOrCreateMetadata(type);

        // Get the section name from cache
        System.String section = GetSectionName(type);

        // Process each bindable property
        foreach (PropertyMetadata propertyInfo in metadata.BindableProperties)
        {
            try
            {
                // Get the configuration value using the appropriate method
                System.Object? value = GetConfigValue(configFile, section, propertyInfo);

                // Handle missing or empty configuration values
                if (value == null ||
                   (value is System.String strValue && System.String.IsNullOrEmpty(strValue)))
                {
                    this.HandleEmptyValue(configFile, section, propertyInfo);
                    continue;
                }

                // Assign the value to the property using the cached setter
                propertyInfo.SetValue(this, value);
            }
            catch (System.Exception ex)
            {
                _logger?.Warn($"Error setting value for {propertyInfo.Name}: {ex.Message}");
            }
        }

        // Mark as initialized and record timestamp
        _ = System.Threading.Interlocked.Exchange(ref _isInitialized, 1);
        LastInitializationTime = System.DateTime.UtcNow;
    }

    #endregion Private Methods
}