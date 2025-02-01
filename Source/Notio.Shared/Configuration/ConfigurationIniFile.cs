using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;

namespace Notio.Shared.Configuration;

/// <summary>
/// A wrapper class for reading and writing INI files.
/// </summary>
internal sealed class ConfiguredIniFile
{
    private readonly string _path;
    private readonly Dictionary<string, Dictionary<string, string>> _iniData;

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
        _path = path;
        // Use case-insensitive keys for sections and keys.
        _iniData = new Dictionary<string, Dictionary<string, string>>(StringComparer.OrdinalIgnoreCase);
        Load();
    }

    /// <summary>
    /// Loads the data from the INI file into memory.
    /// </summary>
    private void Load()
    {
        if (!ExistsFile)
            return;

        string currentSection = string.Empty;

        foreach (var line in File.ReadLines(_path))
        {
            string trimmedLine = line.Trim();

            // Skip empty lines or comments.
            if (string.IsNullOrEmpty(trimmedLine) || trimmedLine.StartsWith(';'))
                continue;

            // Check if the line is a section header.
            if (trimmedLine.StartsWith('[') && trimmedLine.EndsWith(']'))
            {
                currentSection = trimmedLine[1..^1].Trim();
                if (!_iniData.ContainsKey(currentSection))
                {
                    _iniData[currentSection] = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                }
            }
            else
            {
                // Assume the line is a key-value pair.
                var keyValue = trimmedLine.Split(['='], 2);
                if (keyValue.Length == 2)
                {
                    string key = keyValue[0].Trim();
                    string value = keyValue[1].Trim();

                    // Ensure the current section exists.
                    if (!_iniData.TryGetValue(currentSection, out var section))
                    {
                        section = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                        _iniData[currentSection] = section;
                    }

                    // Assign the key-value pair to the section.
                    section[key] = value;
                }
            }
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
        if (!_iniData.TryGetValue(section, out Dictionary<string, string>? sectionData))
        {
            sectionData = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            _iniData[section] = sectionData;
        }

        // Only write if the key does not already exist.
        if (!sectionData.ContainsKey(key))
        {
            sectionData[key] = value.ToString() ?? string.Empty;
            WriteFile();  // Write the entire data back to the file.
        }
    }

    /// <summary>
    /// Gets the value for the specified key in the specified section as a string.
    /// </summary>
    /// <param name="section">The section name in the INI file.</param>
    /// <param name="key">The key name in the section.</param>
    /// <returns>The string value, or an empty string if not found.</returns>
    public string GetString(string section, string key)
    {
        return _iniData.TryGetValue(section, out Dictionary<string, string>? sectionData) &&
               sectionData.TryGetValue(key, out string? value)
            ? value
            : string.Empty;
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
        return !string.IsNullOrEmpty(stringValue) && stringValue.Length == 1 ? stringValue[0] : (char?)null;
    }

    /// <summary>
    /// Gets the value for the specified key in the specified section as a boolean.
    /// </summary>
    public bool? GetBool(string section, string key)
    {
        string stringValue = GetString(section, key);
        return bool.TryParse(stringValue, out bool parsedValue) ? parsedValue : (bool?)null;
    }

    /// <summary>
    /// Gets the value for the specified key in the specified section as a decimal.
    /// </summary>
    public decimal? GetDecimal(string section, string key)
    {
        string stringValue = GetString(section, key);
        return decimal.TryParse(stringValue, NumberStyles.Number, CultureInfo.InvariantCulture, out decimal parsedValue)
            ? parsedValue
            : (decimal?)null;
    }

    /// <summary>
    /// Gets the value for the specified key in the specified section as a byte.
    /// </summary>
    public byte? GetByte(string section, string key)
    {
        string stringValue = GetString(section, key);
        return byte.TryParse(stringValue, out byte parsedValue) ? parsedValue : (byte?)null;
    }

    /// <summary>
    /// Gets the value for the specified key in the specified section as an sbyte.
    /// </summary>
    public sbyte? GetSByte(string section, string key)
    {
        string stringValue = GetString(section, key);
        return sbyte.TryParse(stringValue, out sbyte parsedValue) ? parsedValue : (sbyte?)null;
    }

    /// <summary>
    /// Gets the value for the specified key in the specified section as a short.
    /// </summary>
    public short? GetInt16(string section, string key)
    {
        string stringValue = GetString(section, key);
        return short.TryParse(stringValue, out short parsedValue) ? parsedValue : (short?)null;
    }

    /// <summary>
    /// Gets the value for the specified key in the specified section as an unsigned short.
    /// </summary>
    public ushort? GetUInt16(string section, string key)
    {
        string stringValue = GetString(section, key);
        return ushort.TryParse(stringValue, out ushort parsedValue) ? parsedValue : (ushort?)null;
    }

    /// <summary>
    /// Gets the value for the specified key in the specified section as an integer.
    /// </summary>
    public int? GetInt32(string section, string key)
    {
        string stringValue = GetString(section, key);
        return int.TryParse(stringValue, out int parsedValue) ? parsedValue : (int?)null;
    }

    /// <summary>
    /// Gets the value for the specified key in the specified section as an unsigned integer.
    /// </summary>
    public uint? GetUInt32(string section, string key)
    {
        string stringValue = GetString(section, key);
        return uint.TryParse(stringValue, out uint parsedValue) ? parsedValue : (uint?)null;
    }

    /// <summary>
    /// Gets the value for the specified key in the specified section as a long.
    /// </summary>
    public long? GetInt64(string section, string key)
    {
        string stringValue = GetString(section, key);
        return long.TryParse(stringValue, out long parsedValue) ? parsedValue : (long?)null;
    }

    /// <summary>
    /// Gets the value for the specified key in the specified section as an unsigned long.
    /// </summary>
    public ulong? GetUInt64(string section, string key)
    {
        string stringValue = GetString(section, key);
        return ulong.TryParse(stringValue, out ulong parsedValue) ? parsedValue : (ulong?)null;
    }

    /// <summary>
    /// Gets the value for the specified key in the specified section as a float.
    /// </summary>
    public float? GetSingle(string section, string key)
    {
        string stringValue = GetString(section, key);
        return float.TryParse(stringValue, NumberStyles.Float, CultureInfo.InvariantCulture, out float parsedValue)
            ? parsedValue
            : (float?)null;
    }

    /// <summary>
    /// Gets the value for the specified key in the specified section as a double.
    /// </summary>
    public double? GetDouble(string section, string key)
    {
        string stringValue = GetString(section, key);
        return double.TryParse(stringValue, NumberStyles.Float, CultureInfo.InvariantCulture, out double parsedValue)
            ? parsedValue
            : (double?)null;
    }

    /// <summary>
    /// Gets the value for the specified key in the specified section as a DateTime.
    /// </summary>
    public DateTime? GetDateTime(string section, string key)
    {
        string stringValue = GetString(section, key);
        return DateTime.TryParse(stringValue, CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime parsedValue)
            ? parsedValue
            : (DateTime?)null;
    }

    /// <summary>
    /// Writes the INI data to the file.
    /// </summary>
    private void WriteFile()
    {
        if (_iniData.Count == 0)
            return;

        using var writer = new StreamWriter(_path);
        foreach (var section in _iniData)
        {
            writer.WriteLine($"[{section.Key}]");

            foreach (var keyValue in section.Value)
            {
                writer.WriteLine($"{keyValue.Key}={keyValue.Value}");
            }

            writer.WriteLine(); // Blank line between sections
        }
    }
}