// Copyright (c) 2025 PPN Corporation. All rights reserved.

#if DEBUG
[assembly: System.Runtime.CompilerServices.InternalsVisibleTo("Nalix.Framework.Configuration.Tests")]
[assembly: System.Runtime.CompilerServices.InternalsVisibleTo("Nalix.Framework.Configuration.Benchmarks")]
#endif

namespace Nalix.Framework.Configuration.Internal;

/// <summary>
/// A high-performance wrapper class for reading and writing INI files.
/// </summary>
[System.Diagnostics.DebuggerNonUserCode]
[System.Runtime.CompilerServices.SkipLocalsInit]
[System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverage]
[System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never)]
[System.Diagnostics.DebuggerDisplay("Path = {_path}, Sections = {_iniData.Count}, Dirty = {_isDirty}")]
internal sealed class IniConfig
{
    #region Constants

    // LZ4CompressionConstants for better readability and performance
    private const System.Char SectionStart = '[';

    private const System.Char SectionEnd = ']';
    private const System.Char KeyValueSeparator = '=';
    private const System.Char CommentChar = ';';
    private const System.Char CacheKeySeparator = ':';

    // Standard buffer sizes
    private const System.Int32 DefaultBufferSize = 4096;

    #endregion Constants

    #region Fields

    // Thread synchronization for file operations
    private readonly System.Threading.ReaderWriterLockSlim _fileLock;

    [System.Diagnostics.CodeAnalysis.NotNull]
    private readonly System.String _path;
    private readonly System.Collections.Generic.Dictionary<System.String,
                     System.Collections.Generic.Dictionary<System.String, System.String>> _iniData;

    // Caches for frequently accessed values
    private readonly System.Collections.Generic.Dictionary<System.String, System.Object> _valueCache;

    // Track if the file has been modified
    private System.Boolean _isDirty;

    private System.DateTime _lastFileReadTime;

    #endregion Fields

    #region Properties

    /// <summary>
    /// Checks whether the file exists at the provided path.
    /// </summary>
    public System.Boolean ExistsFile => System.IO.File.Exists(_path);

    #endregion Properties

    #region Constructor

    /// <summary>
    /// Initializes a new instance of the <see cref="IniConfig"/> class for the specified path.
    /// </summary>
    /// <param name="path">The path to the INI file.</param>
    /// <exception cref="System.ArgumentNullException">Thrown when path is null.</exception>
    /// <exception cref="System.ArgumentException">Thrown when path is invalid.</exception>
    /// <exception cref="System.Security.SecurityException">Thrown when path contains path traversal attempts.</exception>
    public IniConfig(System.String path)
    {
        if (path == null)
        {
            throw new System.ArgumentNullException(nameof(path), "Configuration file path cannot be null.");
        }

        if (System.String.IsNullOrWhiteSpace(path))
        {
            throw new System.ArgumentException("Configuration file path cannot be empty or whitespace.", nameof(path));
        }

        // Validate path for security - prevent path traversal
        try
        {
            _path = System.IO.Path.GetFullPath(path);
        }
        catch (System.ArgumentException ex)
        {
            throw new System.ArgumentException($"Invalid configuration file path: {path}", nameof(path), ex);
        }
        catch (System.Security.SecurityException ex)
        {
            throw new System.Security.SecurityException($"Security error accessing path: {path}", ex);
        }
        catch (System.NotSupportedException ex)
        {
            throw new System.ArgumentException($"Unsupported path format: {path}", nameof(path), ex);
        }

        // Additional validation - ensure path doesn't contain invalid characters
        if (_path.IndexOfAny(System.IO.Path.GetInvalidPathChars()) >= 0)
        {
            throw new System.ArgumentException(
                $"Configuration file path contains invalid characters: {path}", nameof(path));
        }

        // Use case-insensitive keys for sections and keys
        _iniData = new(System.StringComparer.OrdinalIgnoreCase);
        _valueCache = new(System.StringComparer.OrdinalIgnoreCase);
        _fileLock = new(System.Threading.LockRecursionPolicy.NoRecursion);

        // Load the file if it exists
        if (ExistsFile)
        {
            LoadWithRetry();
        }
    }

    #endregion Constructor

    #region Public API

    /// <summary>
    /// Reloads the INI file from disk, discarding any unsaved changes.
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining |
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
    public void Reload() => Load();

    /// <summary>
    /// Writes a value to the INI file if the key does not already exist.
    /// </summary>
    /// <param name="section">The section name in the INI file.</param>
    /// <param name="key">The key name in the section.</param>
    /// <param name="value">The value to write.</param>
    /// <exception cref="System.ArgumentNullException">Thrown when section, key, or value is null.</exception>
    /// <exception cref="System.ArgumentException">Thrown when section or key contains invalid characters.</exception>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
    public void WriteValue(
        [System.Diagnostics.CodeAnalysis.NotNull] System.String section,
        [System.Diagnostics.CodeAnalysis.NotNull] System.String key,
        [System.Diagnostics.CodeAnalysis.NotNull] System.Object value)
    {
        if (key == null)
        {
            throw new System.ArgumentNullException(nameof(key), "Configuration key cannot be null.");
        }

        if (section == null)
        {
            throw new System.ArgumentNullException(nameof(section), "Configuration section cannot be null.");
        }

        if (value == null)
        {
            throw new System.ArgumentNullException(nameof(value), "Configuration value cannot be null.");
        }

        // Validate section and key don't contain special characters that would break INI format
        if (section.IndexOfAny(['\r', '\n', '[', ']']) >= 0)
        {
            throw new System.ArgumentException(
                "Section name cannot contain newline, '[', or ']' characters.", nameof(section));
        }

        if (key.IndexOfAny(['\r', '\n', '=']) >= 0)
        {
            throw new System.ArgumentException(
                "Key name cannot contain newline or '=' characters.", nameof(key));
        }

        _fileLock.EnterUpgradeableReadLock();
        try
        {
            // Check for external file changes
            CheckFileChanges();

            if (!_iniData.TryGetValue(
                section,
                out System.Collections.Generic.Dictionary<System.String, System.String>? sectionData))
            {
                _fileLock.EnterWriteLock();
                try
                {
                    sectionData = new System.Collections.Generic.Dictionary<
                        System.String, System.String>(System.StringComparer.OrdinalIgnoreCase);

                    _iniData[section] = sectionData;
                }
                finally
                {
                    _fileLock.ExitWriteLock();
                }
            }

            // Only write if the key doesn't exist
            if (!sectionData.TryGetValue(key, out System.String? existing) || System.String.IsNullOrEmpty(existing))
            {
                _fileLock.EnterWriteLock();
                try
                {
                    System.String stringValue = FormatValue(value);
                    sectionData[key] = stringValue;

                    // Dispose any cached value for this key
                    System.String cacheKey = CreateCacheKey(section, key);
                    _ = _valueCache.Remove(cacheKey);

                    _isDirty = true;
                }
                finally
                {
                    _fileLock.ExitWriteLock();
                }

                // WriteInt16 changes to the file
                WriteFile();
            }
        }
        finally
        {
            _fileLock.ExitUpgradeableReadLock();
        }
    }

    /// <summary>
    /// Gets the value for the specified key in the specified section as a string.
    /// </summary>
    /// <param name="section">The section name in the INI file.</param>
    /// <param name="key">The key name in the section.</param>
    /// <returns>The string value, or an empty string if not found.</returns>
    /// <exception cref="System.ArgumentNullException">Thrown when section or key is null.</exception>
    /// <exception cref="System.ArgumentException">Thrown when section or key is empty.</exception>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
    [return: System.Diagnostics.CodeAnalysis.NotNull]
    public System.String GetString(
        [System.Diagnostics.CodeAnalysis.NotNull] System.String section,
        [System.Diagnostics.CodeAnalysis.NotNull] System.String key)
    {
        if (key == null)
        {
            throw new System.ArgumentNullException(nameof(key), "Configuration key cannot be null.");
        }

        if (section == null)
        {
            throw new System.ArgumentNullException(nameof(section), "Configuration section cannot be null.");
        }

        if (System.String.IsNullOrWhiteSpace(key))
        {
            throw new System.ArgumentException("Configuration key cannot be empty or whitespace.", nameof(key));
        }

        if (System.String.IsNullOrWhiteSpace(section))
        {
            throw new System.ArgumentException("Configuration section cannot be empty or whitespace.", nameof(section));
        }

        // Check for file changes before reading
        CheckFileChanges();

        _fileLock.EnterReadLock();
        try
        {
            if (_iniData.TryGetValue(section,
                out System.Collections.Generic.Dictionary<System.String, System.String>? sectionData) &&
                sectionData.TryGetValue(key, out System.String? value))
            {
                if (value.Equals("null", System.StringComparison.OrdinalIgnoreCase))
                {
                    return null!;
                }

                return value;
            }

            return System.String.Empty;
        }
        finally
        {
            _fileLock.ExitReadLock();
        }
    }

    /// <summary>
    /// Gets the value for the specified key in the specified section as a character.
    /// </summary>
    /// <param name="section">The section name in the INI file.</param>
    /// <param name="key">The key name in the section.</param>
    /// <returns>The character value if the string has exactly one character; otherwise, null.</returns>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
    [return: System.Diagnostics.CodeAnalysis.MaybeNull]
    public System.Char? GetChar(
        [System.Diagnostics.CodeAnalysis.NotNull] System.String section,
        [System.Diagnostics.CodeAnalysis.NotNull] System.String key)
    {
        System.String stringValue = GetString(section, key);
        return !System.String.IsNullOrEmpty(stringValue) && stringValue.Length == 1 ? stringValue[0] : null;
    }

    /// <summary>
    /// Gets the value for the specified key in the specified section as a boolean.
    /// </summary>
    /// <param name="section">The section name in the INI file.</param>
    /// <param name="key">The key name in the section.</param>
    /// <returns>The boolean value if parsed successfully, otherwise null.</returns>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
    [return: System.Diagnostics.CodeAnalysis.MaybeNull]
    public System.Boolean? GetBool(
        [System.Diagnostics.CodeAnalysis.NotNull] System.String section,
        [System.Diagnostics.CodeAnalysis.NotNull] System.String key)
    {
        System.String cacheKey = CreateCacheKey(section, key, "bool");

        if (_valueCache.TryGetValue(cacheKey, out System.Object? cachedValue))
        {
            return (System.Boolean?)cachedValue;
        }

        System.String stringValue = GetString(section, key);
        System.Boolean? result = null;

        if (stringValue.Equals("null", System.StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }
        else if (!System.String.IsNullOrEmpty(stringValue))
        {
            // Optimize common boolean representations
            if (stringValue.Equals("true", System.StringComparison.OrdinalIgnoreCase) ||
                stringValue.Equals("1", System.StringComparison.OrdinalIgnoreCase) ||
                stringValue.Equals("yes", System.StringComparison.OrdinalIgnoreCase))
            {
                result = true;
            }
            else if (stringValue.Equals("false", System.StringComparison.OrdinalIgnoreCase) ||
                     stringValue.Equals("0", System.StringComparison.OrdinalIgnoreCase) ||
                     stringValue.Equals("no", System.StringComparison.OrdinalIgnoreCase))
            {
                result = false;
            }
            else
            {
                _ = System.Boolean.TryParse(stringValue, out System.Boolean parsedValue);
                result = parsedValue;
            }

            // Caches the result
            _valueCache[cacheKey] = result;
        }

        return result;
    }

    /// <summary>
    /// Gets the value for the specified key in the specified section as a decimal.
    /// </summary>
    /// <param name="section">The section name in the INI file.</param>
    /// <param name="key">The key name in the section.</param>
    /// <returns>The decimal value if parsed successfully, otherwise null.</returns>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
    [return: System.Diagnostics.CodeAnalysis.MaybeNull]
    public System.Decimal? GetDecimal(
        [System.Diagnostics.CodeAnalysis.NotNull] System.String section,
        [System.Diagnostics.CodeAnalysis.NotNull] System.String key)
    {
        System.String cacheKey = CreateCacheKey(section, key, "decimal");

        if (_valueCache.TryGetValue(cacheKey, out System.Object? cachedValue))
        {
            return (System.Decimal?)cachedValue;
        }

        System.String stringValue = GetString(section, key);

        if (stringValue.Equals("null", System.StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }
        else if (System.Decimal.TryParse(
            stringValue, System.Globalization.NumberStyles.Number,
            System.Globalization.CultureInfo.InvariantCulture, out System.Decimal parsedValue))
        {
            _valueCache[cacheKey] = parsedValue;
            return parsedValue;
        }

        return null;
    }

    /// <summary>
    /// Gets the value for the specified key in the specified section as a byte.
    /// </summary>
    /// <param name="section">The section name in the INI file.</param>
    /// <param name="key">The key name in the section.</param>
    /// <returns>The byte value if parsed successfully, otherwise null.</returns>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
    [return: System.Diagnostics.CodeAnalysis.MaybeNull]
    public System.Byte? GetByte(
        [System.Diagnostics.CodeAnalysis.NotNull] System.String section,
        [System.Diagnostics.CodeAnalysis.NotNull] System.String key)
    {
        System.String cacheKey = CreateCacheKey(section, key, "byte");

        if (_valueCache.TryGetValue(cacheKey, out System.Object? cachedValue))
        {
            return (System.Byte?)cachedValue;
        }

        System.String stringValue = GetString(section, key);

        if (stringValue.Equals("null", System.StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }
        else if (System.Byte.TryParse(stringValue, out System.Byte parsedValue))
        {
            _valueCache[cacheKey] = parsedValue;
            return parsedValue;
        }

        return null;
    }

    /// <summary>
    /// Gets the value for the specified key in the specified section as an sbyte.
    /// </summary>
    /// <param name="section">The section name in the INI file.</param>
    /// <param name="key">The key name in the section.</param>
    /// <returns>The sbyte value if parsed successfully, otherwise null.</returns>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
    [return: System.Diagnostics.CodeAnalysis.MaybeNull]
    public System.SByte? GetSByte(
        [System.Diagnostics.CodeAnalysis.NotNull] System.String section,
        [System.Diagnostics.CodeAnalysis.NotNull] System.String key)
    {
        System.String cacheKey = CreateCacheKey(section, key, "sbyte");

        if (_valueCache.TryGetValue(cacheKey, out System.Object? cachedValue))
        {
            return (System.SByte?)cachedValue;
        }

        System.String stringValue = GetString(section, key);

        if (stringValue.Equals("null", System.StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }
        else if (System.SByte.TryParse(stringValue, out System.SByte parsedValue))
        {
            _valueCache[cacheKey] = parsedValue;
            return parsedValue;
        }

        return null;
    }

    /// <summary>
    /// Gets the value for the specified key in the specified section as a short.
    /// </summary>
    /// <param name="section">The section name in the INI file.</param>
    /// <param name="key">The key name in the section.</param>
    /// <returns>The short value if parsed successfully, otherwise null.</returns>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
    [return: System.Diagnostics.CodeAnalysis.MaybeNull]
    public System.Int16? GetInt16(
        [System.Diagnostics.CodeAnalysis.NotNull] System.String section,
        [System.Diagnostics.CodeAnalysis.NotNull] System.String key)
    {
        System.String cacheKey = CreateCacheKey(section, key, "int16");

        if (_valueCache.TryGetValue(cacheKey, out System.Object? cachedValue))
        {
            return (System.Int16?)cachedValue;
        }

        System.String stringValue = GetString(section, key);

        if (stringValue.Equals("null", System.StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }
        else if (System.Int16.TryParse(stringValue, out System.Int16 parsedValue))
        {
            _valueCache[cacheKey] = parsedValue;
            return parsedValue;
        }

        return null;
    }

    /// <summary>
    /// Gets the value for the specified key in the specified section as an unsigned short.
    /// </summary>
    /// <param name="section">The section name in the INI file.</param>
    /// <param name="key">The key name in the section.</param>
    /// <returns>The unsigned short value if parsed successfully, otherwise null.</returns>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
    [return: System.Diagnostics.CodeAnalysis.MaybeNull]
    public System.UInt16? GetUInt16(
        [System.Diagnostics.CodeAnalysis.NotNull] System.String section,
        [System.Diagnostics.CodeAnalysis.NotNull] System.String key)
    {
        System.String cacheKey = CreateCacheKey(section, key, "uint16");

        if (_valueCache.TryGetValue(cacheKey, out System.Object? cachedValue))
        {
            return (System.UInt16?)cachedValue;
        }

        System.String stringValue = GetString(section, key);

        if (stringValue.Equals("null", System.StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }
        else if (System.UInt16.TryParse(stringValue, out System.UInt16 parsedValue))
        {
            _valueCache[cacheKey] = parsedValue;
            return parsedValue;
        }

        return null;
    }

    /// <summary>
    /// Gets the value for the specified key in the specified section as an integer.
    /// </summary>
    /// <param name="section">The section name in the INI file.</param>
    /// <param name="key">The key name in the section.</param>
    /// <returns>The integer value if parsed successfully, otherwise null.</returns>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
    [return: System.Diagnostics.CodeAnalysis.MaybeNull]
    public System.Int32? GetInt32(
        [System.Diagnostics.CodeAnalysis.NotNull] System.String section,
        [System.Diagnostics.CodeAnalysis.NotNull] System.String key)
    {
        System.String cacheKey = CreateCacheKey(section, key, "int32");

        if (_valueCache.TryGetValue(cacheKey, out System.Object? cachedValue))
        {
            return (System.Int32?)cachedValue;
        }

        System.String stringValue = GetString(section, key);

        if (stringValue.Equals("null", System.StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }
        else if (System.Int32.TryParse(stringValue, out System.Int32 parsedValue))
        {
            _valueCache[cacheKey] = parsedValue;
            return parsedValue;
        }

        return null;
    }

    /// <summary>
    /// Gets the value for the specified key in the specified section as an unsigned integer.
    /// </summary>
    /// <param name="section">The section name in the INI file.</param>
    /// <param name="key">The key name in the section.</param>
    /// <returns>The unsigned integer value if parsed successfully, otherwise null.</returns>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
    [return: System.Diagnostics.CodeAnalysis.MaybeNull]
    public System.UInt32? GetUInt32(
        [System.Diagnostics.CodeAnalysis.NotNull] System.String section,
        [System.Diagnostics.CodeAnalysis.NotNull] System.String key)
    {
        System.String cacheKey = CreateCacheKey(section, key, "uint32");

        if (_valueCache.TryGetValue(cacheKey, out System.Object? cachedValue))
        {
            return (System.UInt32?)cachedValue;
        }

        System.String stringValue = GetString(section, key);

        if (stringValue.Equals("null", System.StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }
        else if (System.UInt32.TryParse(stringValue, out System.UInt32 parsedValue))
        {
            _valueCache[cacheKey] = parsedValue;
            return parsedValue;
        }

        return null;
    }

    /// <summary>
    /// Gets the value for the specified key in the specified section as a long.
    /// </summary>
    /// <param name="section">The section name in the INI file.</param>
    /// <param name="key">The key name in the section.</param>
    /// <returns>The long value if parsed successfully, otherwise null.</returns>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
    [return: System.Diagnostics.CodeAnalysis.MaybeNull]
    public System.Int64? GetInt64(System.String section, System.String key)
    {
        System.String cacheKey = CreateCacheKey(section, key, "int64");

        if (_valueCache.TryGetValue(cacheKey, out System.Object? cachedValue))
        {
            return (System.Int64?)cachedValue;
        }

        System.String stringValue = GetString(section, key);

        if (stringValue.Equals("null", System.StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }
        else if (System.Int64.TryParse(stringValue, out System.Int64 parsedValue))
        {
            _valueCache[cacheKey] = parsedValue;
            return parsedValue;
        }

        return null;
    }

    /// <summary>
    /// Gets the value for the specified key in the specified section as an unsigned long.
    /// </summary>
    /// <param name="section">The section name in the INI file.</param>
    /// <param name="key">The key name in the section.</param>
    /// <returns>The unsigned long value if parsed successfully, otherwise null.</returns>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
    [return: System.Diagnostics.CodeAnalysis.MaybeNull]
    public System.UInt64? GetUInt64(
        [System.Diagnostics.CodeAnalysis.NotNull] System.String section,
        [System.Diagnostics.CodeAnalysis.NotNull] System.String key)
    {
        System.String cacheKey = CreateCacheKey(section, key, "uint64");

        if (_valueCache.TryGetValue(cacheKey, out System.Object? cachedValue))
        {
            return (System.UInt64?)cachedValue;
        }

        System.String stringValue = GetString(section, key);

        if (stringValue.Equals("null", System.StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }
        else if (System.UInt64.TryParse(stringValue, out System.UInt64 parsedValue))
        {
            _valueCache[cacheKey] = parsedValue;
            return parsedValue;
        }

        return null;
    }

    /// <summary>
    /// Gets the value for the specified key in the specified section as a float.
    /// </summary>
    /// <param name="section">The section name in the INI file.</param>
    /// <param name="key">The key name in the section.</param>
    /// <returns>The float value if parsed successfully, otherwise null.</returns>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
    [return: System.Diagnostics.CodeAnalysis.MaybeNull]
    public System.Single? GetSingle(
        [System.Diagnostics.CodeAnalysis.NotNull] System.String section,
        [System.Diagnostics.CodeAnalysis.NotNull] System.String key)
    {
        System.String cacheKey = CreateCacheKey(section, key, "single");

        if (_valueCache.TryGetValue(cacheKey, out System.Object? cachedValue))
        {
            return (System.Single?)cachedValue;
        }

        System.String stringValue = GetString(section, key);

        if (stringValue.Equals("null", System.StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }
        else if (System.Single.TryParse(
            stringValue, System.Globalization.NumberStyles.Float,
            System.Globalization.CultureInfo.InvariantCulture, out System.Single parsedValue))
        {
            _valueCache[cacheKey] = parsedValue;
            return parsedValue;
        }

        return null;
    }

    /// <summary>
    /// Gets the value for the specified key in the specified section as a double.
    /// </summary>
    /// <param name="section">The section name in the INI file.</param>
    /// <param name="key">The key name in the section.</param>
    /// <returns>The double value if parsed successfully, otherwise null.</returns>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
    [return: System.Diagnostics.CodeAnalysis.MaybeNull]
    public System.Double? GetDouble(System.String section, System.String key)
    {
        System.String cacheKey = CreateCacheKey(section, key, "double");

        if (_valueCache.TryGetValue(cacheKey, out System.Object? cachedValue))
        {
            return (System.Double?)cachedValue;
        }

        System.String stringValue = GetString(section, key);

        if (stringValue.Equals("null", System.StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }
        else if (System.Double.TryParse(
            stringValue, System.Globalization.NumberStyles.Float,
            System.Globalization.CultureInfo.InvariantCulture, out System.Double parsedValue))
        {
            _valueCache[cacheKey] = parsedValue;
            return parsedValue;
        }

        return null;
    }

    /// <summary>
    /// Gets the value for the specified key in the specified section as a DateTime.
    /// </summary>
    /// <param name="section">The section name in the INI file.</param>
    /// <param name="key">The key name in the section.</param>
    /// <returns>The DateTime value if parsed successfully, otherwise null.</returns>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
    [return: System.Diagnostics.CodeAnalysis.MaybeNull]
    public System.DateTime? GetDateTime(
        [System.Diagnostics.CodeAnalysis.NotNull] System.String section,
        [System.Diagnostics.CodeAnalysis.NotNull] System.String key)
    {
        System.String cacheKey = CreateCacheKey(section, key, "datetime");

        if (_valueCache.TryGetValue(cacheKey, out System.Object? cachedValue))
        {
            return (System.DateTime?)cachedValue;
        }

        System.String stringValue = GetString(section, key);

        if (stringValue.Equals("null", System.StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }
        else if (System.DateTime.TryParse(
            stringValue, System.Globalization.CultureInfo.InvariantCulture,
            System.Globalization.DateTimeStyles.None, out System.DateTime parsedValue))
        {
            _valueCache[cacheKey] = parsedValue;
            return parsedValue;
        }

        return null;
    }

    /// <summary>
    /// Gets the value for the specified key in the specified section as a TimeSpan.
    /// </summary>
    /// <param name="section">The section name in the INI file.</param>
    /// <param name="key">The key name in the section.</param>
    /// <returns>The TimeSpan value if parsed successfully, otherwise null.</returns>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
    [return: System.Diagnostics.CodeAnalysis.MaybeNull]
    public System.TimeSpan? GetTimeSpan(
        [System.Diagnostics.CodeAnalysis.NotNull] System.String section,
        [System.Diagnostics.CodeAnalysis.NotNull] System.String key)
    {
        System.String cacheKey = CreateCacheKey(section, key, "timespan");

        if (_valueCache.TryGetValue(cacheKey, out System.Object? cachedValue))
        {
            return (System.TimeSpan?)cachedValue;
        }

        System.String stringValue = GetString(section, key);

        if (stringValue.Equals("null", System.StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }
        else if (System.TimeSpan.TryParse(
            stringValue,
            System.Globalization.CultureInfo.InvariantCulture, out System.TimeSpan parsedValue))
        {
            _valueCache[cacheKey] = parsedValue;
            return parsedValue;
        }

        return null;
    }

    /// <summary>
    /// Gets the value for the specified key in the specified section as a Guid.
    /// </summary>
    /// <param name="section">The section name in the INI file.</param>
    /// <param name="key">The key name in the section.</param>
    /// <returns>The Guid value if parsed successfully, otherwise null.</returns>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
    [return: System.Diagnostics.CodeAnalysis.MaybeNull]
    public System.Guid? GetGuid(
        [System.Diagnostics.CodeAnalysis.NotNull] System.String section,
        [System.Diagnostics.CodeAnalysis.NotNull] System.String key)
    {
        System.String cacheKey = CreateCacheKey(section, key, "guid");

        if (_valueCache.TryGetValue(cacheKey, out System.Object? cachedValue))
        {
            return (System.Guid?)cachedValue;
        }

        System.String stringValue = GetString(section, key);

        if (stringValue.Equals("null", System.StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }
        else if (System.Guid.TryParse(stringValue, out System.Guid parsedValue))
        {
            _valueCache[cacheKey] = parsedValue;
            return parsedValue;
        }

        return null;
    }

    /// <summary>
    /// Gets the value for the specified key in the specified section as an enum of type <typeparamref name="TEnum"/>.
    /// </summary>
    /// <typeparam name="TEnum">The enum type to parse.</typeparam>
    /// <param name="section">The section name in the INI file.</param>
    /// <param name="key">The key name in the section.</param>
    /// <returns>The enum value if parsed successfully, otherwise null.</returns>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
    [return: System.Diagnostics.CodeAnalysis.MaybeNull]
    public TEnum? GetEnum<TEnum>(
        [System.Diagnostics.CodeAnalysis.NotNull] System.String section,
        [System.Diagnostics.CodeAnalysis.NotNull] System.String key) where TEnum : struct, System.Enum
    {
        System.String cacheKey = CreateCacheKey(section, key, $"enum:{typeof(TEnum).FullName}");

        if (_valueCache.TryGetValue(cacheKey, out System.Object? cachedValue))
        {
            return (TEnum?)cachedValue;
        }

        System.String stringValue = GetString(section, key);
        if (System.String.IsNullOrEmpty(stringValue))
        {
            return null;
        }
        else if (stringValue.Equals("null", System.StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        // Try parse name (case-insensitive)
        if (System.Enum.TryParse<TEnum>(stringValue, true, out var result))
        {
            _valueCache[cacheKey] = result;
            return result;
        }

        // Try parse numeric value (handles all underlying types)
        try
        {
            System.Object numeric = System.Convert.ChangeType(stringValue,
                System.Enum.GetUnderlyingType(typeof(TEnum)),
                System.Globalization.CultureInfo.InvariantCulture);

            var boxed = (TEnum)System.Enum.ToObject(typeof(TEnum), numeric);
            _valueCache[cacheKey] = boxed;
            return boxed;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Forces a write of any pending changes to the file.
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
    public void Flush()
    {
        if (_isDirty)
        {
            WriteFile();
        }
    }

    #endregion Public API

    #region Private Methods

    /// <summary>
    /// Creates a cache key from section, key, and optional type suffix.
    /// </summary>
    [System.Diagnostics.StackTraceHidden]
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    [return: System.Diagnostics.CodeAnalysis.NotNull]
    private static System.String CreateCacheKey(
        System.String section, 
        System.String key, 
        System.String? typeSuffix = null)
    {
        if (typeSuffix == null)
        {
            return string.Concat(section, CacheKeySeparator.ToString(), key);
        }

        return string.Concat(section, CacheKeySeparator.ToString(), key, CacheKeySeparator.ToString(), typeSuffix);
    }

    /// <summary>
    /// Formats a value for storage in the INI file.
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    private static System.String FormatValue(System.Object value)
    {
        if (value == null)
        {
            return System.String.Empty;
        }

        // ToByteArray numeric values with invariant culture for consistency
        return value switch
        {
            System.Single f => f.ToString("G", System.Globalization.CultureInfo.InvariantCulture),
            System.Double d => d.ToString("G", System.Globalization.CultureInfo.InvariantCulture),
            System.Decimal m => m.ToString("G", System.Globalization.CultureInfo.InvariantCulture),
            System.DateTime dt => dt.ToString("O", System.Globalization.CultureInfo.InvariantCulture),
            _ => value.ToString() ?? System.String.Empty
        };
    }

    /// <summary>
    /// Loads the INI file with retry logic for handling file access issues.
    /// </summary>
    [System.Diagnostics.StackTraceHidden]
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    private void LoadWithRetry()
    {
        const System.Int32 maxRetries = 3;
        System.Int32 retryCount = 0;
        System.Boolean success = false;

        while (!success && retryCount < maxRetries)
        {
            try
            {
                Load();
                success = true;
            }
            catch (System.IO.IOException)
            {
                retryCount++;
                if (retryCount >= maxRetries)
                {
                    throw;
                }

                // Push exponential backoff delay
                System.Threading.Thread.Sleep(100 * (System.Int32)System.Math.Pow(2, retryCount - 1));
            }
        }
    }

    /// <summary>
    /// Loads the data from the INI file into memory with optimized parsing.
    /// </summary>
    /// <exception cref="System.IO.IOException">Thrown when file reading fails.</exception>
    /// <exception cref="System.UnauthorizedAccessException">Thrown when file access is denied.</exception>
    [System.Diagnostics.StackTraceHidden]
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    private void Load()
    {
        if (!ExistsFile)
        {
            return;
        }

        _fileLock.EnterReadLock();

        System.String currentSection = System.String.Empty;
        System.Collections.Generic.Dictionary<System.String, System.String> currentSectionData;

        try
        {
            // Clear existing data
            _iniData.Clear();
            _valueCache.Clear();

            currentSectionData = new(System.StringComparer.OrdinalIgnoreCase);
            _iniData[currentSection] = currentSectionData;

            // Use a buffered reader for better performance
            using var reader = new System.IO.StreamReader(
                _path, System.Text.Encoding.UTF8, true, DefaultBufferSize);

            System.Int32 lineNumber = 0;
            System.String? line;

            while ((line = reader.ReadLine()) != null)
            {
                lineNumber++;
                System.String trimmedLine = line.Trim();

                // Skip empty lines or comments
                if (System.String.IsNullOrEmpty(trimmedLine) || trimmedLine[0] == CommentChar)
                {
                    continue;
                }

                // Process section
                if (trimmedLine[0] == SectionStart && trimmedLine[^1] == SectionEnd)
                {
                    currentSection = trimmedLine.Substring(1, trimmedLine.Length - 2).Trim();

                    // Validate section name
                    if (System.String.IsNullOrWhiteSpace(currentSection))
                    {
                        // Skip invalid section but continue parsing
                        continue;
                    }

                    if (!_iniData.TryGetValue(currentSection, out currentSectionData!))
                    {
                        currentSectionData = new(System.StringComparer.OrdinalIgnoreCase);
                        _iniData[currentSection] = currentSectionData;
                    }
                }
                else
                {
                    // Handle key-value pairs with optimized parsing
                    System.Int32 separatorIndex = trimmedLine.IndexOf(KeyValueSeparator);
                    if (separatorIndex > 0 && separatorIndex < trimmedLine.Length - 1)
                    {
                        System.String key = trimmedLine.Substring(0, separatorIndex).Trim();
                        System.String value = trimmedLine.Substring(separatorIndex + 1).Trim();

                        // Skip if key is empty
                        if (System.String.IsNullOrWhiteSpace(key))
                        {
                            continue;
                        }

                        // Store the key-value pair in the current section
                        currentSectionData[key] = value;
                    }
                }
            }

            // Store the last read time for file change detection
            _lastFileReadTime = System.IO.File.GetLastWriteTimeUtc(_path);
            _isDirty = false;
        }
        catch (System.IO.FileNotFoundException ex)
        {
            throw new System.IO.IOException($"Configuration file not found: {_path}", ex);
        }
        catch (System.UnauthorizedAccessException ex)
        {
            throw new System.UnauthorizedAccessException($"Access denied to configuration file: {_path}", ex);
        }
        catch (System.IO.IOException)
        {
            // Re-throw IO exceptions as-is
            throw;
        }
        catch (System.Exception ex)
        {
            throw new System.IO.IOException($"Error reading configuration file: {_path}", ex);
        }
        finally
        {
            _fileLock.ExitReadLock();
        }
    }

    /// <summary>
    /// Checks if the file has been modified externally and reloads if necessary.
    /// </summary>
    [System.Diagnostics.StackTraceHidden]
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    private void CheckFileChanges()
    {
        if (!ExistsFile)
        {
            return;
        }

        try
        {
            System.DateTime lastWriteTime = System.IO.File.GetLastWriteTimeUtc(_path);
            if (lastWriteTime > _lastFileReadTime)
            {
                Load();
            }
        }
        catch (System.IO.IOException)
        {
            // Ignore file access errors - we'll use the data we have
        }
    }

    /// <summary>
    /// Writes the INI data to the file with optimized I/O and error handling.
    /// Uses atomic file replacement to prevent data corruption.
    /// </summary>
    /// <exception cref="System.IO.IOException">Thrown when file writing fails.</exception>
    /// <exception cref="System.UnauthorizedAccessException">Thrown when file access is denied.</exception>
    [System.Diagnostics.StackTraceHidden]
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    private void WriteFile()
    {
        if (!_isDirty)
        {
            return;
        }

        _fileLock.EnterWriteLock();
        System.String? tempFileName = null;

        try
        {
            // Ensure directory exists with validation
            System.String? directory = System.IO.Path.GetDirectoryName(_path);
            if (System.String.IsNullOrWhiteSpace(directory))
            {
                throw new System.InvalidOperationException(
                    "Cannot write configuration file: invalid directory path.");
            }

            if (!System.IO.Directory.Exists(directory))
            {
                try
                {
                    _ = System.IO.Directory.CreateDirectory(directory);
                }
                catch (System.UnauthorizedAccessException ex)
                {
                    throw new System.UnauthorizedAccessException(
                        $"Access denied when creating directory: {directory}", ex);
                }
            }

            // Write to a temporary file first to prevent corruption
            tempFileName = _path + ".tmp";

            // Delete temp file if it exists from a previous failed operation
            if (System.IO.File.Exists(tempFileName))
            {
                try
                {
                    System.IO.File.Delete(tempFileName);
                }
                catch (System.IO.IOException)
                {
                    // If we can't delete the temp file, try with a different name
                    tempFileName = $"{_path}.{System.Guid.NewGuid():N}.tmp";
                }
            }

            using (System.IO.StreamWriter writer = new(
                tempFileName, false, System.Text.Encoding.UTF8, DefaultBufferSize))
            {
                foreach (var section in _iniData)
                {
                    if (section.Key != System.String.Empty)
                    {
                        writer.Write(SectionStart);
                        writer.Write(section.Key);
                        writer.WriteLine(SectionEnd);
                    }

                    foreach (var keyValue in section.Value)
                    {
                        writer.Write(keyValue.Key);
                        writer.Write(KeyValueSeparator);
                        writer.WriteLine(keyValue.Value);
                    }

                    writer.WriteLine(); // Empty line between sections
                }

                // Ensure all data is written to disk
                writer.Flush();
            }

            // Atomic file replacement
            if (System.IO.File.Exists(_path))
            {
                System.IO.File.Replace(tempFileName, _path, null);
            }
            else
            {
                System.IO.File.Move(tempFileName, _path);
            }

            // Update last write time after our own modification
            _lastFileReadTime = System.IO.File.GetLastWriteTimeUtc(_path);
            _isDirty = false;
            tempFileName = null; // Mark as successfully processed
        }
        catch (System.UnauthorizedAccessException ex)
        {
            throw new System.UnauthorizedAccessException(
                $"Access denied when writing configuration file: {_path}", ex);
        }
        catch (System.IO.PathTooLongException ex)
        {
            throw new System.IO.IOException(
                $"Configuration file path is too long: {_path}", ex);
        }
        catch (System.IO.IOException ex)
        {
            throw new System.IO.IOException(
                $"I/O error writing configuration file: {_path}", ex);
        }
        catch (System.Exception ex)
        {
            throw new System.IO.IOException(
                $"Unexpected error writing configuration file: {_path}", ex);
        }
        finally
        {
            // Clean up temp file if write failed
            if (tempFileName != null && System.IO.File.Exists(tempFileName))
            {
                try
                {
                    System.IO.File.Delete(tempFileName);
                }
                catch
                {
                    // Ignore cleanup errors
                }
            }

            _fileLock.ExitWriteLock();
        }
    }

    #endregion Private Methods
}