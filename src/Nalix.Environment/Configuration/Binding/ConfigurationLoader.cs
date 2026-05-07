// Copyright (c) 2025-2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Diagnostics.Contracts;
using System.Runtime.CompilerServices;
using System.Threading;
using Nalix.Environment.Configuration.Internal;

namespace Nalix.Environment.Configuration.Binding;

/// <summary>
/// Provides high-performance access to configuration values by binding them to
/// properties.
/// Source generators emit the binding logic at compile time for full AOT compatibility.
/// </summary>
/// <remarks>
/// Derived classes should have the suffix "Config" in their name (e.g., FooConfig).
/// Supported data types: int, long, short, byte, double, float, decimal, bool, char, string, DateTime, TimeSpan, Guid, and all Enum types.
/// The section and key names in the INI file are derived from the class and property names.
/// Apply <see cref="Abstractions.IniCommentAttribute"/> to the class or its properties
/// to generate human-readable comments in the INI file on first run.
/// </remarks>
[SkipLocalsInit]
[DebuggerDisplay("{GetType().Name,nq} (Initialized = {IsInitialized})")]
public abstract partial class ConfigurationLoader
{
    #region Fields

    private static readonly ConcurrentDictionary<Type, string> s_sectionNameCache;

    private static readonly string[] s_suffixesToTrim =
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

    private int _isInitialized;

    #endregion Fields

    #region Contructor

    static ConfigurationLoader() => s_sectionNameCache = new();

    #endregion Contructor

    #region Properties

    /// <summary>
    /// Gets a value indicating whether this instance has been initialized.
    /// </summary>
    public bool IsInitialized
        => Volatile.Read(ref _isInitialized) == 1;

    /// <summary>
    /// Gets the time when this configuration was last initialized.
    /// </summary>
    public DateTime LastInitializationTime { get; private set; }

    #endregion Properties

    #region Constructor

    /// <summary>
    /// Derived classes should have the suffix "Config" in their name (e.g., FooConfig).
    /// The section and key names in the INI file are derived from the class and property names.
    /// </summary>
    public ConfigurationLoader()
    {
    }

    #endregion Constructor

    #region Public Methods

    /// <summary>
    /// Creates a shallow clone of this configuration instance.
    /// </summary>
    [Pure]
    [MethodImpl(MethodImplOptions.NoInlining)]
    public T Clone<T>() where T : ConfigurationLoader, new()
    {
        T clone = new();
        this.CopyPropertiesTo(clone);
        _ = Interlocked.Exchange(ref clone._isInitialized, _isInitialized);
        clone.LastInitializationTime = this.LastInitializationTime;
        return clone;
    }

    #endregion Public Methods

    #region Protected Methods

    /// <summary>
    /// Copies property values from this instance to another.
    /// Overridden by source generators for AOT compatibility.
    /// </summary>
    protected virtual void CopyPropertiesTo(ConfigurationLoader other)
    {
        // Source generator will override this
    }

    /// <summary>
    /// Binds configuration values from the INI file.
    /// Overridden by source generators for AOT compatibility.
    /// </summary>
    protected virtual void BindProperties(IniConfig configFile, string section)
    {
        // Source generator will override this
    }

    #endregion Protected Methods

    #region Private Methods

    /// <summary>
    /// Initializes an instance of <see cref="ConfigurationLoader"/> from the provided <see cref="IniConfig"/>.
    /// </summary>
    /// <param name="configFile">The INI configuration file to load values from.</param>
    [MemberNotNull(nameof(_isInitialized), nameof(LastInitializationTime))]
    [MethodImpl(MethodImplOptions.NoInlining)]
    internal void Initialize(IniConfig configFile)
    {
        ArgumentNullException.ThrowIfNull(configFile, nameof(configFile));

        string section = GetSectionName(this.GetType());
        this.BindProperties(configFile, section);

        _ = Interlocked.Exchange(ref _isInitialized, 1);
        this.LastInitializationTime = DateTime.UtcNow;
    }

    #endregion Private Methods
}
