using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;

namespace Nalix.Shared.Configuration.Internal;

/// <summary>
/// A high-performance wrapper class for reading and writing INI files.
/// </summary>
internal sealed class IniConfig
{
    #region Constants

    // LZ4Constants for better readability and performance
    private const Char SectionStart = '[';

    private const Char SectionEnd = ']';
    private const Char KeyValueSeparator = '=';
    private const Char CommentChar = ';';

    // Standard buffer sizes
    private const Int32 DefaultBufferSize = 4096;

    #endregion Constants

    #region Fields

    // Thread synchronization for file operations
    private readonly ReaderWriterLockSlim _fileLock = new(LockRecursionPolicy.NoRecursion);

    private readonly String _path;
    private readonly Dictionary<String, Dictionary<String, String>> _iniData;

    // Caches for frequently accessed values
    private readonly Dictionary<String, Object> _valueCache = new(StringComparer.OrdinalIgnoreCase);

    // Track if the file has been modified
    private Boolean _isDirty;

    private DateTime _lastFileReadTime;

    #endregion Fields

    #region Properties

    /// <summary>
    /// Checks whether the file exists at the provided path.
    /// </summary>
    public Boolean ExistsFile => File.Exists(_path);

    #endregion Properties

    #region Constructor

    /// <summary>
    /// Initializes a new instance of the <see cref="IniConfig"/> class for the specified path.
    /// </summary>
    /// <param name="path">The path to the INI file.</param>
    public IniConfig(String path)
    {
        _path = path ?? throw new ArgumentNullException(nameof(path));

        // Use case-insensitive keys for sections and keys
        _iniData = new Dictionary<String, Dictionary<String, String>>(StringComparer.OrdinalIgnoreCase);

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
    public void Reload() => Load();

    /// <summary>
    /// Writes a value to the INI file if the key does not already exist.
    /// </summary>
    /// <param name="section">The section name in the INI file.</param>
    /// <param name="key">The key name in the section.</param>
    /// <param name="value">The value to write.</param>
    public void WriteValue(String section, String key, Object value)
    {
        ArgumentNullException.ThrowIfNull(key);
        ArgumentNullException.ThrowIfNull(section);

        _fileLock.EnterUpgradeableReadLock();
        try
        {
            // Check for external file changes
            CheckFileChanges();

            if (!_iniData.TryGetValue(section, out Dictionary<String, String>? sectionData))
            {
                _fileLock.EnterWriteLock();
                try
                {
                    sectionData = new Dictionary<String, String>(StringComparer.OrdinalIgnoreCase);
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
                    String stringValue = FormatValue(value);
                    sectionData[key] = stringValue;

                    // Dispose any cached value for this key
                    String cacheKey = $"{section}:{key}";
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
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public String GetString(String section, String key)
    {
        ArgumentNullException.ThrowIfNull(key);
        ArgumentNullException.ThrowIfNull(section);

        // Check for file changes before reading
        CheckFileChanges();

        _fileLock.EnterReadLock();
        try
        {
            return _iniData.TryGetValue(section, out Dictionary<String, String>? sectionData) &&
                sectionData.TryGetValue(key, out String? value)
                ? value
                : String.Empty;
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
    public Char? GetChar(String section, String key)
    {
        String stringValue = GetString(section, key);
        return !String.IsNullOrEmpty(stringValue) && stringValue.Length == 1 ? stringValue[0] : null;
    }

    /// <summary>
    /// Gets the value for the specified key in the specified section as a boolean.
    /// </summary>
    public Boolean? GetBool(String section, String key)
    {
        String cacheKey = $"{section}:{key}:bool";

        if (_valueCache.TryGetValue(cacheKey, out Object? cachedValue))
        {
            return (Boolean?)cachedValue;
        }

        String stringValue = GetString(section, key);
        Boolean? result = null;

        if (!String.IsNullOrEmpty(stringValue))
        {
            // Optimize common boolean representations
            if (stringValue.Equals("true", StringComparison.OrdinalIgnoreCase) ||
                stringValue.Equals("1", StringComparison.OrdinalIgnoreCase) ||
                stringValue.Equals("yes", StringComparison.OrdinalIgnoreCase))
            {
                result = true;
            }
            else if (stringValue.Equals("false", StringComparison.OrdinalIgnoreCase) ||
                     stringValue.Equals("0", StringComparison.OrdinalIgnoreCase) ||
                     stringValue.Equals("no", StringComparison.OrdinalIgnoreCase))
            {
                result = false;
            }
            else
            {
                _ = Boolean.TryParse(stringValue, out Boolean parsedValue);
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
    public Decimal? GetDecimal(String section, String key)
    {
        String cacheKey = $"{section}:{key}:decimal";

        if (_valueCache.TryGetValue(cacheKey, out Object? cachedValue))
        {
            return (Decimal?)cachedValue;
        }

        String stringValue = GetString(section, key);

        if (Decimal.TryParse(stringValue, NumberStyles.Number, CultureInfo.InvariantCulture, out Decimal parsedValue))
        {
            _valueCache[cacheKey] = parsedValue;
            return parsedValue;
        }

        return null;
    }

    /// <summary>
    /// Gets the value for the specified key in the specified section as a byte.
    /// </summary>
    public Byte? GetByte(String section, String key)
    {
        String cacheKey = $"{section}:{key}:byte";

        if (_valueCache.TryGetValue(cacheKey, out Object? cachedValue))
        {
            return (Byte?)cachedValue;
        }

        String stringValue = GetString(section, key);

        if (Byte.TryParse(stringValue, out Byte parsedValue))
        {
            _valueCache[cacheKey] = parsedValue;
            return parsedValue;
        }

        return null;
    }

    /// <summary>
    /// Gets the value for the specified key in the specified section as an sbyte.
    /// </summary>
    public SByte? GetSByte(String section, String key)
    {
        String cacheKey = $"{section}:{key}:sbyte";

        if (_valueCache.TryGetValue(cacheKey, out Object? cachedValue))
        {
            return (SByte?)cachedValue;
        }

        String stringValue = GetString(section, key);

        if (SByte.TryParse(stringValue, out SByte parsedValue))
        {
            _valueCache[cacheKey] = parsedValue;
            return parsedValue;
        }

        return null;
    }

    /// <summary>
    /// Gets the value for the specified key in the specified section as a short.
    /// </summary>
    public Int16? GetInt16(String section, String key)
    {
        String cacheKey = $"{section}:{key}:int16";

        if (_valueCache.TryGetValue(cacheKey, out Object? cachedValue))
        {
            return (Int16?)cachedValue;
        }

        String stringValue = GetString(section, key);

        if (Int16.TryParse(stringValue, out Int16 parsedValue))
        {
            _valueCache[cacheKey] = parsedValue;
            return parsedValue;
        }

        return null;
    }

    /// <summary>
    /// Gets the value for the specified key in the specified section as an unsigned short.
    /// </summary>
    public UInt16? GetUInt16(String section, String key)
    {
        String cacheKey = $"{section}:{key}:uint16";

        if (_valueCache.TryGetValue(cacheKey, out Object? cachedValue))
        {
            return (UInt16?)cachedValue;
        }

        String stringValue = GetString(section, key);

        if (UInt16.TryParse(stringValue, out UInt16 parsedValue))
        {
            _valueCache[cacheKey] = parsedValue;
            return parsedValue;
        }

        return null;
    }

    /// <summary>
    /// Gets the value for the specified key in the specified section as an integer.
    /// </summary>
    public Int32? GetInt32(String section, String key)
    {
        String cacheKey = $"{section}:{key}:int32";

        if (_valueCache.TryGetValue(cacheKey, out Object? cachedValue))
        {
            return (Int32?)cachedValue;
        }

        String stringValue = GetString(section, key);

        if (Int32.TryParse(stringValue, out Int32 parsedValue))
        {
            _valueCache[cacheKey] = parsedValue;
            return parsedValue;
        }

        return null;
    }

    /// <summary>
    /// Gets the value for the specified key in the specified section as an unsigned integer.
    /// </summary>
    public UInt32? GetUInt32(String section, String key)
    {
        String cacheKey = $"{section}:{key}:uint32";

        if (_valueCache.TryGetValue(cacheKey, out Object? cachedValue))
        {
            return (UInt32?)cachedValue;
        }

        String stringValue = GetString(section, key);

        if (UInt32.TryParse(stringValue, out UInt32 parsedValue))
        {
            _valueCache[cacheKey] = parsedValue;
            return parsedValue;
        }

        return null;
    }

    /// <summary>
    /// Gets the value for the specified key in the specified section as a long.
    /// </summary>
    public Int64? GetInt64(String section, String key)
    {
        String cacheKey = $"{section}:{key}:int64";

        if (_valueCache.TryGetValue(cacheKey, out Object? cachedValue))
        {
            return (Int64?)cachedValue;
        }

        String stringValue = GetString(section, key);

        if (Int64.TryParse(stringValue, out Int64 parsedValue))
        {
            _valueCache[cacheKey] = parsedValue;
            return parsedValue;
        }

        return null;
    }

    /// <summary>
    /// Gets the value for the specified key in the specified section as an unsigned long.
    /// </summary>
    public UInt64? GetUInt64(String section, String key)
    {
        String cacheKey = $"{section}:{key}:uint64";

        if (_valueCache.TryGetValue(cacheKey, out Object? cachedValue))
        {
            return (UInt64?)cachedValue;
        }

        String stringValue = GetString(section, key);

        if (UInt64.TryParse(stringValue, out UInt64 parsedValue))
        {
            _valueCache[cacheKey] = parsedValue;
            return parsedValue;
        }

        return null;
    }

    /// <summary>
    /// Gets the value for the specified key in the specified section as a float.
    /// </summary>
    public Single? GetSingle(String section, String key)
    {
        String cacheKey = $"{section}:{key}:single";

        if (_valueCache.TryGetValue(cacheKey, out Object? cachedValue))
        {
            return (Single?)cachedValue;
        }

        String stringValue = GetString(section, key);

        if (Single.TryParse(stringValue, NumberStyles.Float, CultureInfo.InvariantCulture, out Single parsedValue))
        {
            _valueCache[cacheKey] = parsedValue;
            return parsedValue;
        }

        return null;
    }

    /// <summary>
    /// Gets the value for the specified key in the specified section as a double.
    /// </summary>
    public Double? GetDouble(String section, String key)
    {
        String cacheKey = $"{section}:{key}:double";

        if (_valueCache.TryGetValue(cacheKey, out Object? cachedValue))
        {
            return (Double?)cachedValue;
        }

        String stringValue = GetString(section, key);

        if (Double.TryParse(stringValue, NumberStyles.Float, CultureInfo.InvariantCulture, out Double parsedValue))
        {
            _valueCache[cacheKey] = parsedValue;
            return parsedValue;
        }

        return null;
    }

    /// <summary>
    /// Gets the value for the specified key in the specified section as a DateTime.
    /// </summary>
    public DateTime? GetDateTime(String section, String key)
    {
        String cacheKey = $"{section}:{key}:datetime";

        if (_valueCache.TryGetValue(cacheKey, out Object? cachedValue))
        {
            return (DateTime?)cachedValue;
        }

        String stringValue = GetString(section, key);

        if (DateTime.TryParse(stringValue, CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime parsedValue))
        {
            _valueCache[cacheKey] = parsedValue;
            return parsedValue;
        }

        return null;
    }

    /// <summary>
    /// Gets the value for the specified key in the specified section as a TimeSpan.
    /// </summary>
    public TimeSpan? GetTimeSpan(String section, String key)
    {
        String cacheKey = $"{section}:{key}:timespan";

        if (_valueCache.TryGetValue(cacheKey, out Object? cachedValue))
        {
            return (TimeSpan?)cachedValue;
        }

        String stringValue = GetString(section, key);

        if (TimeSpan.TryParse(stringValue, CultureInfo.InvariantCulture, out TimeSpan parsedValue))
        {
            _valueCache[cacheKey] = parsedValue;
            return parsedValue;
        }

        return null;
    }

    /// <summary>
    /// Gets the value for the specified key in the specified section as a Guid.
    /// </summary>
    public Guid? GetGuid(String section, String key)
    {
        String cacheKey = $"{section}:{key}:guid";

        if (_valueCache.TryGetValue(cacheKey, out Object? cachedValue))
        {
            return (Guid?)cachedValue;
        }

        String stringValue = GetString(section, key);

        if (Guid.TryParse(stringValue, out Guid parsedValue))
        {
            _valueCache[cacheKey] = parsedValue;
            return parsedValue;
        }

        return null;
    }

    /// <summary>
    /// Gets all sections in the INI file.
    /// </summary>
    public IEnumerable<String> GetSections()
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
    public IEnumerable<String> GetKeys(String section)
    {
        ArgumentNullException.ThrowIfNull(section);

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
    private void LoadWithRetry()
    {
        const Int32 maxRetries = 3;
        Int32 retryCount = 0;
        Boolean success = false;

        while (!success && retryCount < maxRetries)
        {
            try
            {
                Load();
                success = true;
            }
            catch (IOException)
            {
                retryCount++;
                if (retryCount >= maxRetries)
                {
                    throw;
                }

                // Add exponential backoff delay
                Thread.Sleep(100 * (Int32)Math.Pow(2, retryCount - 1));
            }
        }
    }

    /// <summary>
    /// Loads the data from the INI file into memory with optimized parsing.
    /// </summary>
    private void Load()
    {
        if (!ExistsFile)
        {
            return;
        }

        _fileLock.EnterReadLock();
        try
        {
            // Dispose existing data
            _iniData.Clear();
            _valueCache.Clear();

            String currentSection = String.Empty;
            Dictionary<String, String> currentSectionData = new(StringComparer.OrdinalIgnoreCase);
            _iniData[currentSection] = currentSectionData;

            // Use a buffered reader for better performance
            using var reader = new StreamReader(_path, Encoding.UTF8, true, DefaultBufferSize);

            String? line;
            while ((line = reader.ReadLine()) != null)
            {
                String trimmedLine = line.Trim();

                // Skip empty lines or comments
                if (String.IsNullOrEmpty(trimmedLine) || trimmedLine[0] == CommentChar)
                {
                    continue;
                }

                // Process section
                if (trimmedLine[0] == SectionStart && trimmedLine[^1] == SectionEnd)
                {
                    currentSection = trimmedLine[1..^1].Trim();

                    if (!_iniData.TryGetValue(currentSection, out currentSectionData!))
                    {
                        currentSectionData = new Dictionary<String, String>(StringComparer.OrdinalIgnoreCase);
                        _iniData[currentSection] = currentSectionData;
                    }
                }
                else
                {
                    // Handle key-value pairs with optimized parsing
                    Int32 separatorIndex = trimmedLine.IndexOf(KeyValueSeparator);
                    if (separatorIndex > 0)
                    {
                        String key = trimmedLine[..separatorIndex].Trim();
                        String value = trimmedLine[(separatorIndex + 1)..].Trim();

                        // Store the key-value pair in the current section
                        currentSectionData[key] = value;
                    }
                }
            }

            // Store the last read time for file change detection
            _lastFileReadTime = File.GetLastWriteTimeUtc(_path);
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
    private void CheckFileChanges()
    {
        if (!ExistsFile)
        {
            return;
        }

        try
        {
            DateTime lastWriteTime = File.GetLastWriteTimeUtc(_path);
            if (lastWriteTime > _lastFileReadTime)
            {
                Load();
            }
        }
        catch (IOException)
        {
            // Ignore file access errors - we'll use the data we have
        }
    }

    /// <summary>
    /// Formats a value for storage in the INI file.
    /// </summary>
    private static String FormatValue(Object value)
    {
        if (value == null)
        {
            return String.Empty;
        }

        // Format numeric values with invariant culture for consistency
        return value switch
        {
            Single f => f.ToString("G", CultureInfo.InvariantCulture),
            Double d => d.ToString("G", CultureInfo.InvariantCulture),
            Decimal m => m.ToString("G", CultureInfo.InvariantCulture),
            DateTime dt => dt.ToString("O", CultureInfo.InvariantCulture),
            _ => value.ToString() ?? String.Empty
        };
    }

    /// <summary>
    /// Writes the INI data to the file with optimized I/O and error handling.
    /// </summary>
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
            String? directory = Path.GetDirectoryName(_path);
            if (!String.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                _ = Directory.CreateDirectory(directory);
            }

            // WriteInt16 to a temporary file first to prevent corruption
            String tempFileName = _path + ".tmp";

            using (var writer = new StreamWriter(tempFileName, false, Encoding.UTF8, DefaultBufferSize))
            {
                foreach (var section in _iniData)
                {
                    if (section.Key != String.Empty)
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
            if (File.Exists(_path))
            {
                File.Replace(tempFileName, _path, null);
            }
            else
            {
                File.Move(tempFileName, _path);
            }

            // Update last write time after our own modification
            _lastFileReadTime = File.GetLastWriteTimeUtc(_path);
            _isDirty = false;
        }
        catch (Exception ex)
        {
            throw new IOException($"Failed to write INI file: {_path}", ex);
        }
        finally
        {
            _fileLock.ExitWriteLock();
        }
    }

    #endregion Private Methods
}