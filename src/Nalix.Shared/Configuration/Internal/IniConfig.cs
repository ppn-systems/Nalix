// Copyright (c) 2025 PPN Corporation. All rights reserved.

#if DEBUG
[assembly: System.Runtime.CompilerServices.InternalsVisibleTo("Nalix.Shared.Tests")]
[assembly: System.Runtime.CompilerServices.InternalsVisibleTo("Nalix.Shared.Benchmarks")]
#endif

namespace Nalix.Shared.Configuration.Internal;

/// <summary>
/// A high-performance wrapper class for reading and writing INI files.
/// </summary>
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
    public IniConfig(System.String path)
    {
        _path = path ?? throw new System.ArgumentNullException(nameof(path));

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
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public void Reload() => Load();

    /// <summary>
    /// Writes a value to the INI file if the key does not already exist.
    /// </summary>
    /// <param name="section">The section name in the INI file.</param>
    /// <param name="key">The key name in the section.</param>
    /// <param name="value">The value to write.</param>    
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public void WriteValue(System.String section, System.String key, System.Object value)
    {
        System.ArgumentNullException.ThrowIfNull(key);
        System.ArgumentNullException.ThrowIfNull(section);

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
            if (!sectionData.ContainsKey(key))
            {
                _fileLock.EnterWriteLock();
                try
                {
                    System.String stringValue = FormatValue(value);
                    sectionData[key] = stringValue;

                    // Dispose any cached value for this key
                    System.String cacheKey = $"{section}:{key}";
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
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public System.String GetString(System.String section, System.String key)
    {
        System.ArgumentNullException.ThrowIfNull(key);
        System.ArgumentNullException.ThrowIfNull(section);

        // Check for file changes before reading
        CheckFileChanges();

        _fileLock.EnterReadLock();
        try
        {
            return _iniData.TryGetValue(section,
                out System.Collections.Generic.Dictionary<System.String, System.String>? sectionData) &&
                sectionData.TryGetValue(key, out System.String? value) ? value : System.String.Empty;
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
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public System.Char? GetChar(System.String section, System.String key)
    {
        System.String stringValue = GetString(section, key);
        return !System.String.IsNullOrEmpty(stringValue) && stringValue.Length == 1 ? stringValue[0] : null;
    }

    /// <summary>
    /// Gets the value for the specified key in the specified section as a boolean.
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public System.Boolean? GetBool(System.String section, System.String key)
    {
        System.String cacheKey = $"{section}:{key}:bool";

        if (_valueCache.TryGetValue(cacheKey, out System.Object? cachedValue))
        {
            return (System.Boolean?)cachedValue;
        }

        System.String stringValue = GetString(section, key);
        System.Boolean? result = null;

        if (!System.String.IsNullOrEmpty(stringValue))
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
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public System.Decimal? GetDecimal(System.String section, System.String key)
    {
        System.String cacheKey = $"{section}:{key}:decimal";

        if (_valueCache.TryGetValue(cacheKey, out System.Object? cachedValue))
        {
            return (System.Decimal?)cachedValue;
        }

        System.String stringValue = GetString(section, key);

        if (System.Decimal.TryParse(
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
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public System.Byte? GetByte(System.String section, System.String key)
    {
        System.String cacheKey = $"{section}:{key}:byte";

        if (_valueCache.TryGetValue(cacheKey, out System.Object? cachedValue))
        {
            return (System.Byte?)cachedValue;
        }

        System.String stringValue = GetString(section, key);

        if (System.Byte.TryParse(stringValue, out System.Byte parsedValue))
        {
            _valueCache[cacheKey] = parsedValue;
            return parsedValue;
        }

        return null;
    }

    /// <summary>
    /// Gets the value for the specified key in the specified section as an sbyte.
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public System.SByte? GetSByte(System.String section, System.String key)
    {
        System.String cacheKey = $"{section}:{key}:sbyte";

        if (_valueCache.TryGetValue(cacheKey, out System.Object? cachedValue))
        {
            return (System.SByte?)cachedValue;
        }

        System.String stringValue = GetString(section, key);

        if (System.SByte.TryParse(stringValue, out System.SByte parsedValue))
        {
            _valueCache[cacheKey] = parsedValue;
            return parsedValue;
        }

        return null;
    }

    /// <summary>
    /// Gets the value for the specified key in the specified section as a short.
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public System.Int16? GetInt16(System.String section, System.String key)
    {
        System.String cacheKey = $"{section}:{key}:int16";

        if (_valueCache.TryGetValue(cacheKey, out System.Object? cachedValue))
        {
            return (System.Int16?)cachedValue;
        }

        System.String stringValue = GetString(section, key);

        if (System.Int16.TryParse(stringValue, out System.Int16 parsedValue))
        {
            _valueCache[cacheKey] = parsedValue;
            return parsedValue;
        }

        return null;
    }

    /// <summary>
    /// Gets the value for the specified key in the specified section as an unsigned short.
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public System.UInt16? GetUInt16(System.String section, System.String key)
    {
        System.String cacheKey = $"{section}:{key}:uint16";

        if (_valueCache.TryGetValue(cacheKey, out System.Object? cachedValue))
        {
            return (System.UInt16?)cachedValue;
        }

        System.String stringValue = GetString(section, key);

        if (System.UInt16.TryParse(stringValue, out System.UInt16 parsedValue))
        {
            _valueCache[cacheKey] = parsedValue;
            return parsedValue;
        }

        return null;
    }

    /// <summary>
    /// Gets the value for the specified key in the specified section as an integer.
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public System.Int32? GetInt32(System.String section, System.String key)
    {
        System.String cacheKey = $"{section}:{key}:int32";

        if (_valueCache.TryGetValue(cacheKey, out System.Object? cachedValue))
        {
            return (System.Int32?)cachedValue;
        }

        System.String stringValue = GetString(section, key);

        if (System.Int32.TryParse(stringValue, out System.Int32 parsedValue))
        {
            _valueCache[cacheKey] = parsedValue;
            return parsedValue;
        }

        return null;
    }

    /// <summary>
    /// Gets the value for the specified key in the specified section as an unsigned integer.
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public System.UInt32? GetUInt32(System.String section, System.String key)
    {
        System.String cacheKey = $"{section}:{key}:uint32";

        if (_valueCache.TryGetValue(cacheKey, out System.Object? cachedValue))
        {
            return (System.UInt32?)cachedValue;
        }

        System.String stringValue = GetString(section, key);

        if (System.UInt32.TryParse(stringValue, out System.UInt32 parsedValue))
        {
            _valueCache[cacheKey] = parsedValue;
            return parsedValue;
        }

        return null;
    }

    /// <summary>
    /// Gets the value for the specified key in the specified section as a long.
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public System.Int64? GetInt64(System.String section, System.String key)
    {
        System.String cacheKey = $"{section}:{key}:int64";

        if (_valueCache.TryGetValue(cacheKey, out System.Object? cachedValue))
        {
            return (System.Int64?)cachedValue;
        }

        System.String stringValue = GetString(section, key);

        if (System.Int64.TryParse(stringValue, out System.Int64 parsedValue))
        {
            _valueCache[cacheKey] = parsedValue;
            return parsedValue;
        }

        return null;
    }

    /// <summary>
    /// Gets the value for the specified key in the specified section as an unsigned long.
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public System.UInt64? GetUInt64(System.String section, System.String key)
    {
        System.String cacheKey = $"{section}:{key}:uint64";

        if (_valueCache.TryGetValue(cacheKey, out System.Object? cachedValue))
        {
            return (System.UInt64?)cachedValue;
        }

        System.String stringValue = GetString(section, key);

        if (System.UInt64.TryParse(stringValue, out System.UInt64 parsedValue))
        {
            _valueCache[cacheKey] = parsedValue;
            return parsedValue;
        }

        return null;
    }

    /// <summary>
    /// Gets the value for the specified key in the specified section as a float.
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public System.Single? GetSingle(System.String section, System.String key)
    {
        System.String cacheKey = $"{section}:{key}:single";

        if (_valueCache.TryGetValue(cacheKey, out System.Object? cachedValue))
        {
            return (System.Single?)cachedValue;
        }

        System.String stringValue = GetString(section, key);

        if (System.Single.TryParse(
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
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public System.Double? GetDouble(System.String section, System.String key)
    {
        System.String cacheKey = $"{section}:{key}:double";

        if (_valueCache.TryGetValue(cacheKey, out System.Object? cachedValue))
        {
            return (System.Double?)cachedValue;
        }

        System.String stringValue = GetString(section, key);

        if (System.Double.TryParse(
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
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public System.DateTime? GetDateTime(System.String section, System.String key)
    {
        System.String cacheKey = $"{section}:{key}:datetime";

        if (_valueCache.TryGetValue(cacheKey, out System.Object? cachedValue))
        {
            return (System.DateTime?)cachedValue;
        }

        System.String stringValue = GetString(section, key);

        if (System.DateTime.TryParse(
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
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public System.TimeSpan? GetTimeSpan(System.String section, System.String key)
    {
        System.String cacheKey = $"{section}:{key}:timespan";

        if (_valueCache.TryGetValue(cacheKey, out System.Object? cachedValue))
        {
            return (System.TimeSpan?)cachedValue;
        }

        System.String stringValue = GetString(section, key);

        if (System.TimeSpan.TryParse(
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
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public System.Guid? GetGuid(System.String section, System.String key)
    {
        System.String cacheKey = $"{section}:{key}:guid";

        if (_valueCache.TryGetValue(cacheKey, out System.Object? cachedValue))
        {
            return (System.Guid?)cachedValue;
        }

        System.String stringValue = GetString(section, key);

        if (System.Guid.TryParse(stringValue, out System.Guid parsedValue))
        {
            _valueCache[cacheKey] = parsedValue;
            return parsedValue;
        }

        return null;
    }

    /// <summary>
    /// Gets all sections in the INI file.
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public System.Collections.Generic.IEnumerable<System.String> GetSections()
    {
        _fileLock.EnterReadLock();
        try
        {
            return [.. _iniData.Keys];
        }
        finally
        {
            _fileLock.ExitReadLock();
        }
    }

    /// <summary>
    /// Gets all keys in the specified section.
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public System.Collections.Generic.IEnumerable<System.String> GetKeys(System.String section)
    {
        System.ArgumentNullException.ThrowIfNull(section);

        _fileLock.EnterReadLock();
        try
        {
            return _iniData.TryGetValue(section, out var sectionData) ? [.. sectionData.Keys] : [];
        }
        finally
        {
            _fileLock.ExitReadLock();
        }
    }

    /// <summary>
    /// Forces a write of any pending changes to the file.
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public void Flush()
    {
        if (_isDirty)
        {
            WriteFile();
        }
    }

    /// <summary>
    /// Clears the value cache to force fresh reads from the data.
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public void ClearCache()
    {
        _fileLock.EnterWriteLock();
        try
        {
            _valueCache.Clear();
        }
        finally
        {
            _fileLock.ExitWriteLock();
        }
    }

    #endregion Public API

    #region Private Methods

    /// <summary>
    /// Loads the INI file with retry logic for handling file access issues.
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
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
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
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
            // Dispose existing data
            _iniData.Clear();
            _valueCache.Clear();

            currentSectionData = new(System.StringComparer.OrdinalIgnoreCase);
            _iniData[currentSection] = currentSectionData;

            // Use a buffered reader for better performance
            using var reader = new System.IO.StreamReader(
                _path, System.Text.Encoding.UTF8, true, DefaultBufferSize);

            System.String? line;
            while ((line = reader.ReadLine()) != null)
            {
                System.String trimmedLine = line.Trim();

                // Skip empty lines or comments
                if (System.String.IsNullOrEmpty(trimmedLine) || trimmedLine[0] == CommentChar)
                {
                    continue;
                }

                // Process section
                if (trimmedLine[0] == SectionStart && trimmedLine[^1] == SectionEnd)
                {
                    currentSection = trimmedLine[1..^1].Trim();

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
                    if (separatorIndex > 0)
                    {
                        System.String key = trimmedLine[..separatorIndex].Trim();
                        System.String value = trimmedLine[(separatorIndex + 1)..].Trim();

                        // Store the key-value pair in the current section
                        currentSectionData[key] = value;
                    }
                }
            }

            // Store the last read time for file change detection
            _lastFileReadTime = System.IO.File.GetLastWriteTimeUtc(_path);
            _isDirty = false;
        }
        finally
        {
            _fileLock.ExitReadLock();
        }
    }

    /// <summary>
    /// Checks if the file has been modified externally and reloads if necessary.
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
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
    /// Writes the INI data to the file with optimized I/O and error handling.
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    private void WriteFile()
    {
        if (!_isDirty)
        {
            return;
        }

        _fileLock.EnterWriteLock();
        try
        {
            // Ensure directory exists
            System.String? directory = System.IO.Path.GetDirectoryName(_path);
            if (!System.String.IsNullOrEmpty(directory) && !System.IO.Directory.Exists(directory))
            {
                _ = System.IO.Directory.CreateDirectory(directory);
            }

            // WriteInt16 to a temporary file first to prevent corruption
            System.String tempFileName = _path + ".tmp";

            using (System.IO.StreamWriter writer = new(
                tempFileName, false, System.Text.Encoding.UTF8, DefaultBufferSize))
            {
                foreach (var section in _iniData)
                {
                    if (section.Key != System.String.Empty)
                    {
                        writer.WriteLine($"[{section.Key}]");
                    }

                    foreach (var keyValue in section.Value)
                    {
                        writer.WriteLine($"{keyValue.Key}={keyValue.Value}");
                    }

                    writer.WriteLine(); // Empty line between sections
                }
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
        }
        catch (System.Exception ex)
        {
            throw new System.IO.IOException($"Failed to write INI file: {_path}", ex);
        }
        finally
        {
            _fileLock.ExitWriteLock();
        }
    }

    #endregion Private Methods
}