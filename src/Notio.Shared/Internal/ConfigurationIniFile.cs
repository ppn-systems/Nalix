using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;

namespace Notio.Shared.Internal;

/// <summary>
/// A high-performance wrapper class for reading and writing INI files.
/// </summary>
internal sealed class ConfiguredIniFile
{
    // Constants for better readability and performance
    private const char SectionStart = '[';
    private const char SectionEnd = ']';
    private const char KeyValueSeparator = '=';
    private const char CommentChar = ';';

    // Standard buffer sizes
    private const int DefaultBufferSize = 4096;

    // Thread synchronization for file operations
    private readonly ReaderWriterLockSlim _fileLock = new(LockRecursionPolicy.NoRecursion);

    private readonly string _path;
    private readonly Dictionary<string, Dictionary<string, string>> _iniData;

    // Caches for frequently accessed values
    private readonly Dictionary<string, object> _valueCache = new(StringComparer.OrdinalIgnoreCase);

    // Track if the file has been modified
    private bool _isDirty;
    private DateTime _lastFileReadTime;

    /// <summary>
    /// Checks whether the file exists at the provided path.
    /// </summary>
    public bool ExistsFile => File.Exists(_path);

    /// <summary>
    /// Initializes a new instance of the <see cref="ConfiguredIniFile"/> class for the specified path.
    /// </summary>
    /// <param name="path">The path to the INI file.</param>
    public ConfiguredIniFile(string path)
    {
        _path = path ?? throw new ArgumentNullException(nameof(path));

        // Use case-insensitive keys for sections and keys
        _iniData = new Dictionary<string, Dictionary<string, string>>(StringComparer.OrdinalIgnoreCase);

        // Load the file if it exists
        if (ExistsFile)
        {
            LoadWithRetry();
        }
    }

    /// <summary>
    /// Loads the INI file with retry logic for handling file access issues.
    /// </summary>
    private void LoadWithRetry()
    {
        const int maxRetries = 3;
        int retryCount = 0;
        bool success = false;

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
                    throw;

                // Add exponential backoff delay
                Thread.Sleep(100 * (int)Math.Pow(2, retryCount - 1));
            }
        }
    }

    /// <summary>
    /// Loads the data from the INI file into memory with optimized parsing.
    /// </summary>
    private void Load()
    {
        if (!ExistsFile)
            return;

        _fileLock.EnterReadLock();
        try
        {
            // Clear existing data
            _iniData.Clear();
            _valueCache.Clear();

            string currentSection = string.Empty;
            Dictionary<string, string> currentSectionData = new(StringComparer.OrdinalIgnoreCase);
            _iniData[currentSection] = currentSectionData;

            // Use a buffered reader for better performance
            using var reader = new StreamReader(_path, Encoding.UTF8, true, DefaultBufferSize);

            string? line;
            while ((line = reader.ReadLine()) != null)
            {
                string trimmedLine = line.Trim();

                // Skip empty lines or comments
                if (string.IsNullOrEmpty(trimmedLine) || trimmedLine[0] == CommentChar)
                    continue;

                // Process section
                if (trimmedLine[0] == SectionStart && trimmedLine[^1] == SectionEnd)
                {
                    currentSection = trimmedLine[1..^1].Trim();

                    if (!_iniData.TryGetValue(currentSection, out currentSectionData!))
                    {
                        currentSectionData = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                        _iniData[currentSection] = currentSectionData;
                    }
                }
                else
                {
                    // Handle key-value pairs with optimized parsing
                    int separatorIndex = trimmedLine.IndexOf(KeyValueSeparator);
                    if (separatorIndex > 0)
                    {
                        string key = trimmedLine[..separatorIndex].Trim();
                        string value = trimmedLine[(separatorIndex + 1)..].Trim();

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
        if (!ExistsFile) return;

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
    /// Writes a value to the INI file if the key does not already exist.
    /// </summary>
    /// <param name="section">The section name in the INI file.</param>
    /// <param name="key">The key name in the section.</param>
    /// <param name="value">The value to write.</param>
    public void WriteValue(string section, string key, object value)
    {
        ArgumentNullException.ThrowIfNull(key);
        ArgumentNullException.ThrowIfNull(section);

        _fileLock.EnterUpgradeableReadLock();
        try
        {
            // Check for external file changes
            CheckFileChanges();

            if (!_iniData.TryGetValue(section, out Dictionary<string, string>? sectionData))
            {
                _fileLock.EnterWriteLock();
                try
                {
                    sectionData = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
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
                    string stringValue = FormatValue(value);
                    sectionData[key] = stringValue;

                    // Clear any cached value for this key
                    string cacheKey = $"{section}:{key}";
                    _valueCache.Remove(cacheKey);

                    _isDirty = true;
                }
                finally
                {
                    _fileLock.ExitWriteLock();
                }

                // Write changes to the file
                WriteFile();
            }
        }
        finally
        {
            _fileLock.ExitUpgradeableReadLock();
        }
    }

    /// <summary>
    /// Formats a value for storage in the INI file.
    /// </summary>
    private static string FormatValue(object value)
    {
        if (value == null) return string.Empty;

        // Format numeric values with invariant culture for consistency
        return value switch
        {
            float f => f.ToString("G", CultureInfo.InvariantCulture),
            double d => d.ToString("G", CultureInfo.InvariantCulture),
            decimal m => m.ToString("G", CultureInfo.InvariantCulture),
            DateTime dt => dt.ToString("O", CultureInfo.InvariantCulture),
            _ => value.ToString() ?? string.Empty
        };
    }

    /// <summary>
    /// Gets the value for the specified key in the specified section as a string.
    /// </summary>
    /// <param name="section">The section name in the INI file.</param>
    /// <param name="key">The key name in the section.</param>
    /// <returns>The string value, or an empty string if not found.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public string GetString(string section, string key)
    {
        ArgumentNullException.ThrowIfNull(key);
        ArgumentNullException.ThrowIfNull(section);

        // Check for file changes before reading
        CheckFileChanges();

        _fileLock.EnterReadLock();
        try
        {
            if (_iniData.TryGetValue(section, out Dictionary<string, string>? sectionData) &&
                sectionData.TryGetValue(key, out string? value))
            {
                return value;
            }

            return string.Empty;
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
    public char? GetChar(string section, string key)
    {
        string stringValue = GetString(section, key);
        return !string.IsNullOrEmpty(stringValue) && stringValue.Length == 1 ? stringValue[0] : null;
    }

    /// <summary>
    /// Gets the value for the specified key in the specified section as a boolean.
    /// </summary>
    public bool? GetBool(string section, string key)
    {
        string cacheKey = $"{section}:{key}:bool";

        if (_valueCache.TryGetValue(cacheKey, out object? cachedValue))
        {
            return (bool?)cachedValue;
        }

        string stringValue = GetString(section, key);
        bool? result = null;

        if (!string.IsNullOrEmpty(stringValue))
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
                _ = bool.TryParse(stringValue, out bool parsedValue);
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
    public decimal? GetDecimal(string section, string key)
    {
        string cacheKey = $"{section}:{key}:decimal";

        if (_valueCache.TryGetValue(cacheKey, out object? cachedValue))
        {
            return (decimal?)cachedValue;
        }

        string stringValue = GetString(section, key);

        if (decimal.TryParse(stringValue, NumberStyles.Number, CultureInfo.InvariantCulture, out decimal parsedValue))
        {
            _valueCache[cacheKey] = parsedValue;
            return parsedValue;
        }

        return null;
    }

    /// <summary>
    /// Gets the value for the specified key in the specified section as a byte.
    /// </summary>
    public byte? GetByte(string section, string key)
    {
        string cacheKey = $"{section}:{key}:byte";

        if (_valueCache.TryGetValue(cacheKey, out object? cachedValue))
        {
            return (byte?)cachedValue;
        }

        string stringValue = GetString(section, key);

        if (byte.TryParse(stringValue, out byte parsedValue))
        {
            _valueCache[cacheKey] = parsedValue;
            return parsedValue;
        }

        return null;
    }

    /// <summary>
    /// Gets the value for the specified key in the specified section as an sbyte.
    /// </summary>
    public sbyte? GetSByte(string section, string key)
    {
        string cacheKey = $"{section}:{key}:sbyte";

        if (_valueCache.TryGetValue(cacheKey, out object? cachedValue))
        {
            return (sbyte?)cachedValue;
        }

        string stringValue = GetString(section, key);

        if (sbyte.TryParse(stringValue, out sbyte parsedValue))
        {
            _valueCache[cacheKey] = parsedValue;
            return parsedValue;
        }

        return null;
    }

    /// <summary>
    /// Gets the value for the specified key in the specified section as a short.
    /// </summary>
    public short? GetInt16(string section, string key)
    {
        string cacheKey = $"{section}:{key}:int16";

        if (_valueCache.TryGetValue(cacheKey, out object? cachedValue))
        {
            return (short?)cachedValue;
        }

        string stringValue = GetString(section, key);

        if (short.TryParse(stringValue, out short parsedValue))
        {
            _valueCache[cacheKey] = parsedValue;
            return parsedValue;
        }

        return null;
    }

    /// <summary>
    /// Gets the value for the specified key in the specified section as an unsigned short.
    /// </summary>
    public ushort? GetUInt16(string section, string key)
    {
        string cacheKey = $"{section}:{key}:uint16";

        if (_valueCache.TryGetValue(cacheKey, out object? cachedValue))
        {
            return (ushort?)cachedValue;
        }

        string stringValue = GetString(section, key);

        if (ushort.TryParse(stringValue, out ushort parsedValue))
        {
            _valueCache[cacheKey] = parsedValue;
            return parsedValue;
        }

        return null;
    }

    /// <summary>
    /// Gets the value for the specified key in the specified section as an integer.
    /// </summary>
    public int? GetInt32(string section, string key)
    {
        string cacheKey = $"{section}:{key}:int32";

        if (_valueCache.TryGetValue(cacheKey, out object? cachedValue))
        {
            return (int?)cachedValue;
        }

        string stringValue = GetString(section, key);

        if (int.TryParse(stringValue, out int parsedValue))
        {
            _valueCache[cacheKey] = parsedValue;
            return parsedValue;
        }

        return null;
    }

    /// <summary>
    /// Gets the value for the specified key in the specified section as an unsigned integer.
    /// </summary>
    public uint? GetUInt32(string section, string key)
    {
        string cacheKey = $"{section}:{key}:uint32";

        if (_valueCache.TryGetValue(cacheKey, out object? cachedValue))
        {
            return (uint?)cachedValue;
        }

        string stringValue = GetString(section, key);

        if (uint.TryParse(stringValue, out uint parsedValue))
        {
            _valueCache[cacheKey] = parsedValue;
            return parsedValue;
        }

        return null;
    }

    /// <summary>
    /// Gets the value for the specified key in the specified section as a long.
    /// </summary>
    public long? GetInt64(string section, string key)
    {
        string cacheKey = $"{section}:{key}:int64";

        if (_valueCache.TryGetValue(cacheKey, out object? cachedValue))
        {
            return (long?)cachedValue;
        }

        string stringValue = GetString(section, key);

        if (long.TryParse(stringValue, out long parsedValue))
        {
            _valueCache[cacheKey] = parsedValue;
            return parsedValue;
        }

        return null;
    }

    /// <summary>
    /// Gets the value for the specified key in the specified section as an unsigned long.
    /// </summary>
    public ulong? GetUInt64(string section, string key)
    {
        string cacheKey = $"{section}:{key}:uint64";

        if (_valueCache.TryGetValue(cacheKey, out object? cachedValue))
        {
            return (ulong?)cachedValue;
        }

        string stringValue = GetString(section, key);

        if (ulong.TryParse(stringValue, out ulong parsedValue))
        {
            _valueCache[cacheKey] = parsedValue;
            return parsedValue;
        }

        return null;
    }

    /// <summary>
    /// Gets the value for the specified key in the specified section as a float.
    /// </summary>
    public float? GetSingle(string section, string key)
    {
        string cacheKey = $"{section}:{key}:single";

        if (_valueCache.TryGetValue(cacheKey, out object? cachedValue))
        {
            return (float?)cachedValue;
        }

        string stringValue = GetString(section, key);

        if (float.TryParse(stringValue, NumberStyles.Float, CultureInfo.InvariantCulture, out float parsedValue))
        {
            _valueCache[cacheKey] = parsedValue;
            return parsedValue;
        }

        return null;
    }

    /// <summary>
    /// Gets the value for the specified key in the specified section as a double.
    /// </summary>
    public double? GetDouble(string section, string key)
    {
        string cacheKey = $"{section}:{key}:double";

        if (_valueCache.TryGetValue(cacheKey, out object? cachedValue))
        {
            return (double?)cachedValue;
        }

        string stringValue = GetString(section, key);

        if (double.TryParse(stringValue, NumberStyles.Float, CultureInfo.InvariantCulture, out double parsedValue))
        {
            _valueCache[cacheKey] = parsedValue;
            return parsedValue;
        }

        return null;
    }

    /// <summary>
    /// Gets the value for the specified key in the specified section as a DateTime.
    /// </summary>
    public DateTime? GetDateTime(string section, string key)
    {
        string cacheKey = $"{section}:{key}:datetime";

        if (_valueCache.TryGetValue(cacheKey, out object? cachedValue))
        {
            return (DateTime?)cachedValue;
        }

        string stringValue = GetString(section, key);

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
    public TimeSpan? GetTimeSpan(string section, string key)
    {
        string cacheKey = $"{section}:{key}:timespan";

        if (_valueCache.TryGetValue(cacheKey, out object? cachedValue))
        {
            return (TimeSpan?)cachedValue;
        }

        string stringValue = GetString(section, key);

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
    public Guid? GetGuid(string section, string key)
    {
        string cacheKey = $"{section}:{key}:guid";

        if (_valueCache.TryGetValue(cacheKey, out object? cachedValue))
        {
            return (Guid?)cachedValue;
        }

        string stringValue = GetString(section, key);

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
    public IEnumerable<string> GetSections()
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
    public IEnumerable<string> GetKeys(string section)
    {
        ArgumentNullException.ThrowIfNull(section);

        _fileLock.EnterReadLock();
        try
        {
            if (_iniData.TryGetValue(section, out var sectionData))
            {
                return [.. sectionData.Keys];
            }

            return [];
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

    /// <summary>
    /// Reloads the INI file from disk, discarding any unsaved changes.
    /// </summary>
    public void Reload() => Load();

    /// <summary>
    /// Writes the INI data to the file with optimized I/O and error handling.
    /// </summary>
    private void WriteFile()
    {
        if (!_isDirty) return;

        _fileLock.EnterWriteLock();
        try
        {
            // Ensure directory exists
            string? directory = Path.GetDirectoryName(_path);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            // Write to a temporary file first to prevent corruption
            string tempFileName = _path + ".tmp";

            using (var writer = new StreamWriter(tempFileName, false, Encoding.UTF8, DefaultBufferSize))
            {
                foreach (var section in _iniData)
                {
                    if (section.Key != string.Empty)
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
}
