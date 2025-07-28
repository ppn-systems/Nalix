// Copyright (c) 2025-2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using Nalix.Common.Diagnostics.Abstractions;
using Nalix.Framework.Configuration.Internal;
using Nalix.Framework.Injection;

namespace Nalix.Framework.Configuration.Binding;

/// <summary>
/// Provides high-performance access to configuration values by binding them to properties.
/// This class uses optimized reflection with caching to efficiently populate properties from an INI configuration file.
/// </summary>
/// <remarks>
/// Derived classes should have the suffix "Config" in their name (e.g., FooConfig).
/// Supported data types: int, long, short, byte, double, float, decimal, bool, char, string, DateTime, TimeSpan, Guid, and all Enum types.
/// The section and key names in the INI file are derived from the class and property names.
/// Apply <see cref="Nalix.Common.Shared.Attributes.IniCommentAttribute"/> to the class or its properties
/// to generate human-readable comments in the INI file on first run.
/// </remarks>
[System.Runtime.CompilerServices.SkipLocalsInit]
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
        "Config",
        "Option",
        "Configs",
        "Options",
        "Setting",
        "Settings",
        "Configuration",
        "Configurations",
    ];

    private System.Int32 _isInitialized;

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
    public System.Boolean IsInitialized
        => System.Threading.Volatile.Read(ref _isInitialized) == 1;

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
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Style", "IDE0290:Use primary constructor", Justification = "<Pending>")]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Roslynator", "RCS1160:Abstract type should not have public constructors", Justification = "<Pending>")]
    public ConfigurationLoader()
    {
    }

    #endregion Constructor

    #region Public Methods

    /// <summary>
    /// Creates a shallow clone of this configuration instance.
    /// </summary>
    [System.Diagnostics.Contracts.Pure]
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    [return: System.Diagnostics.CodeAnalysis.NotNull]
    public T Clone<T>() where T : ConfigurationLoader, new()
    {
        T clone = new();
        System.Type type = GetType();

        ConfigurationMetadata metadata = GetOrCreateMetadata(type);

        foreach (PropertyMetadata propertyInfo in metadata.BindableProperties)
        {
            System.Object? value = propertyInfo.PropertyInfo.GetValue(this);
            propertyInfo.PropertyInfo.SetValue(clone, value);
        }

        _ = System.Threading.Interlocked.Exchange(ref clone._isInitialized, _isInitialized);
        clone.LastInitializationTime = LastInitializationTime;

        return clone;
    }

    #endregion Public Methods

    #region Private Methods

    /// <summary>
    /// Initializes an instance of <see cref="ConfigurationLoader"/> from the provided <see cref="IniConfig"/>.
    /// Section and property comments from <see cref="Nalix.Common.Shared.Attributes.IniCommentAttribute"/>
    /// are written to the file the first time a key is generated.
    /// </summary>
    /// <param name="configFile">The INI configuration file to load values from.</param>
    /// <exception cref="System.ArgumentNullException">Thrown when configFile is null.</exception>
    [System.Diagnostics.CodeAnalysis.MemberNotNull(nameof(_isInitialized), nameof(LastInitializationTime))]
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    internal void Initialize(IniConfig configFile)
    {
        System.ArgumentNullException.ThrowIfNull(configFile, nameof(configFile));

        System.Type type = GetType();
        ConfigurationMetadata metadata = GetOrCreateMetadata(type);
        System.String section = GetSectionName(type);

        InstanceManager.Instance.GetExistingInstance<ILogger>()?
                                .Meta($"[FW.{nameof(ConfigurationLoader)}:Internal] init type={type.Name} section={section}");

        // Write the section-level comment once, before the first property is processed.
        // IniConfig.WriteComment is a no-op when the section already exists, so this
        // is safe to call on every initialization — it only fires on first-time generation.
        configFile.WriteComment(section, key: null, comment: metadata.SectionComment);

        foreach (PropertyMetadata propertyInfo in metadata.BindableProperties)
        {
            try
            {
                System.Object? value = GetConfigValue(configFile, section, propertyInfo);

                if (value == null ||
                   (value is System.String strValue && System.String.IsNullOrEmpty(strValue)))
                {
                    InstanceManager.Instance.GetExistingInstance<ILogger>()?
                                            .Trace($"[FW.{nameof(ConfigurationLoader)}:Internal] missing-value section={section} key={propertyInfo.Name}");

                    // HandleEmptyValue writes the comment + default value for new keys
                    this.HandleEmptyValue(configFile, section, propertyInfo);
                    continue;
                }

                propertyInfo.SetValue(this, value);
            }
            catch (System.ArgumentException ex)
            {
                InstanceManager.Instance.GetExistingInstance<ILogger>()?
                                        .Error($"[FW.{nameof(ConfigurationLoader)}:Internal] invalid-argument section={section} key={propertyInfo.Name}", ex);
            }
            catch (System.InvalidOperationException ex)
            {
                InstanceManager.Instance.GetExistingInstance<ILogger>()?
                                        .Error($"[FW.{nameof(ConfigurationLoader)}:Internal] invalid-operation section={section} key={propertyInfo.Name}", ex);
            }
            catch (System.Exception ex)
            {
                InstanceManager.Instance.GetExistingInstance<ILogger>()?
                                        .Warn($"[FW.{nameof(ConfigurationLoader)}:Internal] set-error section={section} key={propertyInfo.Name} type={ex.GetType().Name}, ex={ex.Message}");
            }
        }

        _ = System.Threading.Interlocked.Exchange(ref _isInitialized, 1);
        LastInitializationTime = System.DateTime.UtcNow;
    }

    #endregion Private Methods
}